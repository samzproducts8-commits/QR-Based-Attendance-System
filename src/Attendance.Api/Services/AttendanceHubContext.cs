using Attendance.Api.Hubs;
using Attendance.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Attendance.Api.Services;

/// <summary>
/// Bridges the Application layer's <see cref="IAttendanceHubContext"/>
/// abstraction to the concrete SignalR <see cref="IHubContext{AttendanceHub}"/>,
/// keeping <c>QrSessionService</c> free of any SignalR dependency.
/// </summary>
/// <remarks>
/// Satisfies Requirement 3.6 / 6.1.
/// </remarks>
public sealed class AttendanceHubContext : IAttendanceHubContext
{
    private readonly IHubContext<AttendanceHub> _hubContext;

    public AttendanceHubContext(IHubContext<AttendanceHub> hubContext)
    {
        _hubContext = hubContext;
    }

    /// <inheritdoc />
    public Task SendNewQrCodeAsync(string base64Image, Guid tokenValue, DateTime expiresAt)
        => _hubContext.Clients.All.SendAsync(
            AttendanceHub.ReceiveQrCode, base64Image, tokenValue, expiresAt);
}
