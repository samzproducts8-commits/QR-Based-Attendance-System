using Attendance.Application.DTOs;
using Attendance.Application.Enums;
using Attendance.Application.Interfaces;
using Attendance.Application.Models;
using Attendance.Application.Services;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Moq;

namespace Attendance.Tests.PropertyTests;

// ---------------------------------------------------------------------------
// FsCheck v3 Arbitraries
// ---------------------------------------------------------------------------

/// <summary>
/// Generators for QrSessionService property tests.
/// </summary>
public static class QrSessionArbitraries
{
    /// <summary>
    /// Generates a random, non-empty GUID.
    /// </summary>
    public static Arbitrary<Guid> NonEmptyGuidArb()
    {
        var gen = from a in Gen.Choose(1, int.MaxValue)
                  from b in Gen.Choose(0, int.MaxValue)
                  from c in Gen.Choose(0, int.MaxValue)
                  from d in Gen.Choose(0, int.MaxValue)
                  select new Guid(a, (short)(b & 0xFFFF), (short)((b >> 16) & 0xFFFF),
                                  (byte)(c & 0xFF), (byte)((c >> 8) & 0xFF),
                                  (byte)((c >> 16) & 0xFF), (byte)((c >> 24) & 0xFF),
                                  (byte)(d & 0xFF), (byte)((d >> 8) & 0xFF),
                                  (byte)((d >> 16) & 0xFF), (byte)((d >> 24) & 0xFF));
        return Arb.From(gen);
    }

    /// <summary>
    /// Generates a <see cref="QrSessionSnapshot"/> with the given token, status Used or Expired.
    /// Used to test the immutability property.
    /// </summary>
    public static Arbitrary<QrSessionSnapshot> UsedOrExpiredSnapshotArb()
    {
        var now = DateTime.UtcNow;
        var gen =
            Gen.Elements<QrSessionStatusCode>(QrSessionStatusCode.Used, QrSessionStatusCode.Expired)
               .SelectMany(status =>
                   Gen.Choose(1, 10_000).Select(id =>
                       new QrSessionSnapshot(
                           QrSessionId: id,
                           TokenValue: Guid.NewGuid(),
                           GeneratedAt: now.AddSeconds(-20),
                           ExpiresAt: now.AddSeconds(-5),
                           Status: status,
                           UsedByStaffId: status == QrSessionStatusCode.Used ? 42 : null,
                           UsedAt: status == QrSessionStatusCode.Used ? now.AddSeconds(-1) : null)));
        return Arb.From(gen);
    }
}

// ---------------------------------------------------------------------------
// Shared mock builder helpers
// ---------------------------------------------------------------------------

/// <summary>
/// Builder helpers so each property test can create a service instance
/// with minimal boilerplate.
/// </summary>
internal static class QrServiceBuilder
{
    /// <summary>
    /// Creates a <see cref="QrSessionService"/> wired to the supplied mocks.
    /// Also stubs AddAsync and SendNewQrCodeAsync to avoid null-ref in
    /// <c>GenerateNewTokenAsync</c> (called internally after a successful consume).
    /// </summary>
    public static (QrSessionService Service,
                   Mock<IQrSessionRepository> RepoMock,
                   Mock<IAttendanceHubContext> HubMock)
        Build(Action<Mock<IQrSessionRepository>>? configureRepo = null)
    {
        var repoMock = new Mock<IQrSessionRepository>(MockBehavior.Strict);
        var hubMock  = new Mock<IAttendanceHubContext>(MockBehavior.Strict);

        // Stub AddAsync so GenerateNewTokenAsync doesn't throw.
        repoMock
            .Setup(r => r.AddAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);

        // Stub SendNewQrCodeAsync so the hub push doesn't throw.
        hubMock
            .Setup(h => h.SendNewQrCodeAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);

        configureRepo?.Invoke(repoMock);

        var service = new QrSessionService(repoMock.Object, hubMock.Object);
        return (service, repoMock, hubMock);
    }
}

// ---------------------------------------------------------------------------
// Property 1: Token Single-Use Guarantee
// Validates: Requirements 3.3, 3.5, 3.8
// ---------------------------------------------------------------------------

/// <summary>
/// <b>Property 1: Token Single-Use Guarantee</b>
/// <para>
/// For any random GUID token, if the repository simulates two sequential consume
/// calls — the first returning (1 row, session) and the second returning (0 rows, null)
/// followed by a re-query that returns a Used snapshot — then exactly one call
/// must receive <see cref="ConsumeStatus.Success"/> and the other must receive
/// <see cref="ConsumeStatus.TokenAlreadyUsed"/>.
/// </para>
/// <b>Validates: Requirements 3.3, 3.5, 3.8</b>
/// </summary>
public class QrSessionServiceProperty1_SingleUseGuarantee
{
    // -----------------------------------------------------------------------
    // Core property: first caller gets Success, second gets AlreadyUsed
    // -----------------------------------------------------------------------

    /// <summary>
    /// For any random token GUID, simulating atomic two-concurrent-call
    /// semantics: the call that wins the DB race (rowsAffected=1) returns
    /// Success; the loser (rowsAffected=0, snapshot.Status=Used) returns
    /// AlreadyUsed.
    /// <b>Validates: Requirements 3.3, 3.8</b>
    /// </summary>
    [Property(Arbitrary = [typeof(QrSessionArbitraries)], MaxTest = 300)]
    public Property Property1_FirstCallSucceeds_SecondCallAlreadyUsed(Guid token)
    {
        if (token == Guid.Empty)
            return Prop.ToProperty(true); // skip degenerate input

        var now = DateTime.UtcNow;

        // Snapshot returned on the successful consume
        var successSnapshot = new QrSessionSnapshot(
            QrSessionId: 1,
            TokenValue: token,
            GeneratedAt: now.AddSeconds(-5),
            ExpiresAt: now.AddSeconds(10),
            Status: QrSessionStatusCode.Used,
            UsedByStaffId: null,
            UsedAt: now);

        // Snapshot returned on the re-query after the failed consume
        var alreadyUsedSnapshot = new QrSessionSnapshot(
            QrSessionId: 1,
            TokenValue: token,
            GeneratedAt: now.AddSeconds(-5),
            ExpiresAt: now.AddSeconds(10),
            Status: QrSessionStatusCode.Used,
            UsedByStaffId: 99,
            UsedAt: now.AddMilliseconds(-10));

        // Simulate two sequential calls:
        // call 1 → (1, successSnapshot)  [wins the race]
        // call 2 → (0, null)              [loses the race]
        var callCount = 0;
        var repoMock = new Mock<IQrSessionRepository>(MockBehavior.Strict);

        repoMock
            .Setup(r => r.ConsumeTokenAsync(token, It.IsAny<int?>()))
            .Returns(() =>
            {
                callCount++;
                return callCount == 1
                    ? Task.FromResult<(int, QrSessionSnapshot?)>((1, successSnapshot))
                    : Task.FromResult<(int, QrSessionSnapshot?)>((0, null));
            });

        // On the second call's re-query, return the already-used snapshot
        repoMock
            .Setup(r => r.GetByTokenAsync(token))
            .ReturnsAsync(alreadyUsedSnapshot);

        // Stub infra for GenerateNewTokenAsync (called internally after first success)
        repoMock
            .Setup(r => r.AddAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);

        var hubMock = new Mock<IAttendanceHubContext>(MockBehavior.Strict);
        hubMock
            .Setup(h => h.SendNewQrCodeAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);

        var service = new QrSessionService(repoMock.Object, hubMock.Object);

        // First call — should succeed
        var result1 = service.ValidateAndConsumeAsync(token).GetAwaiter().GetResult();

        // Second call — should be rejected as already used
        var result2 = service.ValidateAndConsumeAsync(token).GetAwaiter().GetResult();

        bool firstWins  = result1.Status == ConsumeStatus.Success;
        bool secondLoses = result2.Status == ConsumeStatus.TokenAlreadyUsed;

        return Prop.Label(
            firstWins && secondLoses,
            $"Token={token:D} | " +
            $"First={result1.Status} (expected Success), " +
            $"Second={result2.Status} (expected TokenAlreadyUsed)");
    }

    // -----------------------------------------------------------------------
    // Variant: success result carries a non-zero SessionId
    // -----------------------------------------------------------------------

    /// <summary>
    /// The successful consume result must include the database SessionId
    /// from the consumed snapshot.
    /// <b>Validates: Requirement 3.3</b>
    /// </summary>
    [Property(Arbitrary = [typeof(QrSessionArbitraries)], MaxTest = 300)]
    public Property Property1_SuccessResult_CarriesCorrectSessionId(Guid token)
    {
        if (token == Guid.Empty)
            return Prop.ToProperty(true);

        var now = DateTime.UtcNow;
        var expectedSessionId = Math.Abs(token.GetHashCode()) % 100_000 + 1; // deterministic non-zero

        var snapshot = new QrSessionSnapshot(
            QrSessionId: expectedSessionId,
            TokenValue: token,
            GeneratedAt: now.AddSeconds(-3),
            ExpiresAt: now.AddSeconds(12),
            Status: QrSessionStatusCode.Used,
            UsedByStaffId: null,
            UsedAt: now);

        var (service, _, _) = QrServiceBuilder.Build(repo =>
            repo.Setup(r => r.ConsumeTokenAsync(token, It.IsAny<int?>()))
                .ReturnsAsync((1, (QrSessionSnapshot?)snapshot)));

        var result = service.ValidateAndConsumeAsync(token).GetAwaiter().GetResult();

        return Prop.Label(
            result.Status == ConsumeStatus.Success && result.SessionId == expectedSessionId,
            $"Expected Success with SessionId={expectedSessionId}, " +
            $"got Status={result.Status}, SessionId={result.SessionId}");
    }

    // -----------------------------------------------------------------------
    // Variant: loser with NotFound snapshot → TokenNotFound
    // -----------------------------------------------------------------------

    /// <summary>
    /// When rowsAffected=0 and there is no row in the database for the token,
    /// the service returns TokenNotFound — never Success.
    /// <b>Validates: Requirement 3.5</b>
    /// </summary>
    [Property(Arbitrary = [typeof(QrSessionArbitraries)], MaxTest = 300)]
    public Property Property1_ZeroRowsAndNoSnapshot_ReturnsTokenNotFound(Guid token)
    {
        if (token == Guid.Empty)
            return Prop.ToProperty(true);

        var (service, _, _) = QrServiceBuilder.Build(repo =>
        {
            repo.Setup(r => r.ConsumeTokenAsync(token, It.IsAny<int?>()))
                .ReturnsAsync((0, (QrSessionSnapshot?)null));

            repo.Setup(r => r.GetByTokenAsync(token))
                .ReturnsAsync((QrSessionSnapshot?)null);
        });

        var result = service.ValidateAndConsumeAsync(token).GetAwaiter().GetResult();

        return Prop.Label(
            result.Status == ConsumeStatus.TokenNotFound,
            $"Expected TokenNotFound, got {result.Status}");
    }

    // -----------------------------------------------------------------------
    // Variant: loser with Expired snapshot → TokenExpired
    // -----------------------------------------------------------------------

    /// <summary>
    /// When rowsAffected=0 and the re-query reveals an Expired snapshot,
    /// the service returns TokenExpired — never Success.
    /// <b>Validates: Requirement 3.5</b>
    /// </summary>
    [Property(Arbitrary = [typeof(QrSessionArbitraries)], MaxTest = 300)]
    public Property Property1_ZeroRowsAndExpiredSnapshot_ReturnsTokenExpired(Guid token)
    {
        if (token == Guid.Empty)
            return Prop.ToProperty(true);

        var now = DateTime.UtcNow;
        var expiredSnapshot = new QrSessionSnapshot(
            QrSessionId: 7,
            TokenValue: token,
            GeneratedAt: now.AddSeconds(-30),
            ExpiresAt: now.AddSeconds(-15),
            Status: QrSessionStatusCode.Expired,
            UsedByStaffId: null,
            UsedAt: null);

        var (service, _, _) = QrServiceBuilder.Build(repo =>
        {
            repo.Setup(r => r.ConsumeTokenAsync(token, It.IsAny<int?>()))
                .ReturnsAsync((0, (QrSessionSnapshot?)null));

            repo.Setup(r => r.GetByTokenAsync(token))
                .ReturnsAsync(expiredSnapshot);
        });

        var result = service.ValidateAndConsumeAsync(token).GetAwaiter().GetResult();

        return Prop.Label(
            result.Status == ConsumeStatus.TokenExpired,
            $"Expected TokenExpired, got {result.Status}");
    }
}

// ---------------------------------------------------------------------------
// Property 7: Token State Immutability
// Validates: Requirements 3.3, 3.5, 3.8
// ---------------------------------------------------------------------------

/// <summary>
/// <b>Property 7: Token State Immutability</b>
/// <para>
/// After receiving a <see cref="ConsumeStatus.TokenAlreadyUsed"/> or
/// <see cref="ConsumeStatus.TokenExpired"/> result, the service must not issue
/// any further state-changing repository calls (no AddAsync that would create a
/// new session, no extra ConsumeTokenAsync calls).  The immutable terminal
/// states must remain inert.
/// </para>
/// <b>Validates: Requirements 3.3, 3.5, 3.8</b>
/// </summary>
public class QrSessionServiceProperty7_StateImmutability
{
    // -----------------------------------------------------------------------
    // Core property: Used/Expired terminal states cause no state changes
    // -----------------------------------------------------------------------

    /// <summary>
    /// For any random token with a Used or Expired snapshot, calling
    /// <c>ValidateAndConsumeAsync</c> must NOT trigger <c>AddAsync</c>
    /// (which would generate a new token) since no successful consumption
    /// occurred.
    /// <b>Validates: Requirements 3.3, 3.8</b>
    /// </summary>
    [Property(Arbitrary = [typeof(QrSessionArbitraries)], MaxTest = 300)]
    public Property Property7_UsedOrExpiredResult_NoStateChangeRequested(
        QrSessionSnapshot terminalSnapshot)
    {
        // terminalSnapshot is already Used or Expired (from the arbitrary)
        var token = terminalSnapshot.TokenValue;

        var addAsyncCallCount = 0;

        var repoMock = new Mock<IQrSessionRepository>(MockBehavior.Strict);

        // ConsumeTokenAsync finds 0 rows (the DB won't update a non-Active token)
        repoMock
            .Setup(r => r.ConsumeTokenAsync(token, It.IsAny<int?>()))
            .ReturnsAsync((0, (QrSessionSnapshot?)null));

        // Re-query returns the terminal snapshot
        repoMock
            .Setup(r => r.GetByTokenAsync(token))
            .ReturnsAsync(terminalSnapshot);

        // Track whether AddAsync is called (it shouldn't be)
        repoMock
            .Setup(r => r.AddAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Callback(() => addAsyncCallCount++)
            .Returns(Task.CompletedTask);

        var hubMock = new Mock<IAttendanceHubContext>(MockBehavior.Strict);
        hubMock
            .Setup(h => h.SendNewQrCodeAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);

        var service = new QrSessionService(repoMock.Object, hubMock.Object);

        var result = service.ValidateAndConsumeAsync(token).GetAwaiter().GetResult();

        bool isTerminalResult =
            result.Status == ConsumeStatus.TokenAlreadyUsed ||
            result.Status == ConsumeStatus.TokenExpired;

        bool noStateChangeIssued = addAsyncCallCount == 0;

        return Prop.Label(
            isTerminalResult && noStateChangeIssued,
            $"Token={token:D} | SnapshotStatus={terminalSnapshot.Status} | " +
            $"Result={result.Status} (expected AlreadyUsed or Expired) | " +
            $"AddAsync calls={addAsyncCallCount} (expected 0)");
    }

    // -----------------------------------------------------------------------
    // Variant: Used snapshot → exactly AlreadyUsed, no hub push
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the snapshot is Used, the result is TokenAlreadyUsed and the
    /// SignalR hub receives no push (no new QR generated).
    /// <b>Validates: Requirements 3.3, 3.8</b>
    /// </summary>
    [Property(Arbitrary = [typeof(QrSessionArbitraries)], MaxTest = 300)]
    public Property Property7_UsedSnapshot_NoHubPush(Guid token)
    {
        if (token == Guid.Empty)
            return Prop.ToProperty(true);

        var now = DateTime.UtcNow;
        var hubPushCount = 0;

        var usedSnapshot = new QrSessionSnapshot(
            QrSessionId: 5,
            TokenValue: token,
            GeneratedAt: now.AddSeconds(-10),
            ExpiresAt: now.AddSeconds(5),
            Status: QrSessionStatusCode.Used,
            UsedByStaffId: 1,
            UsedAt: now.AddSeconds(-1));

        var repoMock = new Mock<IQrSessionRepository>(MockBehavior.Strict);
        repoMock
            .Setup(r => r.ConsumeTokenAsync(token, It.IsAny<int?>()))
            .ReturnsAsync((0, (QrSessionSnapshot?)null));
        repoMock
            .Setup(r => r.GetByTokenAsync(token))
            .ReturnsAsync(usedSnapshot);
        repoMock
            .Setup(r => r.AddAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);

        var hubMock = new Mock<IAttendanceHubContext>(MockBehavior.Strict);
        hubMock
            .Setup(h => h.SendNewQrCodeAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<DateTime>()))
            .Callback(() => hubPushCount++)
            .Returns(Task.CompletedTask);

        var service = new QrSessionService(repoMock.Object, hubMock.Object);
        var result  = service.ValidateAndConsumeAsync(token).GetAwaiter().GetResult();

        return Prop.Label(
            result.Status == ConsumeStatus.TokenAlreadyUsed && hubPushCount == 0,
            $"Result={result.Status} (expected AlreadyUsed), HubPushCount={hubPushCount} (expected 0)");
    }

    // -----------------------------------------------------------------------
    // Variant: Expired snapshot → exactly TokenExpired, no hub push
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the snapshot is Expired, the result is TokenExpired and the
    /// SignalR hub receives no push (no new QR generated).
    /// <b>Validates: Requirements 3.5, 3.8</b>
    /// </summary>
    [Property(Arbitrary = [typeof(QrSessionArbitraries)], MaxTest = 300)]
    public Property Property7_ExpiredSnapshot_NoHubPush(Guid token)
    {
        if (token == Guid.Empty)
            return Prop.ToProperty(true);

        var now = DateTime.UtcNow;
        var hubPushCount = 0;

        var expiredSnapshot = new QrSessionSnapshot(
            QrSessionId: 8,
            TokenValue: token,
            GeneratedAt: now.AddSeconds(-30),
            ExpiresAt: now.AddSeconds(-15),
            Status: QrSessionStatusCode.Expired,
            UsedByStaffId: null,
            UsedAt: null);

        var repoMock = new Mock<IQrSessionRepository>(MockBehavior.Strict);
        repoMock
            .Setup(r => r.ConsumeTokenAsync(token, It.IsAny<int?>()))
            .ReturnsAsync((0, (QrSessionSnapshot?)null));
        repoMock
            .Setup(r => r.GetByTokenAsync(token))
            .ReturnsAsync(expiredSnapshot);
        repoMock
            .Setup(r => r.AddAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);

        var hubMock = new Mock<IAttendanceHubContext>(MockBehavior.Strict);
        hubMock
            .Setup(h => h.SendNewQrCodeAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<DateTime>()))
            .Callback(() => hubPushCount++)
            .Returns(Task.CompletedTask);

        var service = new QrSessionService(repoMock.Object, hubMock.Object);
        var result  = service.ValidateAndConsumeAsync(token).GetAwaiter().GetResult();

        return Prop.Label(
            result.Status == ConsumeStatus.TokenExpired && hubPushCount == 0,
            $"Result={result.Status} (expected TokenExpired), HubPushCount={hubPushCount} (expected 0)");
    }

    // -----------------------------------------------------------------------
    // Positive control: Success DOES trigger a new token generation
    // -----------------------------------------------------------------------

    /// <summary>
    /// Contrast test: a successful consume MUST trigger exactly one hub push
    /// (the new QR code generated after consuming the token).
    /// This confirms the immutability property is meaningful — failures
    /// suppress the push that successes require.
    /// <b>Validates: Requirement 3.5</b>
    /// </summary>
    [Property(Arbitrary = [typeof(QrSessionArbitraries)], MaxTest = 300)]
    public Property Property7_SuccessResult_TriggersExactlyOneHubPush(Guid token)
    {
        if (token == Guid.Empty)
            return Prop.ToProperty(true);

        var now = DateTime.UtcNow;
        var hubPushCount = 0;

        var successSnapshot = new QrSessionSnapshot(
            QrSessionId: 42,
            TokenValue: token,
            GeneratedAt: now.AddSeconds(-3),
            ExpiresAt: now.AddSeconds(12),
            Status: QrSessionStatusCode.Used,
            UsedByStaffId: null,
            UsedAt: now);

        var repoMock = new Mock<IQrSessionRepository>(MockBehavior.Strict);
        repoMock
            .Setup(r => r.ConsumeTokenAsync(token, It.IsAny<int?>()))
            .ReturnsAsync((1, (QrSessionSnapshot?)successSnapshot));
        repoMock
            .Setup(r => r.AddAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);

        var hubMock = new Mock<IAttendanceHubContext>(MockBehavior.Strict);
        hubMock
            .Setup(h => h.SendNewQrCodeAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<DateTime>()))
            .Callback(() => hubPushCount++)
            .Returns(Task.CompletedTask);

        var service = new QrSessionService(repoMock.Object, hubMock.Object);
        var result  = service.ValidateAndConsumeAsync(token).GetAwaiter().GetResult();

        return Prop.Label(
            result.Status == ConsumeStatus.Success && hubPushCount == 1,
            $"Result={result.Status} (expected Success), HubPushCount={hubPushCount} (expected 1)");
    }
}
