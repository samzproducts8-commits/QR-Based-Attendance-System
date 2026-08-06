namespace Attendance.Application.DTOs;

/// <summary>
/// Payroll summary aggregates for a single staff member for a specific month.
/// </summary>
/// <param name="StaffId">Staff member primary key.</param>
/// <param name="UniqueCode">Unique employee code (e.g. EMP-001).</param>
/// <param name="FullName">Staff member full name.</param>
/// <param name="Department">Department name.</param>
/// <param name="TotalDaysWorked">Number of distinct working days the employee attended.</param>
/// <param name="TotalHours">Total accrued work hours in the month.</param>
/// <param name="OvertimeHours">Calculated overtime hours accrued in the month.</param>
/// <param name="LatePenalties">Number of late arrival occurrences in the month.</param>
/// <param name="ExcusedAbsences">Number of admin-approved absences for mandatory slots (no payroll deduction).</param>
/// <param name="UnpaidAbsences">Number of unexcused absences for mandatory slots in the month.</param>
public record StaffPayrollSummaryDto(
    int StaffId,
    string UniqueCode,
    string FullName,
    string Department,
    int TotalDaysWorked,
    decimal TotalHours,
    decimal OvertimeHours,
    int LatePenalties,
    int ExcusedAbsences,
    int UnpaidAbsences
);

/// <summary>
/// Monthly aggregated payroll summary for standard payroll processing export.
/// </summary>
/// <param name="Year">Four-digit year of the report.</param>
/// <param name="Month">Month (1–12) of the report.</param>
/// <param name="TotalStaff">Total staff count included in summary.</param>
/// <param name="TotalDaysWorked">Aggregate days worked across all staff.</param>
/// <param name="TotalHoursWorked">Aggregate total hours worked across all staff.</param>
/// <param name="TotalOvertimeHours">Aggregate overtime hours across all staff.</param>
/// <param name="TotalLatePenalties">Aggregate count of late arrival penalties across all staff.</param>
/// <param name="TotalExcusedAbsences">Aggregate count of admin-approved absences across all staff.</param>
/// <param name="TotalUnpaidAbsences">Aggregate count of unpaid absences across all staff.</param>
/// <param name="StaffSummaries">Per-staff payroll records.</param>
public record MonthlyPayrollSummaryDto(
    int Year,
    int Month,
    int TotalStaff,
    int TotalDaysWorked,
    decimal TotalHoursWorked,
    decimal TotalOvertimeHours,
    int TotalLatePenalties,
    int TotalExcusedAbsences,
    int TotalUnpaidAbsences,
    IReadOnlyList<StaffPayrollSummaryDto> StaffSummaries
);

/// <summary>
/// Real-time live dashboard metrics for the current day.
/// </summary>
/// <param name="Date">Today's office date.</param>
/// <param name="TotalActiveStaff">Total active staff members in the system.</param>
/// <param name="TotalActiveCheckIns">Total staff members checked in today.</param>
/// <param name="LateArrivals">Total staff members who arrived late today.</param>
/// <param name="OnLeaveEmployees">Total staff members with an excused absence / on leave today.</param>
/// <param name="UnexcusedAbsences">Total unexcused mandatory slot absences today.</param>
/// <param name="RecentActivities">Recent attendance scan activities recorded today.</param>
public record LiveDashboardMetricsDto(
    DateOnly Date,
    int TotalActiveStaff,
    int TotalActiveCheckIns,
    int LateArrivals,
    int OnLeaveEmployees,
    int UnexcusedAbsences,
    IReadOnlyList<RecentActivityDto> RecentActivities
);

/// <summary>
/// Lightweight DTO for real-time live activity stream logs on the dashboard.
/// </summary>
public record RecentActivityDto(
    long AttendanceLogId,
    int StaffId,
    string StaffName,
    string SlotName,
    DateTime EventTimestamp,
    string StatusLabel
);
