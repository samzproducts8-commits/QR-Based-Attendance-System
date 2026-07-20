namespace Attendance.Application.DTOs;

/// <summary>
/// Daily attendance report for a single staff member showing all slot entries.
/// Satisfies Requirements 4.1 and 4.5.
/// </summary>
/// <param name="StaffId">Staff member primary key.</param>
/// <param name="StaffName">Full name of the staff member.</param>
/// <param name="Date">The calendar date this sheet covers.</param>
/// <param name="Entries">
/// One entry per configured slot; mandatory slots with no log show "Absent".
/// </param>
public record DailyAttendanceSheet(
    int StaffId,
    string StaffName,
    DateOnly Date,
    IReadOnlyList<DailySlotEntry> Entries
);

/// <summary>
/// A single slot entry within a daily attendance sheet.
/// </summary>
/// <param name="SlotId">Slot configuration primary key.</param>
/// <param name="SlotName">Human-readable slot name (e.g. "MorningIn").</param>
/// <param name="EventTimestamp">
/// UTC timestamp of the recorded scan, or <see langword="null"/> when absent.
/// </param>
/// <param name="StatusLabel">
/// "On Time", "Late", or "Absent" (Requirement 4.5).
/// </param>
public record DailySlotEntry(
    int SlotId,
    string SlotName,
    DateTime? EventTimestamp,
    string StatusLabel
);

/// <summary>
/// Monthly aggregated attendance summary, optionally filtered by department or staff.
/// Satisfies Requirements 4.2 and 4.4.
/// </summary>
/// <param name="Year">The four-digit year of the report period.</param>
/// <param name="Month">The month number (1–12) of the report period.</param>
/// <param name="StaffSummaries">Per-staff breakdown of monthly attendance counts.</param>
public record MonthlySummary(
    int Year,
    int Month,
    IReadOnlyList<StaffMonthlySummary> StaffSummaries
);

/// <summary>
/// Monthly attendance aggregates for a single staff member.
/// </summary>
/// <param name="StaffId">Staff member primary key.</param>
/// <param name="StaffName">Full name of the staff member.</param>
/// <param name="Department">Department name (denormalized for display).</param>
/// <param name="SlotSummaries">Per-slot counts of On Time, Late, and Absent events.</param>
public record StaffMonthlySummary(
    int StaffId,
    string StaffName,
    string Department,
    IReadOnlyList<SlotMonthlySummary> SlotSummaries
);

/// <summary>
/// Monthly On Time / Late / Absent counts for a single slot within a staff member's summary.
/// </summary>
/// <param name="SlotId">Slot configuration primary key.</param>
/// <param name="SlotName">Human-readable slot name.</param>
/// <param name="OnTimeCount">Number of days the staff member scanned on time this month.</param>
/// <param name="LateCount">Number of days the staff member scanned late this month.</param>
/// <param name="AbsentCount">
/// Number of working days the staff member had no log for this mandatory slot.
/// </param>
public record SlotMonthlySummary(
    int SlotId,
    string SlotName,
    int OnTimeCount,
    int LateCount,
    int AbsentCount
);
