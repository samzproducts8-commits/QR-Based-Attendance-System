namespace Attendance.Application.Interfaces;

/// <summary>
/// Abstraction over the SignalR hub used to push QR code updates to connected
/// kiosk clients.  Defined in the Application layer so that
/// <c>QrSessionService</c> can broadcast new tokens without a compile-time
/// dependency on the API assembly.
/// </summary>
/// <remarks>
/// The concrete implementation lives in <c>Attendance.Api</c> and wraps
/// <c>IHubContext&lt;AttendanceHub&gt;</c>.
/// Satisfies Requirement 3.6.
/// </remarks>
public interface IAttendanceHubContext
{
    /// <summary>
    /// Pushes a freshly generated QR code to all connected kiosk clients.
    /// </summary>
    /// <param name="base64Image">Base64-encoded PNG QR image.</param>
    /// <param name="tokenValue">The GUID embedded in the QR code.</param>
    /// <param name="expiresAt">UTC expiry timestamp of the token.</param>
    Task SendNewQrCodeAsync(string base64Image, Guid tokenValue, DateTime expiresAt);
}
