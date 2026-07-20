namespace Attendance.Application.Enums;

/// <summary>
/// Represents the computed quality of an attendance event.
/// Satisfies Requirement 3.9.
/// </summary>
public enum AttendanceStatus : byte
{
    /// <summary>Employee scanned within the slot's normal window (at or before EndTime).</summary>
    OnTime = 0,

    /// <summary>Employee scanned in the grace tail, after EndTime but within GracePeriodMinutes.</summary>
    Late = 1,

    /// <summary>Attendance was entered manually by HR / Admin (not via QR scan).</summary>
    ManualEntry = 2
}
