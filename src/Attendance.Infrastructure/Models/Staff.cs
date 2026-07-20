using Attendance.Infrastructure.Enums;

namespace Attendance.Infrastructure.Models;

public class Staff
{
    public int StaffId { get; set; }
    public string UniqueCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public int DepartmentId { get; set; }
    public string? JobTitle { get; set; }
    public DateOnly EmploymentDate { get; set; }
    public StaffStatus Status { get; set; } = StaffStatus.Active;
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public Department Department { get; set; } = null!;
    public StaffProfile? Profile { get; set; }
    public ICollection<AttendanceLog> AttendanceLogs { get; set; } = [];
    public ICollection<QrSession> UsedQrSessions { get; set; } = [];
}
