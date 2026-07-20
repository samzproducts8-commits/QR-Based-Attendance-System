namespace Attendance.Infrastructure.Models;

public class StaffProfile
{
    public int StaffProfileId { get; set; }
    public int StaffId { get; set; }
    public string PhotoFileName { get; set; } = string.Empty;
    public string PhotoContentType { get; set; } = "image/png";
    public string PhotoPath { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? EmergencyContact { get; set; }

    // Navigation properties, 
    // store additional profile info abt staff member
    public Staff Staff { get; set; } = null!;
}
