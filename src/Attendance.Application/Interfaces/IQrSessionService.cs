using Attendance.Application.DTOs;
using Attendance.Application.Models;

namespace Attendance.Application.Interfaces;

/// <summary>
/// Manages the full lifecycle of QR session tokens:
/// generation, atomic consumption, and background expiry.
/// Satisfies Requirements 3.1–3.8.
/// </summary>
public interface IQrSessionService
{
    /// <summary>
    /// Generates a new single-use QR session token, persists it to the database,
    /// renders a QR code image, and pushes the image to all connected kiosk
    /// clients via SignalR (Requirement 3.6).
    /// </summary>
    /// <remarks>
    /// The token encodes only a random GUID — no employee identity data
    /// (Requirement 3.1).  The token's <c>ExpiresAt</c> is set to
    /// 10–15 seconds from generation time (Requirement 3.2).
    /// </remarks>
    /// <returns>
    /// A <see cref="QrCodeResponseDto"/> containing the new <c>TokenValue</c>,
    /// a base64-encoded PNG QR image, and the <c>ExpiresAt</c> timestamp.
    /// </returns>
    Task<QrCodeResponseDto> GenerateNewTokenAsync();

    /// <summary>
    /// Atomically validates and consumes a QR session token submitted by an
    /// employee scan request (Requirement 3.3).
    /// </summary>
    /// <remarks>
    /// Uses a single conditional <c>UPDATE … WHERE Status = Active AND ExpiresAt &gt; NOW()</c>
    /// to guarantee that concurrent requests for the same token yield exactly
    /// one success (Requirement 3.8).
    /// </remarks>
    /// <param name="tokenValue">
    /// The GUID decoded from the scanned QR code.
    /// Must not be <see cref="Guid.Empty"/>.
    /// </param>
    /// <param name="staffId">
    /// The authenticated employee's <c>Staff.StaffId</c>; recorded on the
    /// consumed row as <c>UsedByStaffId</c> for the audit trail (Requirement 7.1).
    /// </param>
    /// <returns>
    /// A <see cref="QrSessionConsumeResult"/> whose <c>Status</c> is one of:
    /// <see cref="ConsumeStatus.Success"/>, <see cref="ConsumeStatus.TokenNotFound"/>,
    /// <see cref="ConsumeStatus.TokenAlreadyUsed"/>, or <see cref="ConsumeStatus.TokenExpired"/>.
    /// </returns>
    Task<QrSessionConsumeResult> ValidateAndConsumeAsync(Guid tokenValue, int? staffId = null);

    /// <summary>
    /// Returns the currently active QR token (re-rendering its image), or
    /// generates a brand-new one when none exists or the active token has
    /// already passed its expiry.  Used by the kiosk's <c>RequestCurrentQr</c>
    /// hub method so a newly connected kiosk screen immediately shows a valid
    /// code (Requirement 6.2).
    /// </summary>
    Task<QrCodeResponseDto> GetOrCreateCurrentAsync();

    /// <summary>
    /// Transitions all <c>Active</c> tokens whose <c>ExpiresAt</c> timestamp has
    /// passed to <c>Expired</c> status, then generates a replacement token for
    /// each expired one (Requirement 3.7).
    /// </summary>
    /// <remarks>
    /// Intended to be called by a background timer service every 5 seconds.
    /// </remarks>
    /// <returns>The number of tokens transitioned to Expired.</returns>
    Task<int> ExpireStaleTokensAsync();
}
