namespace Attendance.Infrastructure.Enums;

public enum AttendanceStatus : byte
{
    OnTime = 0,
    Late = 1,
    ManualEntry = 2,
    Absent = 3
}
