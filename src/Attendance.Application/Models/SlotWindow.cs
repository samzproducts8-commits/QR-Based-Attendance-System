namespace Attendance.Application.Models;

/// <summary>
/// Application-layer representation of an attendance time slot window,
/// used by <see cref="Interfaces.ISlotConfigService.ResolveSlotForTime"/> for
/// in-memory slot resolution without coupling the Application project to
/// Infrastructure entity types.
/// </summary>
/// <param name="SlotId">Database primary key.</param>
/// <param name="SlotName">
/// Named slot identifier, one of: <c>MorningIn</c>, <c>LunchOut</c>,
/// <c>LunchIn</c>, <c>EveningOut</c>.
/// </param>
/// <param name="StartTime">The earliest time at which this slot accepts a scan.</param>
/// <param name="EndTime">The latest time at which this slot accepts a scan.</param>
/// <param name="GracePeriodMinutes">
/// Number of minutes after <see cref="EndTime"/> during which a scan is still
/// accepted (extending the closing boundary).  A scan within the normal
/// <see cref="StartTime"/>–<see cref="EndTime"/> window is <em>On Time</em>;
/// a scan in the grace tail after <see cref="EndTime"/> is <em>Late</em>.
/// </param>
/// <param name="IsMandatory">
/// When <see langword="true"/>, absence in this slot is reported as
/// <em>Absent</em> on daily and monthly reports.
/// </param>
/// <param name="IsActive">
/// Only active slots participate in scan resolution and reporting.
/// </param>
public record SlotWindow(
    int SlotId,
    string SlotName,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int GracePeriodMinutes,
    bool IsMandatory,
    bool IsActive);
