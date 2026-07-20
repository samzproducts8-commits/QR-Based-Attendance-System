using Attendance.Infrastructure.Enums;

namespace Attendance.Infrastructure.Models;
//A single-use, time-boxed QR token (GUID) shown on the kiosk;
//  tracks status (Active/Used/Expired) and who redeemed it.
public class QrSession
{
    public int QrSessionId { get; set; }
    public Guid TokenValue { get; set; }
    public DateTime GeneratedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public QrSessionStatus Status { get; set; }
    public int? UsedByStaffId { get; set; }
    public DateTime? UsedAt { get; set; }

    // Navigation properties
    //Stores QR code session details 
    // (generated QR code, validity period, status).
    public Staff? UsedByStaff { get; set; }
    public ICollection<AttendanceLog> AttendanceLogs { get; set; } = [];
}
