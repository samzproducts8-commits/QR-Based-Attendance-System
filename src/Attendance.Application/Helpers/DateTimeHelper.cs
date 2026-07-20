using Attendance.Application.Enums;
using Attendance.Application.Models;

namespace Attendance.Application.Helpers;

/// <summary>
/// Pure, stateless helper for date/time computations used during attendance
/// recording.
/// </summary>
/// <remarks>
/// Satisfies Requirements 3.9, 3.11, and 4.1.
/// </remarks>
public static class DateTimeHelper
{
    /// <summary>
    /// Fixed offset of the office's local time from UTC.
    /// </summary>
    /// <remarks>
    /// Ethiopia (East Africa Time) is <c>UTC+3</c> year-round with no daylight
    /// saving, so a fixed offset is exact and avoids cross-platform time-zone
    /// database differences (Windows vs. IANA ids). Change this single value if
    /// the office relocates to another zone.
    /// </remarks>
    public static readonly TimeSpan OfficeUtcOffset = TimeSpan.FromHours(3);

    /// <summary>
    /// The current wall-clock instant at the office, derived from
    /// <see cref="DateTime.UtcNow"/> plus <see cref="OfficeUtcOffset"/>.
    /// </summary>
    /// <remarks>
    /// Slot windows, the daily event date, and stored event timestamps are all
    /// expressed in office-local time so that a scan at 08:30 on the office
    /// clock resolves against the 08:00–09:00 slot.
    /// </remarks>
    public static DateTime OfficeNow() => DateTime.UtcNow + OfficeUtcOffset;

    /// <summary>
    /// Computes whether an attendance event is <see cref="AttendanceStatus.OnTime"/>
    /// or <see cref="AttendanceStatus.Late"/> based on the grace-period rule.
    /// </summary>
    /// <param name="eventTime">The office-local time-of-day at which the scan occurred.</param>
    /// <param name="slot">
    /// The resolved slot that contains <paramref name="eventTime"/> within its
    /// window (<c>StartTime</c>–<c>EndTime</c> plus the grace-period tail).
    /// </param>
    /// <returns>
    /// <see cref="AttendanceStatus.OnTime"/> when
    /// <c>eventTime &lt;= slot.EndTime</c>; <see cref="AttendanceStatus.Late"/>
    /// otherwise (i.e. a scan that landed in the grace-period tail after
    /// <c>EndTime</c>).
    /// </returns>
    /// <remarks>
    /// The grace period extends the slot's <em>closing</em> boundary
    /// (Requirement 3.9 / Algorithm 2): scans anywhere inside the normal
    /// <c>StartTime</c>–<c>EndTime</c> window are On Time, and scans in the
    /// <c>EndTime</c>–<c>EndTime + GracePeriodMinutes</c> tail are Late.
    /// A scan at exactly <c>EndTime</c> is On Time (inclusive upper bound);
    /// <see cref="SlotResolver.ResolveSlotForTime"/> guarantees the caller only
    /// reaches here for a scan at or before the grace deadline.
    /// </remarks>
    public static AttendanceStatus ComputeStatusFlag(TimeOnly eventTime, SlotWindow slot)
    {
        return eventTime <= slot.EndTime
            ? AttendanceStatus.OnTime
            : AttendanceStatus.Late;
    }
}
