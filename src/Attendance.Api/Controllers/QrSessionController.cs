using Attendance.Application.DTOs;
using Attendance.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Attendance.Api.Controllers;

/// <summary>
/// QR session token administration (Requirements 3.1, 3.2).
/// The rotation itself is event-driven (scan success or expiry sweep); these
/// endpoints let an admin bootstrap/inspect the kiosk token.
/// </summary>
[ApiController]
[Route("api/qr")]
[Authorize(Roles = "Admin")]
public sealed class QrSessionController : ControllerBase
{
    private readonly IQrSessionService _qrSessionService;

    public QrSessionController(IQrSessionService qrSessionService)
    {
        _qrSessionService = qrSessionService;
    }

    /// <summary>
    /// Forces generation of a fresh token and pushes it to all kiosk screens —
    /// used to kick off the display when the kiosk first goes live.
    /// </summary>
    [HttpPost("generate")]
    [ProducesResponseType<QrCodeResponseDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Generate()
        => Ok(await _qrSessionService.GenerateNewTokenAsync());

    /// <summary>
    /// Returns the currently active token (generating one when none exists) —
    /// for admin monitoring.
    /// </summary>
    [HttpGet("current")]
    [ProducesResponseType<QrCodeResponseDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrent()
        => Ok(await _qrSessionService.GetOrCreateCurrentAsync());
}
