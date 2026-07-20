using Attendance.Application.Models;

namespace Attendance.Application.Interfaces;

/// <summary>
/// Provides data-access operations for the <c>QrSession</c> aggregate.
/// The interface lives in the Application layer so that
/// <c>QrSessionService</c> (also in Application) can depend on it
/// without a circular reference to the Infrastructure assembly.
/// </summary>
/// <remarks>
/// Satisfies Requirements 3.3 and 3.8.
/// </remarks>
public interface IQrSessionRepository
{
    /// <summary>
    /// Atomically validates and consumes a QR session token.
    /// </summary>
    /// <remarks>
    /// Executes a single conditional <c>UPDATE</c>:
    /// <code>
    /// UPDATE QrSession
    ///    SET Status = 1, UsedAt = SYSUTCDATETIME()
    ///  WHERE TokenValue = @token
    ///    AND Status     = 0
    ///    AND ExpiresAt  &gt; SYSUTCDATETIME()
    /// </code>
    /// The database serialises concurrent executions, so exactly one caller
    /// receives <c>rowsAffected = 1</c> for any given token value —
    /// satisfying the single-use / race-condition-free guarantee
    /// (Requirement 3.8).
    /// <para>
    /// When <c>rowsAffected = 0</c> the caller should re-query via
    /// <see cref="GetByTokenAsync"/> to determine whether the token was
    /// not found, already used, or expired.
    /// </para>
    /// </remarks>
    /// <param name="tokenValue">The GUID decoded from the scanned QR code.</param>
    /// <param name="staffId">
    /// The authenticated employee's <c>Staff.StaffId</c>, written to
    /// <c>UsedByStaffId</c> as part of the same atomic UPDATE
    /// (<see langword="null"/> for anonymous/system consumption).
    /// </param>
    /// <returns>
    /// A tuple of:
    /// <list type="bullet">
    ///   <item><term>rowsAffected</term><description>1 if consumed successfully; 0 otherwise.</description></item>
    ///   <item><term>session</term><description>The consumed session snapshot (populated only when <c>rowsAffected = 1</c>); otherwise <see langword="null"/>.</description></item>
    /// </list>
    /// </returns>
    Task<(int rowsAffected, QrSessionSnapshot? session)> ConsumeTokenAsync(Guid tokenValue, int? staffId = null);

    /// <summary>
    /// Retrieves a <see cref="QrSessionSnapshot"/> by its token GUID regardless
    /// of status, for post-failure reason classification.
    /// </summary>
    /// <param name="tokenValue">The GUID to look up.</param>
    /// <returns>
    /// The matching snapshot, or <see langword="null"/> if no row exists.
    /// </returns>
    Task<QrSessionSnapshot?> GetByTokenAsync(Guid tokenValue);

    /// <summary>
    /// Returns the current active QR session token, or <see langword="null"/>
    /// if none exists.
    /// </summary>
    Task<QrSessionSnapshot?> GetActiveTokenAsync();

    /// <summary>
    /// Persists a new <c>QrSession</c> row to the database.
    /// </summary>
    /// <param name="tokenValue">The new unique GUID for the session.</param>
    /// <param name="generatedAt">UTC timestamp at which the token was generated.</param>
    /// <param name="expiresAt">UTC timestamp at which the token expires.</param>
    Task AddAsync(Guid tokenValue, DateTime generatedAt, DateTime expiresAt);

    /// <summary>
    /// Transitions all <c>Active</c> tokens whose <c>ExpiresAt</c> has passed
    /// to <c>Expired</c> status.
    /// </summary>
    /// <returns>The number of rows updated.</returns>
    Task<int> ExpireStaleTokensAsync();
}
