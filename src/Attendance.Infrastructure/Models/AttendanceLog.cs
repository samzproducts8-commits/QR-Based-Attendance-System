using Attendance.Infrastructure.Enums;

namespace Attendance.Infrastructure.Models;
//The actual recorded attendance event — links
//  a staff member, a slot, and (optionally) the QR session used,
//  with a timestamp/date and status flag (OnTime/Late/ManualEntry). Unique per (Staff, Slot, Date).
public class AttendanceLog
{
    public long AttendanceLogId { get; set; }
    public int StaffId { get; set; }
    public int SlotId { get; set; }
    public int? QrSessionId { get; set; }
    public DateTime EventTimestamp { get; set; }
    public DateOnly EventDate { get; set; }
    public AttendanceStatus StatusFlag { get; set; }

    // Navigation properties
    public Staff Staff { get; set; } = null!;
    public AttendanceSlotConfig Slot { get; set; } = null!;
    public QrSession? QrSession { get; set; }
}
