namespace Attendance.Application.Models;

/// <summary>
/// Represents the outcome of a QR token validation and consumption attempt.
/// </summary>
public enum ConsumeStatus
{
    /// <summary>Token was valid, unused, and not expired — successfully consumed.</summary>
    Success = 0,

    /// <summary>No QrSession row exists for the supplied token GUID.</summary>
    TokenNotFound = 1,

    /// <summary>Token exists but has already been consumed by a previous scan.</summary>
    TokenAlreadyUsed = 2,

    /// <summary>Token existed but its ExpiresAt timestamp has passed.</summary>
    TokenExpired = 3
}

/// <summary>
/// Encapsulates the result of a single atomic QR token validation and consumption attempt.
/// </summary>
/// <param name="Status">The outcome classification for the attempt.</param>
/// <param name="SessionId">
/// The database primary key of the consumed <c>QrSession</c> row.
/// Populated only when <see cref="Status"/> is <see cref="ConsumeStatus.Success"/>;
/// otherwise <c>0</c>.
/// </param>
public record QrSessionConsumeResult(ConsumeStatus Status, int SessionId = 0)
{
    /// <summary>Convenience factory for a successful consumption.</summary>
    /// <param name="sessionId">The <c>QrSessionId</c> of the consumed row.</param>
    public static QrSessionConsumeResult Success(int sessionId)
        => new(ConsumeStatus.Success, sessionId);

    /// <summary>Convenience factory for a not-found result.</summary>
    public static QrSessionConsumeResult NotFound()
        => new(ConsumeStatus.TokenNotFound);

    /// <summary>Convenience factory for an already-used result.</summary>
    public static QrSessionConsumeResult AlreadyUsed()
        => new(ConsumeStatus.TokenAlreadyUsed);

    /// <summary>Convenience factory for an expired result.</summary>
    public static QrSessionConsumeResult Expired()
        => new(ConsumeStatus.TokenExpired);
}
