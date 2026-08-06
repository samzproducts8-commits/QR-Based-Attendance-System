using Attendance.Application.DTOs;
using Attendance.Application.Enums;
using Attendance.Application.Exceptions;
using Attendance.Application.Helpers;
using Attendance.Application.Interfaces;
using Attendance.Application.Models;

namespace Attendance.Application.Services;

/// <summary>
/// Records attendance events (slot resolution + grace-period status) and
/// builds daily/monthly reports and their Excel/PDF exports.
/// </summary>
/// <remarks>
/// Satisfies Requirements 3.4, 3.9–3.12 and 4.1–4.5.
/// </remarks>
public sealed class AttendanceService : IAttendanceService
{
    private const string AbsentLabel      = "Absent";
    private const string NotRecordedLabel = "Not Recorded";

    private readonly IAttendanceRepository _repository;
    private readonly IReportExporter _exporter;
    private readonly IAttendanceHubContext? _hubContext;

    public AttendanceService(
        IAttendanceRepository repository,
        IReportExporter exporter,
        IAttendanceHubContext? hubContext = null)
    {
        _repository = repository;
        _exporter   = exporter;
        _hubContext = hubContext;
    }

    // -------------------------------------------------------------------------
    // Attendance recording (Algorithm 2 in the design document)
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    /// <remarks>
    /// Satisfies Requirements 3.4, 3.9, 3.10, 3.11, 3.12.
    /// Algorithm:
    /// <list type="number">
    ///   <item>Resolve the open slot from the current UTC time (Requirement 3.9).</item>
    ///   <item>Reject when no slot window is open — <see cref="OutsideScheduleException"/> → 422 (Requirement 3.11).</item>
    ///   <item>Friendly duplicate pre-check — <see cref="DuplicateAttendanceException"/> → 409 (Requirement 3.10);
    ///         the DB unique constraint on (StaffId, SlotId, EventDate) is the true guard for races (Requirement 7.3).</item>
    ///   <item>Compute OnTime/Late from the slot's EndTime — a scan in the grace tail is Late (Requirement 3.9).</item>
    ///   <item>Insert the log and return the greeting DTO (Requirement 3.12).</item>
    /// </list>
    /// </remarks>
    public async Task<AttendanceRecordDto> RecordAttendanceAsync(int staffId, int qrSessionId)
    {
        // Office-local time: slot windows are stored as office wall-clock hours,
        // so resolution, the event date, and the timestamp must all use it.
        DateTime now         = DateTimeHelper.OfficeNow();
        DateOnly today       = DateOnly.FromDateTime(now);
        TimeOnly currentTime = TimeOnly.FromDateTime(now);

        IReadOnlyList<SlotWindow> activeSlots = await _repository.GetActiveSlotWindowsAsync();

        SlotWindow? slot = SlotResolver.ResolveSlotForTime(currentTime, activeSlots);
        if (slot is null)
            throw new OutsideScheduleException("No attendance slot is open at this time.");

        if (await _repository.LogExistsAsync(staffId, slot.SlotId, today))
            throw new DuplicateAttendanceException("You have already checked in for this slot today.");

        StaffSnapshot? staff = await _repository.GetStaffSnapshotAsync(staffId)
            ?? throw new NotFoundException($"Staff member {staffId} was not found.");

        AttendanceStatus statusFlag = DateTimeHelper.ComputeStatusFlag(currentTime, slot);

        // The repository translates a unique-constraint violation (concurrent
        // duplicate) into DuplicateAttendanceException (Error Scenario 7).
        await _repository.InsertLogAsync(staffId, slot.SlotId, qrSessionId, now, today, statusFlag);

        string statusLabel = StatusLabelOf(statusFlag);
        string greeting    = FormatGreeting(staff.FullName, slot.SlotName, now, statusLabel);

        _ = NotifyLiveDashboardAsync();

        return new AttendanceRecordDto(staff.FullName, slot.SlotName, now, statusLabel, greeting);
    }

    // -------------------------------------------------------------------------
    // Daily reports (Requirements 4.1, 4.5)
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task<DailyAttendanceSheet> GetDailySheetAsync(int staffId, DateOnly date)
    {
        StaffSnapshot? staff = await _repository.GetStaffSnapshotAsync(staffId)
            ?? throw new NotFoundException($"Staff member {staffId} was not found.");

        IReadOnlyList<SlotWindow> slots = await _repository.GetActiveSlotWindowsAsync();
        IReadOnlyList<AttendanceLogEntry> logs =
            await _repository.GetLogsForStaffDateAsync(staffId, date);

        return BuildSheet(staff, date, slots, logs);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DailyAttendanceSheet>> GetDailySheetsAsync(DateOnly date)
    {
        IReadOnlyList<StaffSnapshot> staff = await _repository.GetActiveStaffAsync();
        IReadOnlyList<SlotWindow> slots    = await _repository.GetActiveSlotWindowsAsync();
        IReadOnlyList<AttendanceLogEntry> logs =
            await _repository.GetLogsForRangeAsync(date, date);

        var logsByStaff = logs.ToLookup(l => l.StaffId);

        return staff
            .Select(s => BuildSheet(s, date, slots, logsByStaff[s.StaffId].ToList()))
            .ToList();
    }

    /// <summary>
    /// Assembles one staff member's daily sheet: one entry per active slot,
    /// with mandatory slots lacking a log marked <c>Absent</c>
    /// (Requirement 4.5 / Property 8).
    /// </summary>
    private static DailyAttendanceSheet BuildSheet(
        StaffSnapshot staff,
        DateOnly date,
        IReadOnlyList<SlotWindow> slots,
        IReadOnlyList<AttendanceLogEntry> logs)
    {
        var logsBySlot = logs.ToDictionary(l => l.SlotId);

        var entries = slots
            .OrderBy(s => s.StartTime)
            .Select(slot =>
            {
                if (logsBySlot.TryGetValue(slot.SlotId, out var log))
                {
                    return new DailySlotEntry(
                        slot.SlotId,
                        slot.SlotName,
                        log.StatusFlag == AttendanceStatus.Absent ? null : log.EventTimestamp,
                        StatusLabelOf(log.StatusFlag),
                        log.AbsenceReason);
                }

                return new DailySlotEntry(
                    slot.SlotId,
                    slot.SlotName,
                    null,
                    slot.IsMandatory ? AbsentLabel : NotRecordedLabel,
                    null);
            })
            .ToList();

        return new DailyAttendanceSheet(staff.StaffId, staff.FullName, date, entries);
    }

    // -------------------------------------------------------------------------
    // Absence Reasons
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task SetAbsenceReasonAsync(SetAbsenceReasonDto dto)
    {
        StaffSnapshot? staff = await _repository.GetStaffSnapshotAsync(dto.StaffId)
            ?? throw new NotFoundException($"Staff member {dto.StaffId} was not found.");

        IReadOnlyList<SlotWindow> slots = await _repository.GetActiveSlotWindowsAsync();
        SlotWindow? slot = slots.FirstOrDefault(s => s.SlotId == dto.SlotId);

        if (slot is null)
            throw new NotFoundException($"Attendance slot {dto.SlotId} was not found or is not active.");

        if (!slot.IsMandatory)
            throw new BusinessRuleException($"Absence reasons can only be documented for mandatory slots.");

        if (string.IsNullOrWhiteSpace(dto.Reason))
            throw new BusinessRuleException("Absence reason cannot be empty.");

        await _repository.SetAbsenceReasonAsync(dto.StaffId, dto.SlotId, dto.Date, dto.Reason.Trim());
        _ = NotifyLiveDashboardAsync();
    }

    // -------------------------------------------------------------------------
    // Monthly summary (Requirements 4.2, 4.4, 4.5)
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    /// <remarks>
    /// Absent counting rule: for each <em>mandatory</em> slot, the absent count
    /// is the number of working days (Mon–Fri) in the month — capped at today
    /// for the current month — on which the staff member has no log for that
    /// slot.  Optional slots never contribute absences.
    /// </remarks>
    public async Task<MonthlySummary> GetMonthlySummaryAsync(
        int? staffId, int? departmentId, int year, int month)
    {
        var firstDay = new DateOnly(year, month, 1);
        var lastDay  = new DateOnly(year, month, DateTime.DaysInMonth(year, month));

        IReadOnlyList<StaffSnapshot> staffList =
            await _repository.GetActiveStaffAsync(staffId, departmentId);
        IReadOnlyList<SlotWindow> slots = await _repository.GetActiveSlotWindowsAsync();
        IReadOnlyList<AttendanceLogEntry> logs =
            await _repository.GetLogsForRangeAsync(firstDay, lastDay, staffId, departmentId);

        int workingDays = CountWorkingDays(firstDay, lastDay);

        var logsByStaff = logs.ToLookup(l => l.StaffId);

        var staffSummaries = staffList.Select(s =>
        {
            var staffLogs = logsByStaff[s.StaffId].ToLookup(l => l.SlotId);

            var slotSummaries = slots
                .OrderBy(slot => slot.StartTime)
                .Select(slot =>
                {
                    var slotLogs = staffLogs[slot.SlotId].ToList();
                    // ManualEntry counts as an on-time presence for summary purposes.
                    int onTime = slotLogs.Count(l => l.StatusFlag == AttendanceStatus.OnTime || l.StatusFlag == AttendanceStatus.ManualEntry);
                    int late   = slotLogs.Count(l => l.StatusFlag == AttendanceStatus.Late);
                    int daysWithPresenceLog = slotLogs.Where(l => l.StatusFlag != AttendanceStatus.Absent).Select(l => l.EventDate).Distinct().Count();
                    int totalAbsent = slot.IsMandatory ? Math.Max(0, workingDays - daysWithPresenceLog) : 0;

                    // Excused = absent logs with an admin-provided reason
                    int excusedAbsent = slot.IsMandatory
                        ? slotLogs.Count(l => l.StatusFlag == AttendanceStatus.Absent && !string.IsNullOrWhiteSpace(l.AbsenceReason))
                        : 0;
                    int unexcusedAbsent = Math.Max(0, totalAbsent - excusedAbsent);

                    return new SlotMonthlySummary(slot.SlotId, slot.SlotName, onTime, late, excusedAbsent, unexcusedAbsent);
                })
                .ToList();

            return new StaffMonthlySummary(s.StaffId, s.FullName, s.DepartmentName, slotSummaries);
        }).ToList();

        return new MonthlySummary(year, month, staffSummaries);
    }

    /// <summary>
    /// Counts Mon–Fri days in the inclusive range, never counting days after
    /// today (a month still in progress only penalises elapsed days).
    /// </summary>
    private static int CountWorkingDays(DateOnly from, DateOnly to)
    {
        DateOnly today = DateOnly.FromDateTime(DateTimeHelper.OfficeNow());
        if (to > today) to = today;

        int count = 0;
        for (DateOnly d = from; d <= to; d = d.AddDays(1))
        {
            if (d.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                count++;
        }
        return count;
    }

    // -------------------------------------------------------------------------
    // Exports (Requirement 4.3)
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task<byte[]> ExportDailyReportAsync(DateOnly date, ExportFormat format)
    {
        IReadOnlyList<DailyAttendanceSheet> sheets = await GetDailySheetsAsync(date);
        return _exporter.ExportDaily(sheets, date, format);
    }

    /// <inheritdoc/>
    public async Task<byte[]> ExportMonthlyReportAsync(int year, int month, ExportFormat format)
    {
        MonthlySummary summary = await GetMonthlySummaryAsync(null, null, year, month);
        return _exporter.ExportMonthly(summary, format);
    }

    // -------------------------------------------------------------------------
    // Employee history (Requirement 5.5)
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AttendanceHistoryEntry>> GetHistoryAsync(
        int staffId, DateOnly? fromDate = null, DateOnly? toDate = null)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        DateOnly from  = fromDate ?? today.AddDays(-30);
        DateOnly to    = toDate ?? today;

        IReadOnlyList<SlotWindow> slots = await _repository.GetActiveSlotWindowsAsync();
        var slotNames = slots.ToDictionary(s => s.SlotId, s => s.SlotName);

        IReadOnlyList<AttendanceLogEntry> logs =
            await _repository.GetLogsForRangeAsync(from, to, staffId);

        return logs
            .OrderByDescending(l => l.EventTimestamp)
            .Select(l => new AttendanceHistoryEntry(
                l.AttendanceLogId,
                l.EventDate,
                slotNames.TryGetValue(l.SlotId, out var name) ? name : $"Slot {l.SlotId}",
                l.EventTimestamp,
                StatusLabelOf(l.StatusFlag)))
            .ToList();
    }

    // -------------------------------------------------------------------------
    // Formatting helpers
    // -------------------------------------------------------------------------

    private static string StatusLabelOf(AttendanceStatus status) => status switch
    {
        AttendanceStatus.OnTime      => "On Time",
        AttendanceStatus.Late        => "Late",
        AttendanceStatus.ManualEntry => "Manual Entry",
        AttendanceStatus.Absent      => "Absent",
        _                            => status.ToString()
    };

    /// <summary>
    /// Builds the personalised confirmation shown on the employee's phone
    /// (Requirement 3.12), e.g.
    /// <c>"Good morning, John — MorningIn recorded at 08:03 AM. On Time!"</c>.
    /// </summary>
    private static string FormatGreeting(
        string staffName, string slotName, DateTime timestamp, string statusLabel)
    {
        string salutation = slotName switch
        {
            "MorningIn"  => "Good morning",
            "LunchOut"   => "Enjoy your lunch",
            "LunchIn"    => "Welcome back",
            "EveningOut" => "Good evening",
            _            => "Hello"
        };

        return $"{salutation}, {staffName} — {slotName} recorded at {timestamp:hh:mm tt}. {statusLabel}!";
    }

    // -------------------------------------------------------------------------
    // Real-time Live Dashboard & Payroll
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task<LiveDashboardMetricsDto> GetLiveDashboardMetricsAsync()
    {
        DateOnly today = DateOnly.FromDateTime(DateTimeHelper.OfficeNow());

        IReadOnlyList<StaffSnapshot> activeStaff = await _repository.GetActiveStaffAsync();
        IReadOnlyList<SlotWindow> slots          = await _repository.GetActiveSlotWindowsAsync();
        IReadOnlyList<AttendanceLogEntry> logs   = await _repository.GetLogsForRangeAsync(today, today);
        IReadOnlyList<DailyAttendanceSheet> sheets = await GetDailySheetsAsync(today);

        var slotNames = slots.ToDictionary(s => s.SlotId, s => s.SlotName);
        var staffNames = activeStaff.ToDictionary(s => s.StaffId, s => s.FullName);

        int totalActiveStaff = activeStaff.Count;
        int totalActiveCheckIns = logs
            .Where(l => l.StatusFlag != AttendanceStatus.Absent)
            .Select(l => l.StaffId)
            .Distinct()
            .Count();

        int lateArrivals = logs
            .Where(l => l.StatusFlag == AttendanceStatus.Late)
            .Select(l => l.StaffId)
            .Distinct()
            .Count();

        int onLeaveEmployees = logs
            .Where(l => l.StatusFlag == AttendanceStatus.Absent && !string.IsNullOrWhiteSpace(l.AbsenceReason))
            .Select(l => l.StaffId)
            .Distinct()
            .Count();

        int unexcusedAbsences = sheets
            .SelectMany(s => s.Entries)
            .Count(e => e.StatusLabel == AbsentLabel && string.IsNullOrWhiteSpace(e.AbsenceReason));

        var recentActivities = logs
            .Where(l => l.StatusFlag != AttendanceStatus.Absent)
            .OrderByDescending(l => l.EventTimestamp)
            .Take(15)
            .Select(l => new RecentActivityDto(
                l.AttendanceLogId,
                l.StaffId,
                staffNames.TryGetValue(l.StaffId, out var name) ? name : $"Staff {l.StaffId}",
                slotNames.TryGetValue(l.SlotId, out var sName) ? sName : $"Slot {l.SlotId}",
                l.EventTimestamp,
                StatusLabelOf(l.StatusFlag)))
            .ToList();

        return new LiveDashboardMetricsDto(
            today,
            totalActiveStaff,
            totalActiveCheckIns,
            lateArrivals,
            onLeaveEmployees,
            unexcusedAbsences,
            recentActivities);
    }

    /// <inheritdoc/>
    public async Task<MonthlyPayrollSummaryDto> GetMonthlyPayrollSummaryAsync(
        int year, int month, int? departmentId = null)
    {
        var firstDay = new DateOnly(year, month, 1);
        var lastDay  = new DateOnly(year, month, DateTime.DaysInMonth(year, month));

        IReadOnlyList<StaffSnapshot> staffList =
            await _repository.GetActiveStaffAsync(null, departmentId);
        IReadOnlyList<SlotWindow> slots = await _repository.GetActiveSlotWindowsAsync();
        IReadOnlyList<AttendanceLogEntry> logs =
            await _repository.GetLogsForRangeAsync(firstDay, lastDay, null, departmentId);

        int workingDays = CountWorkingDays(firstDay, lastDay);
        var logsByStaff = logs.ToLookup(l => l.StaffId);

        var staffSummaries = staffList.Select(s =>
        {
            var staffLogs = logsByStaff[s.StaffId].ToList();
            var logsBySlot = staffLogs.ToLookup(l => l.SlotId);

            // Days with a physical presence (scan or manual entry)
            var physicalPresenceDates = staffLogs
                .Where(l => l.StatusFlag != AttendanceStatus.Absent)
                .Select(l => l.EventDate)
                .Distinct()
                .ToHashSet();

            // Days covered by an excused (admin-approved) absence reason
            var excusedAbsenceDates = staffLogs
                .Where(l => l.StatusFlag == AttendanceStatus.Absent && !string.IsNullOrWhiteSpace(l.AbsenceReason))
                .Select(l => l.EventDate)
                .Distinct()
                .ToHashSet();

            // Excused days that had no physical presence count toward daysWorked
            int daysWorked = physicalPresenceDates.Count + excusedAbsenceDates.Except(physicalPresenceDates).Count();

            // Hours from physical presence logs
            decimal totalHours = 0m;
            var workedLogs = staffLogs.Where(l => l.StatusFlag != AttendanceStatus.Absent).ToList();
            foreach (var log in workedLogs)
            {
                var slot = slots.FirstOrDefault(sl => sl.SlotId == log.SlotId);
                if (slot is not null)
                {
                    double slotHours = (slot.EndTime - slot.StartTime).TotalHours;
                    if (slotHours < 0) slotHours += 24;
                    totalHours += (decimal)slotHours;
                }
            }

            // Credit slot hours for excused absences (approved leave gets full slot hours)
            var excusedLogs = staffLogs
                .Where(l => l.StatusFlag == AttendanceStatus.Absent && !string.IsNullOrWhiteSpace(l.AbsenceReason))
                .ToList();
            foreach (var log in excusedLogs)
            {
                var slot = slots.FirstOrDefault(sl => sl.SlotId == log.SlotId);
                if (slot is not null)
                {
                    double slotHours = (slot.EndTime - slot.StartTime).TotalHours;
                    if (slotHours < 0) slotHours += 24;
                    totalHours += (decimal)slotHours;
                }
            }
            totalHours = Math.Round(totalHours, 2);

            // Overtime calculated from physical presence only (excused leave doesn't generate overtime)
            var logsByDay = workedLogs.GroupBy(l => l.EventDate);
            decimal overtimeHours = 0m;
            foreach (var dayGroup in logsByDay)
            {
                decimal dayHours = 0m;
                foreach (var log in dayGroup)
                {
                    var slot = slots.FirstOrDefault(sl => sl.SlotId == log.SlotId);
                    if (slot is not null)
                    {
                        double sHours = (slot.EndTime - slot.StartTime).TotalHours;
                        if (sHours < 0) sHours += 24;
                        dayHours += (decimal)sHours;
                    }
                }
                if (dayHours > 8.0m)
                {
                    overtimeHours += (dayHours - 8.0m);
                }
            }
            overtimeHours = Math.Round(overtimeHours, 2);

            int latePenalties = staffLogs.Count(l => l.StatusFlag == AttendanceStatus.Late);

            int excusedAbsences = 0;
            int unpaidAbsences = 0;
            foreach (var slot in slots.Where(sl => sl.IsMandatory))
            {
                var slotLogs = logsBySlot[slot.SlotId].ToList();
                int presenceCount = slotLogs.Count(l => l.StatusFlag != AttendanceStatus.Absent);
                int excusedCount  = slotLogs.Count(l => l.StatusFlag == AttendanceStatus.Absent && !string.IsNullOrWhiteSpace(l.AbsenceReason));
                int missingCount  = Math.Max(0, workingDays - presenceCount);
                excusedAbsences += excusedCount;
                unpaidAbsences += Math.Max(0, missingCount - excusedCount);
            }

            return new StaffPayrollSummaryDto(
                s.StaffId,
                s.UniqueCode,
                s.FullName,
                s.DepartmentName,
                daysWorked,
                totalHours,
                overtimeHours,
                latePenalties,
                excusedAbsences,
                unpaidAbsences);
        }).ToList();

        int totalStaff = staffSummaries.Count;
        int sumDaysWorked = staffSummaries.Sum(s => s.TotalDaysWorked);
        decimal sumHoursWorked = staffSummaries.Sum(s => s.TotalHours);
        decimal sumOvertimeHours = staffSummaries.Sum(s => s.OvertimeHours);
        int sumLatePenalties = staffSummaries.Sum(s => s.LatePenalties);
        int sumExcusedAbsences = staffSummaries.Sum(s => s.ExcusedAbsences);
        int sumUnpaidAbsences = staffSummaries.Sum(s => s.UnpaidAbsences);

        return new MonthlyPayrollSummaryDto(
            year,
            month,
            totalStaff,
            sumDaysWorked,
            sumHoursWorked,
            sumOvertimeHours,
            sumLatePenalties,
            sumExcusedAbsences,
            sumUnpaidAbsences,
            staffSummaries);
    }

    /// <inheritdoc/>
    public async Task<byte[]> ExportPayrollSummaryAsync(
        int year, int month, ExportFormat format, int? departmentId = null)
    {
        MonthlyPayrollSummaryDto summary = await GetMonthlyPayrollSummaryAsync(year, month, departmentId);
        return _exporter.ExportPayroll(summary, format);
    }

    private async Task NotifyLiveDashboardAsync()
    {
        if (_hubContext is null) return;
        try
        {
            LiveDashboardMetricsDto metrics = await GetLiveDashboardMetricsAsync();
            await _hubContext.NotifyLiveDashboardUpdatedAsync(metrics);
        }
        catch
        {
            // Background push failure should not impact core API workflows
        }
    }
}
