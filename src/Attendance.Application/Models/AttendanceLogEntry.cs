using Attendance.Application.Enums;

namespace Attendance.Application.Models;

/// <summary>
/// Layer-agnostic projection of an <c>AttendanceLog</c> row returned by
/// <see cref="Interfaces.IAttendanceRepository"/> for report building and
/// history queries.
/// </summary>
/// <param name="AttendanceLogId">Database primary key.</param>
/// <param name="StaffId">Staff member the event belongs to.</param>
/// <param name="SlotId">Slot configuration the event was recorded against.</param>
/// <param name="EventTimestamp">UTC timestamp of the scan.</param>
/// <param name="EventDate">Calendar date of the scan (fast lookup key).</param>
/// <param name="StatusFlag">Computed status: OnTime, Late, or ManualEntry.</param>
public sealed record AttendanceLogEntry(
    long AttendanceLogId,
    int StaffId,
    int SlotId,
    DateTime EventTimestamp,
    DateOnly EventDate,
    AttendanceStatus StatusFlag
);
