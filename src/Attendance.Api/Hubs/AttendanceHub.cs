using Attendance.Application.DTOs;
using Attendance.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Attendance.Api.Hubs;

/// <summary>
/// SignalR hub that pushes freshly generated QR codes to connected kiosk
/// screens in real time.
/// </summary>
/// <remarks>
/// <para>
/// Client-side event: <c>ReceiveQrCode(base64Png, tokenValue, expiresAt)</c>.
/// </para>
/// <para>
/// Deliberately anonymous: the kiosk is a public display screen (an office
/// entrance monitor/tablet) meant to run unattended all day, and the QR
/// payload it broadcasts carries no security value worth gating behind a
/// login — it is a random, single-use, ~15-second-lived token with no
/// employee identity embedded (see docs/QR-Security.md). Gating this hub
/// behind auth would only force the kiosk to be logged in and would break
/// every ~15 minutes when its access token expired, with no benefit.
/// Recording an attendance event (the actual sensitive action) still
/// requires a full Employee-role JWT via <c>POST /api/attendance/scan</c>.
/// Satisfies Requirements 6.1–6.3.
/// </para>
/// </remarks>
public sealed class AttendanceHub : Hub
{
    /// <summary>Client-side event name for incoming QR updates.</summary>
    public const string ReceiveQrCode = "ReceiveQrCode";

    private readonly IQrSessionService _qrSessionService;

    public AttendanceHub(IQrSessionService qrSessionService)
    {
        _qrSessionService = qrSessionService;
    }

    /// <summary>
    /// Called by a kiosk right after connecting (or reconnecting) so it
    /// immediately receives the current QR code instead of waiting for the
    /// next rotation (Requirement 6.2).  Generates a fresh token when none
    /// is active.
    /// </summary>
    public async Task RequestCurrentQr()
    {
        QrCodeResponseDto current = await _qrSessionService.GetOrCreateCurrentAsync();

        await Clients.Caller.SendAsync(
            ReceiveQrCode, current.QrImageBase64, current.TokenValue, current.ExpiresAt);
    }
}
