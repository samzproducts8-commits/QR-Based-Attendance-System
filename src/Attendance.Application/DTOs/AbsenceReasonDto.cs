namespace Attendance.Application.DTOs;

/// <summary>
/// Request DTO for an Admin/HR user to set or update the absence reason
/// for a staff member's missed mandatory slot.
/// </summary>
/// <param name="StaffId">Staff member primary key.</param>
/// <param name="SlotId">Slot configuration primary key.</param>
/// <param name="Date">Calendar date of the absence.</param>
/// <param name="Reason">Free-text reason for the absence (max 500 chars).</param>
public record SetAbsenceReasonDto(
    int StaffId,
    int SlotId,
    DateOnly Date,
    string Reason
);
