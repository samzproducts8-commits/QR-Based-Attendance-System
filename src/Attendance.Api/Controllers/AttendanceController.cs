using System.Security.Claims;
using Attendance.Api.Services;
using Attendance.Application.DTOs;
using Attendance.Application.Interfaces;
using Attendance.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Attendance.Api.Controllers;

/// <summary>
/// The scan endpoint (core feature) and the employee's own history
/// (Requirements 3.3–3.12, 5.4, 5.5).
/// </summary>
[ApiController]
[Route("api/attendance")]
public sealed class AttendanceController : ControllerBase
{
    private readonly IQrSessionService _qrSessionService;
    private readonly IAttendanceService _attendanceService;
    private readonly ILogger<AttendanceController> _logger;

    public AttendanceController(
        IQrSessionService qrSessionService,
        IAttendanceService attendanceService,
        ILogger<AttendanceController> logger)
    {
        _qrSessionService  = qrSessionService;
        _attendanceService = attendanceService;
        _logger            = logger;
    }

    /// <summary>
    /// Records an attendance event: atomically consumes the scanned QR token,
    /// resolves the current slot, and inserts the log.  The staff identity
    /// comes exclusively from the caller's JWT — the token itself carries no
    /// identity (Requirement 3.1).
    /// </summary>
    /// <response code="200">Attendance recorded; body carries the greeting message.</response>
    /// <response code="404">Token not recognised.</response>
    /// <response code="409">Token already used, or slot already recorded today.</response>
    /// <response code="410">Token expired.</response>
    /// <response code="422">No attendance slot open at this time.</response>
    [HttpPost("scan")]
    [Authorize(Roles = "Employee")]
    [ProducesResponseType<AttendanceRecordDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Scan([FromBody] ScanRequestDto request)
    {
        int? staffId = GetStaffId();// it returns if the token is valid
        if (staffId is null)        // but not contain staffid
            return Forbid();

        if (request.Token == Guid.Empty)
            return BadRequest(new { error = "A QR token is required." });

        QrSessionConsumeResult consumeResult =
            await _qrSessionService.ValidateAndConsumeAsync(request.Token, staffId);

        // Audit trail (Requirement 7.1): every attempt, success or failure,
        // with truncated token, outcome, and client IP.
        _logger.LogInformation(
            "QR scan attempt: StaffId={StaffId} Token={TokenPrefix}… Outcome={Outcome} Ip={Ip}",
            staffId,
            request.Token.ToString()[..8],
            consumeResult.Status,
            HttpContext.Connection.RemoteIpAddress);

        return consumeResult.Status switch
        {
            ConsumeStatus.Success =>
                Ok(await _attendanceService.RecordAttendanceAsync(staffId.Value, consumeResult.SessionId)),
            ConsumeStatus.TokenNotFound =>
                NotFound(new { error = "QR code not recognized." }),
            ConsumeStatus.TokenAlreadyUsed =>
                Conflict(new { error = "This QR code has already been used. Please scan the new code." }),
            ConsumeStatus.TokenExpired =>
                StatusCode(StatusCodes.Status410Gone,
                    new { error = "QR code has expired. Please scan the current code on the kiosk." }),
            _ => BadRequest(new { error = "The QR token could not be processed." })
        };
    }

    /// <summary>
    /// Returns the authenticated employee's own attendance history
    /// (Requirement 5.5 / Property 9: records are filtered by the staff id in
    /// the caller's JWT, never by a client-supplied id).
    /// </summary>
    [HttpGet("my-history")]
    [Authorize(Roles = "Employee")]
    [ProducesResponseType<IReadOnlyList<AttendanceHistoryEntry>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> MyHistory(
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null)
    {
        int? staffId = GetStaffId();
        if (staffId is null)
            return Forbid();

        return Ok(await _attendanceService.GetHistoryAsync(staffId.Value, from, to));
    }

    /// <summary>
    /// Extracts the linked staff id from the JWT's <c>staffId</c> claim;
    /// null for accounts with no staff record.
    /// </summary>
    private int? GetStaffId()
        => int.TryParse(User.FindFirstValue(JwtTokenService.StaffIdClaim), out int id) ? id : null;
}
