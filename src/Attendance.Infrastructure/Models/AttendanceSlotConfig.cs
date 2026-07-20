using Attendance.Infrastructure.Enums;

namespace Attendance.Infrastructure.Models;

public class AttendanceSlotConfig
{
    public int SlotId { get; set; }
    public string SlotName { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int GracePeriodMinutes { get; set; } = 0;
    public bool IsMandatory { get; set; } = true;
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<AttendanceLog> AttendanceLogs { get; set; } = [];
}
