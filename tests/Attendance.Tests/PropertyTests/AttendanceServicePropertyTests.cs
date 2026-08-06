using Attendance.Application.DTOs;
using Attendance.Application.Enums;
using Attendance.Application.Exceptions;
using Attendance.Application.Helpers;
using Attendance.Application.Interfaces;
using Attendance.Application.Models;
using Attendance.Application.Services;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Moq;

namespace Attendance.Tests.PropertyTests;

// ---------------------------------------------------------------------------
// Shared builder
// ---------------------------------------------------------------------------

internal static class AttendanceServiceBuilder
{
    public static (AttendanceService Service, Mock<IAttendanceRepository> RepoMock)
        Build(Action<Mock<IAttendanceRepository>>? configure = null)
    {
        var repoMock     = new Mock<IAttendanceRepository>(MockBehavior.Strict);
        var exporterMock = new Mock<IReportExporter>(MockBehavior.Loose);

        configure?.Invoke(repoMock);

        return (new AttendanceService(repoMock.Object, exporterMock.Object), repoMock);
    }

    /// <summary>
    /// Builds a slot window that contains the current office-local time-of-day,
    /// starting <paramref name="minutesSinceStart"/> minutes ago and ending
    /// 30 minutes ahead.  The office offset must match the service, which
    /// resolves against <see cref="DateTimeHelper.OfficeNow"/>.
    /// Returns null when the window would wrap across midnight (caller skips).
    /// </summary>
    public static SlotWindow? SlotAroundNow(int minutesSinceStart, int gracePeriod, int slotId = 1)
    {
        TimeOnly now = TimeOnly.FromDateTime(DateTimeHelper.OfficeNow());
        int nowMin = now.Hour * 60 + now.Minute;

        int startMin = nowMin - minutesSinceStart;
        int endMin   = nowMin + 30;

        if (startMin < 0 || endMin > 1438)
            return null; // too close to midnight for a stable window

        return new SlotWindow(
            SlotId: slotId,
            SlotName: "MorningIn",
            StartTime: new TimeOnly(startMin / 60, startMin % 60),
            EndTime: new TimeOnly(endMin / 60, endMin % 60),
            GracePeriodMinutes: gracePeriod,
            IsMandatory: true,
            IsActive: true);
    }

    /// <summary>
    /// Builds a 30-minute slot window whose <c>EndTime</c> is
    /// <paramref name="minutesEndFromNow"/> minutes from the current
    /// office-local time (positive = EndTime in the future, so "now" is inside
    /// the window → On Time; negative = EndTime in the past, so "now" is in the
    /// grace tail → Late).  Returns null near midnight (caller skips).
    /// </summary>
    public static SlotWindow? SlotEndingRelativeToNow(int minutesEndFromNow, int gracePeriod, int slotId = 1)
    {
        TimeOnly now = TimeOnly.FromDateTime(DateTimeHelper.OfficeNow());
        int nowMin = now.Hour * 60 + now.Minute;

        int endMin   = nowMin + minutesEndFromNow;
        int startMin = endMin - 30;

        // Keep the whole window plus its grace tail comfortably within the day.
        if (startMin < 0 || endMin + gracePeriod > 1438)
            return null;

        return new SlotWindow(
            SlotId: slotId,
            SlotName: "MorningIn",
            StartTime: new TimeOnly(startMin / 60, startMin % 60),
            EndTime: new TimeOnly(endMin / 60, endMin % 60),
            GracePeriodMinutes: gracePeriod,
            IsMandatory: true,
            IsActive: true);
    }

    public static readonly StaffSnapshot DefaultStaff =
        new(42, "EMP-0042", "Test Person", 1, "Engineering", true);
}

// ---------------------------------------------------------------------------
// Property 6: Duplicate Attendance Rejection
// Validates: Requirements 3.10, 7.3
// ---------------------------------------------------------------------------

/// <summary>
/// <b>Property 6: Duplicate Attendance Rejection</b>
/// <para>
/// For any (staffId, qrSessionId), a second recording attempt for the same
/// slot and day must raise <see cref="DuplicateAttendanceException"/> —
/// whether caught by the friendly pre-check or by the database unique
/// constraint surfaced through the repository.
/// </para>
/// <b>Validates: Requirements 3.10, 7.3</b>
/// </summary>
public class AttendanceServiceProperty6_DuplicateRejection
{
    /// <summary>
    /// When the pre-check finds an existing log, the service throws
    /// DuplicateAttendanceException and never calls InsertLogAsync.
    /// <b>Validates: Requirement 3.10</b>
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Property6_PreCheckDuplicate_Throws(int staffSeed, int sessionSeed)
    {
        SlotWindow? slot = AttendanceServiceBuilder.SlotAroundNow(5, 10);
        if (slot is null)
            return Prop.ToProperty(true); // skip near midnight

        int staffId   = Math.Abs(staffSeed) % 10_000 + 1;
        int sessionId = Math.Abs(sessionSeed) % 10_000 + 1;
        var insertCalls = 0;

        var (service, _) = AttendanceServiceBuilder.Build(repo =>
        {
            repo.Setup(r => r.GetActiveSlotWindowsAsync())
                .ReturnsAsync([slot]);
            repo.Setup(r => r.LogExistsAsync(staffId, slot.SlotId, It.IsAny<DateOnly>()))
                .ReturnsAsync(true);   // already recorded today
            repo.Setup(r => r.InsertLogAsync(
                    It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(),
                    It.IsAny<DateTime>(), It.IsAny<DateOnly>(), It.IsAny<AttendanceStatus>()))
                .Callback(() => insertCalls++)
                .Returns(Task.CompletedTask);
        });

        Exception? thrown = Record.ExceptionAsync(
            () => service.RecordAttendanceAsync(staffId, sessionId)).GetAwaiter().GetResult();

        return Prop.Label(
            thrown is DuplicateAttendanceException && insertCalls == 0,
            $"Expected DuplicateAttendanceException with no insert; got {thrown?.GetType().Name ?? "no exception"}, inserts={insertCalls}");
    }

    /// <summary>
    /// Concurrent-race variant: the pre-check passes but the repository insert
    /// surfaces the DB unique-constraint violation; the exception propagates
    /// unchanged (Error Scenario 7).
    /// <b>Validates: Requirement 7.3</b>
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Property6_ConcurrentDuplicate_InsertThrows(int staffSeed)
    {
        SlotWindow? slot = AttendanceServiceBuilder.SlotAroundNow(5, 10);
        if (slot is null)
            return Prop.ToProperty(true);

        int staffId = Math.Abs(staffSeed) % 10_000 + 1;

        var (service, _) = AttendanceServiceBuilder.Build(repo =>
        {
            repo.Setup(r => r.GetActiveSlotWindowsAsync())
                .ReturnsAsync([slot]);
            repo.Setup(r => r.LogExistsAsync(staffId, slot.SlotId, It.IsAny<DateOnly>()))
                .ReturnsAsync(false);  // race: not visible yet
            repo.Setup(r => r.GetStaffSnapshotAsync(staffId))
                .ReturnsAsync(AttendanceServiceBuilder.DefaultStaff with { StaffId = staffId });
            repo.Setup(r => r.InsertLogAsync(
                    staffId, slot.SlotId, It.IsAny<int?>(),
                    It.IsAny<DateTime>(), It.IsAny<DateOnly>(), It.IsAny<AttendanceStatus>()))
                .ThrowsAsync(new DuplicateAttendanceException(
                    "You have already checked in for this slot today."));
        });

        Exception? thrown = Record.ExceptionAsync(
            () => service.RecordAttendanceAsync(staffId, 1)).GetAwaiter().GetResult();

        return Prop.Label(
            thrown is DuplicateAttendanceException,
            $"Expected DuplicateAttendanceException from insert; got {thrown?.GetType().Name ?? "no exception"}");
    }

    /// <summary>
    /// When no active slot covers the current time, the service throws
    /// OutsideScheduleException (mapped to HTTP 422).
    /// <b>Validates: Requirement 3.11</b>
    /// </summary>
    [Property(MaxTest = 50)]
    public Property Property6_NoOpenSlot_ThrowsOutsideSchedule(int staffSeed)
    {
        int staffId = Math.Abs(staffSeed) % 10_000 + 1;

        var (service, _) = AttendanceServiceBuilder.Build(repo =>
            repo.Setup(r => r.GetActiveSlotWindowsAsync())
                .ReturnsAsync([]));   // no slots open

        Exception? thrown = Record.ExceptionAsync(
            () => service.RecordAttendanceAsync(staffId, 1)).GetAwaiter().GetResult();

        return Prop.Label(
            thrown is OutsideScheduleException,
            $"Expected OutsideScheduleException; got {thrown?.GetType().Name ?? "no exception"}");
    }
}

// ---------------------------------------------------------------------------
// Property 3 (integration): grace-period boundary through RecordAttendanceAsync
// Validates: Requirements 3.9, 3.4
// ---------------------------------------------------------------------------

/// <summary>
/// <b>Property 3 (integration):</b> the StatusLabel produced by
/// <see cref="AttendanceService.RecordAttendanceAsync"/> honours the
/// end-of-window boundary: a scan inside the normal window is "On Time", a
/// scan in the grace tail (after EndTime, but within EndTime + grace) is still
/// accepted and reported "Late".
/// <b>Validates: Requirements 3.9, 3.4, 3.11</b>
/// </summary>
public class AttendanceServiceProperty3_GraceBoundaryIntegration
{
    [Property(MaxTest = 100)]
    public Property Property3_StatusMatchesEndTimeBoundary(int seed)
    {
        // Alternate between an inside-window scan and a grace-tail scan.
        bool tailScan = (Math.Abs(seed) % 2) == 1;

        // Inside window: EndTime 6 min ahead → "now" is comfortably before it.
        // Grace tail:    EndTime 4 min behind with a 15-min tail → "now" is 4 min
        //                past EndTime, still accepted (4 ≤ 15) and marked Late.
        // Both margins clear the few-seconds drift between building the slot and
        // recording, so the outcome is never ambiguous.
        int minutesEndFromNow = tailScan ? -4 : 6;
        int grace             = 15;

        SlotWindow? slot = AttendanceServiceBuilder.SlotEndingRelativeToNow(minutesEndFromNow, grace);
        if (slot is null)
            return Prop.ToProperty(true); // skip near midnight

        var (service, _) = AttendanceServiceBuilder.Build(repo =>
        {
            repo.Setup(r => r.GetActiveSlotWindowsAsync()).ReturnsAsync([slot]);
            repo.Setup(r => r.LogExistsAsync(It.IsAny<int>(), slot.SlotId, It.IsAny<DateOnly>()))
                .ReturnsAsync(false);
            repo.Setup(r => r.GetStaffSnapshotAsync(It.IsAny<int>()))
                .ReturnsAsync(AttendanceServiceBuilder.DefaultStaff);
            repo.Setup(r => r.InsertLogAsync(
                    It.IsAny<int>(), slot.SlotId, It.IsAny<int?>(),
                    It.IsAny<DateTime>(), It.IsAny<DateOnly>(), It.IsAny<AttendanceStatus>()))
                .Returns(Task.CompletedTask);
        });

        AttendanceRecordDto result =
            service.RecordAttendanceAsync(42, 1).GetAwaiter().GetResult();

        string expectedLabel = tailScan ? "Late" : "On Time";

        return Prop.Label(
            result.StatusLabel == expectedLabel,
            $"tailScan={tailScan} minutesEndFromNow={minutesEndFromNow} grace={grace} | " +
            $"Got '{result.StatusLabel}' (expected '{expectedLabel}')");
    }
}

// ---------------------------------------------------------------------------
// Property 8: Report Completeness
// Validates: Requirements 4.1, 4.2, 4.5
// ---------------------------------------------------------------------------

/// <summary>
/// <b>Property 8: Report Completeness</b>
/// <para>
/// For any set of active slots and any subset of them having logs, the daily
/// sheet contains exactly one entry per active slot; every mandatory slot
/// without a log is labelled "Absent"; every slot with a log carries its
/// recorded status.
/// </para>
/// <b>Validates: Requirements 4.1, 4.2, 4.5</b>
/// </summary>
public class AttendanceServiceProperty8_ReportCompleteness
{
    private static List<SlotWindow> MakeSlots(int count, bool[] mandatoryFlags)
    {
        var slots = new List<SlotWindow>();
        for (int i = 0; i < count; i++)
        {
            int startMin = 8 * 60 + i * 90; // 08:00, 09:30, 11:00, …
            slots.Add(new SlotWindow(
                SlotId: i + 1,
                SlotName: $"Slot{i + 1}",
                StartTime: new TimeOnly(startMin / 60, startMin % 60),
                EndTime: new TimeOnly((startMin + 60) / 60, (startMin + 60) % 60),
                GracePeriodMinutes: 10,
                IsMandatory: mandatoryFlags[i],
                IsActive: true));
        }
        return slots;
    }

    [Property(MaxTest = 200)]
    public Property Property8_DailySheet_OneEntryPerSlot_AbsentForMissingMandatory(
        int slotCountSeed, int loggedMaskSeed, int mandatoryMaskSeed)
    {
        int slotCount     = Math.Abs(slotCountSeed) % 6 + 1;      // 1..6 slots
        int loggedMask    = Math.Abs(loggedMaskSeed);
        int mandatoryMask = Math.Abs(mandatoryMaskSeed);

        bool[] mandatory = Enumerable.Range(0, slotCount)
            .Select(i => (mandatoryMask >> i & 1) == 1).ToArray();
        bool[] hasLog = Enumerable.Range(0, slotCount)
            .Select(i => (loggedMask >> i & 1) == 1).ToArray();

        List<SlotWindow> slots = MakeSlots(slotCount, mandatory);
        var date = new DateOnly(2026, 7, 6);

        List<AttendanceLogEntry> logs = slots
            .Where((_, i) => hasLog[i])
            .Select(s => new AttendanceLogEntry(
                AttendanceLogId: s.SlotId,
                StaffId: 42,
                SlotId: s.SlotId,
                EventTimestamp: new DateTime(2026, 7, 6, s.StartTime.Hour, s.StartTime.Minute, 0, DateTimeKind.Utc),
                EventDate: date,
                StatusFlag: s.SlotId % 2 == 0 ? AttendanceStatus.Late : AttendanceStatus.OnTime,
                AbsenceReason: null))
            .ToList();

        var (service, _) = AttendanceServiceBuilder.Build(repo =>
        {
            repo.Setup(r => r.GetStaffSnapshotAsync(42))
                .ReturnsAsync(AttendanceServiceBuilder.DefaultStaff);
            repo.Setup(r => r.GetActiveSlotWindowsAsync()).ReturnsAsync(slots);
            repo.Setup(r => r.GetLogsForStaffDateAsync(42, date)).ReturnsAsync(logs);
        });

        DailyAttendanceSheet sheet =
            service.GetDailySheetAsync(42, date).GetAwaiter().GetResult();

        // (a) exactly one entry per active slot
        bool completeCount = sheet.Entries.Count == slotCount
            && sheet.Entries.Select(e => e.SlotId).Distinct().Count() == slotCount;

        // (b) every mandatory slot without a log is Absent; optional ones are not
        bool absentCorrect = slots.Where((_, i) => !hasLog[i]).All(s =>
        {
            DailySlotEntry entry = sheet.Entries.Single(e => e.SlotId == s.SlotId);
            return s.IsMandatory
                ? entry.StatusLabel == "Absent" && entry.EventTimestamp is null
                : entry.StatusLabel != "Absent" && entry.EventTimestamp is null;
        });

        // (c) every logged slot carries its recorded status and timestamp
        bool loggedCorrect = slots.Where((_, i) => hasLog[i]).All(s =>
        {
            DailySlotEntry entry = sheet.Entries.Single(e => e.SlotId == s.SlotId);
            string expected = s.SlotId % 2 == 0 ? "Late" : "On Time";
            return entry.StatusLabel == expected && entry.EventTimestamp is not null;
        });

        return Prop.Label(
            completeCount && absentCorrect && loggedCorrect,
            $"slots={slotCount} loggedMask={Convert.ToString(loggedMask & ((1 << slotCount) - 1), 2)} | " +
            $"entries={sheet.Entries.Count} completeCount={completeCount} " +
            $"absentCorrect={absentCorrect} loggedCorrect={loggedCorrect}");
    }

    /// <summary>
    /// Monthly summary consistency: for a staff member whose logs are all
    /// generated within the month, each slot's OnTime + Late equals the
    /// number of logged days for that slot.
    /// <b>Validates: Requirement 4.2</b>
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Property8_MonthlyCounts_MatchGeneratedLogs(int daysSeed)
    {
        int loggedDays = Math.Abs(daysSeed) % 15 + 1;   // 1..15 logged days (past month)

        List<SlotWindow> slots = MakeSlots(2, [true, true]);
        int year = 2026, month = 6; // fully in the past — working-day cap not triggered

        var logs = new List<AttendanceLogEntry>();
        long id = 1;
        foreach (SlotWindow slot in slots)
        {
            for (int d = 1; d <= loggedDays; d++)
            {
                logs.Add(new AttendanceLogEntry(
                    id++, 42, slot.SlotId,
                    new DateTime(year, month, d, slot.StartTime.Hour, slot.StartTime.Minute, 0, DateTimeKind.Utc),
                    new DateOnly(year, month, d),
                    d % 3 == 0 ? AttendanceStatus.Late : AttendanceStatus.OnTime,
                    null));
            }
        }

        var (service, _) = AttendanceServiceBuilder.Build(repo =>
        {
            repo.Setup(r => r.GetActiveStaffAsync(42, null))
                .ReturnsAsync([AttendanceServiceBuilder.DefaultStaff]);
            repo.Setup(r => r.GetActiveSlotWindowsAsync()).ReturnsAsync(slots);
            repo.Setup(r => r.GetLogsForRangeAsync(
                    new DateOnly(year, month, 1),
                    new DateOnly(year, month, 30),
                    42, null))
                .ReturnsAsync(logs);
        });

        MonthlySummary summary = service
            .GetMonthlySummaryAsync(42, null, year, month).GetAwaiter().GetResult();

        StaffMonthlySummary staffSummary = summary.StaffSummaries.Single();

        bool countsConsistent = staffSummary.SlotSummaries.All(slot =>
            slot.OnTimeCount + slot.LateCount == loggedDays);

        return Prop.Label(
            countsConsistent,
            $"loggedDays={loggedDays} | " +
            string.Join("; ", staffSummary.SlotSummaries.Select(s =>
                $"{s.SlotName}: onTime={s.OnTimeCount} late={s.LateCount} excusedAbsent={s.ExcusedAbsentCount} unexcusedAbsent={s.UnexcusedAbsentCount}")));
    }
}
