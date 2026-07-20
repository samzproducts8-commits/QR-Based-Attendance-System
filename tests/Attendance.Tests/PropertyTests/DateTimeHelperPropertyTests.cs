using Attendance.Application.Enums;
using Attendance.Application.Helpers;
using Attendance.Application.Models;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace Attendance.Tests.PropertyTests;

// ---------------------------------------------------------------------------
// FsCheck v3 Arbitraries
// ---------------------------------------------------------------------------

/// <summary>A slot paired with an On Time scan (event within [Start, End]).</summary>
/// <remarks>
/// Distinct wrapper type so FsCheck registers it separately from
/// <see cref="LateScan"/>; a bare <c>(SlotWindow, TimeOnly)</c> tuple would
/// collide with the Late generator and silently swap the two.
/// </remarks>
public sealed record OnTimeScan(SlotWindow Slot, TimeOnly EventTime);

/// <summary>A slot paired with a Late scan (event in the grace tail after End).</summary>
public sealed record LateScan(SlotWindow Slot, TimeOnly EventTime);

/// <summary>
/// Generators for DateTimeHelper property tests.
/// </summary>
/// <remarks>
/// Grace-period semantics (Option A): the grace period extends the slot's
/// <em>closing</em> boundary.  A scan anywhere in the normal
/// <c>StartTime</c>–<c>EndTime</c> window is On Time; a scan in the
/// <c>EndTime</c>–<c>EndTime + GracePeriodMinutes</c> tail is Late.
/// </remarks>
public static class DateTimeHelperArbitraries
{
    /// <summary>
    /// Generates a <see cref="SlotWindow"/> whose EndTime (and grace tail) stay
    /// within the same day (i.e., do not wrap past 23:59).
    /// </summary>
    public static Arbitrary<SlotWindow> SlotArb()
    {
        var gen =
            from startMin in Gen.Choose(0, 1300)
            from gracePeriodMin in Gen.Choose(0, 59)
            from durationMin in Gen.Choose(1, Math.Min(120, 1439 - startMin - gracePeriodMin))
            from slotId in Gen.Choose(1, 1000)
            from isMandatory in Gen.Elements(true, false)
            select new SlotWindow(
                SlotId: slotId,
                SlotName: $"Slot{slotId}",
                StartTime: new TimeOnly(startMin / 60, startMin % 60),
                EndTime: new TimeOnly((startMin + durationMin) / 60,
                                     (startMin + durationMin) % 60),
                GracePeriodMinutes: gracePeriodMin,
                IsMandatory: isMandatory,
                IsActive: true);

        return Arb.From(gen);
    }

    /// <summary>
    /// Generates an <see cref="OnTimeScan"/> whose event lands at
    /// <c>StartTime + offsetFromStart</c>, within the normal window
    /// [StartTime, EndTime] — i.e., an On Time scan.
    /// </summary>
    public static Arbitrary<OnTimeScan> OnTimeEventArb()
    {
        var gen =
            from startMin in Gen.Choose(0, 1300)
            from gracePeriodMin in Gen.Choose(0, 59)
            from durationMin in Gen.Choose(1, Math.Min(120, 1439 - startMin - gracePeriodMin))
            from slotId in Gen.Choose(1, 1000)
            from offsetFromStart in Gen.Choose(0, durationMin)   // within [Start, End]
            let slot = new SlotWindow(
                SlotId: slotId,
                SlotName: $"Slot{slotId}",
                StartTime: new TimeOnly(startMin / 60, startMin % 60),
                EndTime: new TimeOnly((startMin + durationMin) / 60,
                                     (startMin + durationMin) % 60),
                GracePeriodMinutes: gracePeriodMin,
                IsMandatory: true,
                IsActive: true)
            let eventMin = startMin + offsetFromStart
            select new OnTimeScan(slot, new TimeOnly(eventMin / 60, eventMin % 60));

        return Arb.From(gen);
    }

    /// <summary>
    /// Generates a <see cref="LateScan"/> whose event lands at
    /// <c>EndTime + offsetFromEnd</c> (offset ≥ 1), strictly after EndTime but
    /// within the grace tail (EndTime, EndTime + grace] — i.e., a Late scan the
    /// resolver still accepts.
    /// </summary>
    public static Arbitrary<LateScan> LateEventArb()
    {
        var gen =
            from startMin in Gen.Choose(0, 1300)
            from gracePeriodMin in Gen.Choose(1, 59)             // need a non-empty tail
            from durationMin in Gen.Choose(1, Math.Min(120, 1439 - startMin - gracePeriodMin))
            from slotId in Gen.Choose(1, 1000)
            from offsetFromEnd in Gen.Choose(1, gracePeriodMin)  // within (End, End + grace]
            let endMin = startMin + durationMin
            let slot = new SlotWindow(
                SlotId: slotId,
                SlotName: $"Slot{slotId}",
                StartTime: new TimeOnly(startMin / 60, startMin % 60),
                EndTime: new TimeOnly(endMin / 60, endMin % 60),
                GracePeriodMinutes: gracePeriodMin,
                IsMandatory: true,
                IsActive: true)
            let eventMin = endMin + offsetFromEnd
            select new LateScan(slot, new TimeOnly(eventMin / 60, eventMin % 60));

        return Arb.From(gen);
    }
}

// ---------------------------------------------------------------------------
// Property 3: Grace-Period Status Consistency
// Validates: Requirements 3.9, 3.11, 3.4, 4.1
// ---------------------------------------------------------------------------

/// <summary>
/// <b>Property 3: Grace-Period Status Consistency</b>
/// <para>
/// For any event time within the normal window (<c>StartTime ≤ time ≤ EndTime</c>),
/// <see cref="DateTimeHelper.ComputeStatusFlag"/> must return
/// <see cref="AttendanceStatus.OnTime"/>.  For any event time strictly after
/// <c>EndTime</c> (in the grace tail), it must return
/// <see cref="AttendanceStatus.Late"/>.
/// </para>
/// <b>Validates: Requirements 3.9, 3.11, 3.4, 4.1</b>
/// </summary>
public class DateTimeHelperProperty3_GracePeriodConsistency
{
    // -----------------------------------------------------------------------
    // Sub-property 3a: eventTime ≤ EndTime → OnTime
    // -----------------------------------------------------------------------

    /// <summary>
    /// For any slot and any event time within [StartTime, EndTime], the
    /// status must be OnTime.
    /// <b>Validates: Requirements 3.9, 3.4</b>
    /// </summary>
    [Property(Arbitrary = [typeof(DateTimeHelperArbitraries)], MaxTest = 500)]
    public Property Property3a_EventTimeInsideWindow_ReturnsOnTime(OnTimeScan input)
    {
        var (slot, eventTime) = input;

        var status = DateTimeHelper.ComputeStatusFlag(eventTime, slot);

        return Prop.Label(
            status == AttendanceStatus.OnTime,
            $"Slot=[{slot.StartTime:HH:mm}–{slot.EndTime:HH:mm}] " +
            $"EventTime={eventTime:HH:mm} | " +
            $"Got={status} (expected OnTime)");
    }

    /// <summary>
    /// The exact EndTime is treated as OnTime (inclusive upper bound of the
    /// on-time window; the grace tail begins after it).
    /// <b>Validates: Requirement 3.9</b>
    /// </summary>
    [Property(Arbitrary = [typeof(DateTimeHelperArbitraries)], MaxTest = 500)]
    public Property Property3a_EventTimeExactlyAtEndTime_ReturnsOnTime(SlotWindow slot)
    {
        var status = DateTimeHelper.ComputeStatusFlag(slot.EndTime, slot);

        return Prop.Label(
            status == AttendanceStatus.OnTime,
            $"Slot=[{slot.StartTime:HH:mm}–{slot.EndTime:HH:mm}] " +
            $"Grace={slot.GracePeriodMinutes}min | " +
            $"Got={status} (expected OnTime at exact EndTime)");
    }

    /// <summary>
    /// An event exactly at StartTime is always OnTime regardless of the grace
    /// period value.
    /// <b>Validates: Requirements 3.9, 3.4</b>
    /// </summary>
    [Property(Arbitrary = [typeof(DateTimeHelperArbitraries)], MaxTest = 500)]
    public Property Property3a_EventAtStartTime_AlwaysOnTime(SlotWindow slot)
    {
        var status = DateTimeHelper.ComputeStatusFlag(slot.StartTime, slot);

        return Prop.Label(
            status == AttendanceStatus.OnTime,
            $"Slot=[{slot.StartTime:HH:mm}–{slot.EndTime:HH:mm}] " +
            $"Grace={slot.GracePeriodMinutes}min | " +
            $"Got={status} (expected OnTime at StartTime)");
    }

    // -----------------------------------------------------------------------
    // Sub-property 3b: eventTime > EndTime → Late
    // -----------------------------------------------------------------------

    /// <summary>
    /// For any slot and any event time strictly after EndTime (within the
    /// grace tail), the status must be Late.
    /// <b>Validates: Requirements 3.9, 4.1</b>
    /// </summary>
    [Property(Arbitrary = [typeof(DateTimeHelperArbitraries)], MaxTest = 500)]
    public Property Property3b_EventTimeInGraceTail_ReturnsLate(LateScan input)
    {
        var (slot, eventTime) = input;

        var status = DateTimeHelper.ComputeStatusFlag(eventTime, slot);

        return Prop.Label(
            status == AttendanceStatus.Late,
            $"Slot=[{slot.StartTime:HH:mm}–{slot.EndTime:HH:mm}] " +
            $"Grace={slot.GracePeriodMinutes}min " +
            $"EventTime={eventTime:HH:mm} | " +
            $"Got={status} (expected Late)");
    }

    /// <summary>
    /// One minute past EndTime is always Late, regardless of the grace period
    /// (grace only governs whether the resolver still accepts the scan, not
    /// how the status is classified).
    /// <b>Validates: Requirement 3.9</b>
    /// </summary>
    [Property(Arbitrary = [typeof(DateTimeHelperArbitraries)], MaxTest = 500)]
    public Property Property3b_OneMinutePastEndTime_ReturnsLate(SlotWindow slot)
    {
        var endMin = slot.EndTime.Hour * 60 + slot.EndTime.Minute;

        // Skip if one minute past EndTime would exceed 23:59
        if (endMin + 1 > 1439)
            return Prop.ToProperty(true);

        var oneAfter = new TimeOnly((endMin + 1) / 60, (endMin + 1) % 60);
        var status   = DateTimeHelper.ComputeStatusFlag(oneAfter, slot);

        return Prop.Label(
            status == AttendanceStatus.Late,
            $"Slot=[{slot.StartTime:HH:mm}–{slot.EndTime:HH:mm}] " +
            $"Grace={slot.GracePeriodMinutes}min " +
            $"OnePastEnd={oneAfter:HH:mm} | " +
            $"Got={status} (expected Late)");
    }

    // -----------------------------------------------------------------------
    // Sub-property 3c: result is always binary (never ManualEntry)
    // -----------------------------------------------------------------------

    /// <summary>
    /// <see cref="DateTimeHelper.ComputeStatusFlag"/> only ever produces
    /// OnTime or Late — never ManualEntry — for any input.
    /// <b>Validates: Requirements 3.9, 4.1</b>
    /// </summary>
    [Property(Arbitrary = [typeof(DateTimeHelperArbitraries)], MaxTest = 500)]
    public Property Property3c_ResultIsNeverManualEntry(SlotWindow slot)
    {
        // Probe with StartTime (guaranteed within-window)
        var status = DateTimeHelper.ComputeStatusFlag(slot.StartTime, slot);

        return Prop.Label(
            status != AttendanceStatus.ManualEntry,
            $"ComputeStatusFlag returned ManualEntry for slot {slot.SlotId} " +
            $"— should only return OnTime or Late");
    }

    // -----------------------------------------------------------------------
    // Sub-property 3d: the EndTime boundary is the sole OnTime/Late divider
    // -----------------------------------------------------------------------

    /// <summary>
    /// For any slot, every time at or before EndTime is OnTime and every time
    /// after EndTime is Late — the classification depends only on EndTime, not
    /// on the grace period (which governs acceptance, not lateness).
    /// <b>Validates: Requirement 3.9</b>
    /// </summary>
    [Property(MaxTest = 300)]
    public Property Property3d_EndTimeIsTheSoleOnTimeLateDivider(
        int startHour, int startMin, int durationSeed, int graceSeed, int offsetSeed)
    {
        var h        = Math.Abs(startHour) % 20;              // 0..19 → leaves room for window + tail
        var m        = Math.Abs(startMin)  % 60;
        var duration = Math.Abs(durationSeed) % 60 + 1;       // 1..60 min window
        var grace    = Math.Abs(graceSeed)   % 30;            // 0..29 min tail

        var startTotalMin = h * 60 + m;
        var endTotalMin   = startTotalMin + duration;

        var slot = new SlotWindow(
            SlotId: 1,
            SlotName: "Boundary",
            StartTime: new TimeOnly(startTotalMin / 60, startTotalMin % 60),
            EndTime: new TimeOnly(endTotalMin / 60, endTotalMin % 60),
            GracePeriodMinutes: grace,
            IsMandatory: true,
            IsActive: true);

        // A probe strictly inside the window (expected OnTime) and one strictly
        // after EndTime (expected Late).
        var insideOffset = Math.Abs(offsetSeed) % duration;   // 0..duration-1 → ≤ EndTime
        var insideMin    = startTotalMin + insideOffset;
        var afterMin     = endTotalMin + 1;

        var inside = new TimeOnly(insideMin / 60, insideMin % 60);
        var after  = new TimeOnly(afterMin  / 60, afterMin  % 60);

        var statusInside = DateTimeHelper.ComputeStatusFlag(inside, slot);
        var statusAfter  = DateTimeHelper.ComputeStatusFlag(after, slot);

        bool insideOnTime = statusInside == AttendanceStatus.OnTime;
        bool afterLate    = statusAfter  == AttendanceStatus.Late;

        return Prop.Label(
            insideOnTime && afterLate,
            $"Slot=[{slot.StartTime:HH:mm}–{slot.EndTime:HH:mm}] Grace={grace}min | " +
            $"Inside={inside:HH:mm}→{statusInside} (expected OnTime), " +
            $"After={after:HH:mm}→{statusAfter} (expected Late)");
    }
}
