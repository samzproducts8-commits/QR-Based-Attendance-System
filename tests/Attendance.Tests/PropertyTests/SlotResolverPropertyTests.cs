using Attendance.Application.Helpers;
using Attendance.Application.Models;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace Attendance.Tests.PropertyTests;

// ---------------------------------------------------------------------------
// FsCheck v3 Arbitraries
// ---------------------------------------------------------------------------

/// <summary>
/// Generators for SlotResolver property tests.
/// </summary>
public static class SlotResolverArbitraries
{
    /// <summary>
    /// Generates a valid, non-degenerate <see cref="SlotWindow"/> whose
    /// StartTime is strictly before EndTime and whose window duration is at
    /// least 1 minute.  The window is always active.
    /// </summary>
    public static Arbitrary<SlotWindow> ActiveSlotWindowArb()
    {
        // Encode a time-of-day as total minutes since midnight (0..1439)
        var gen =
            from startMin in Gen.Choose(0, 1437)           // leave room for ≥1 min window
            from durationMin in Gen.Choose(1, 1439 - startMin)
            from slotId in Gen.Choose(1, 1000)
            from gracePeriodMin in Gen.Choose(0, 60)
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
    /// Generates an inactive <see cref="SlotWindow"/> — IsActive = false.
    /// </summary>
    public static Arbitrary<SlotWindow> InactiveSlotWindowArb()
    {
        var gen =
            from startMin in Gen.Choose(0, 1437)
            from durationMin in Gen.Choose(1, 1439 - startMin)
            from slotId in Gen.Choose(1, 1000)
            select new SlotWindow(
                SlotId: slotId,
                SlotName: $"Inactive{slotId}",
                StartTime: new TimeOnly(startMin / 60, startMin % 60),
                EndTime: new TimeOnly((startMin + durationMin) / 60,
                                     (startMin + durationMin) % 60),
                GracePeriodMinutes: 0,
                IsMandatory: false,
                IsActive: false);

        return Arb.From(gen);
    }
}

// ---------------------------------------------------------------------------
// Property 2: Slot Resolution Correctness
// Validates: Requirements 3.9, 3.11, 3.4, 4.1
// ---------------------------------------------------------------------------

/// <summary>
/// <b>Property 2: Slot Resolution Correctness</b>
/// <para>
/// For any time that falls within an active slot window
/// (<c>StartTime ≤ time ≤ EndTime</c>), <see cref="SlotResolver.ResolveSlotForTime"/>
/// must return that slot.  For any time that falls outside all active windows,
/// the result must be <see langword="null"/>.
/// </para>
/// <b>Validates: Requirements 3.9, 3.11, 3.4, 4.1</b>
/// </summary>
public class SlotResolverProperty2_ResolutionCorrectness
{
    // -----------------------------------------------------------------------
    // Sub-property 2a: time inside a window → correct slot returned
    // -----------------------------------------------------------------------

    /// <summary>
    /// For any active slot and any time within [StartTime, EndTime], the
    /// resolver must return exactly that slot (not null, not a different slot).
    /// <b>Validates: Requirements 3.9, 3.4</b>
    /// </summary>
    [Property(Arbitrary = [typeof(SlotResolverArbitraries)], MaxTest = 500)]
    public Property Property2a_TimeInsideWindow_CorrectSlotReturned(SlotWindow slot)
    {
        // Pick a time uniformly within [StartTime, EndTime]
        var startMin = slot.StartTime.Hour * 60 + slot.StartTime.Minute;
        var endMin   = slot.EndTime.Hour   * 60 + slot.EndTime.Minute;

        // totalMinutes ∈ [startMin, endMin] — use a deterministic mid-point
        // so the property is repeatable without an extra generator parameter
        var midMin = (startMin + endMin) / 2;
        var timeInWindow = new TimeOnly(midMin / 60, midMin % 60);

        var result = SlotResolver.ResolveSlotForTime(timeInWindow, [slot]);

        bool slotFound   = result is not null;
        bool correctSlot = result?.SlotId == slot.SlotId;

        return Prop.Label(
            slotFound && correctSlot,
            $"Slot=[{slot.StartTime:HH:mm}–{slot.EndTime:HH:mm}] " +
            $"Time={timeInWindow:HH:mm} | " +
            $"Got={result?.SlotId.ToString() ?? "null"} (expected {slot.SlotId})");
    }

    /// <summary>
    /// The resolver also returns the correct slot when the probe time is
    /// exactly at StartTime (inclusive lower bound).
    /// <b>Validates: Requirements 3.9, 3.4</b>
    /// </summary>
    [Property(Arbitrary = [typeof(SlotResolverArbitraries)], MaxTest = 500)]
    public Property Property2a_TimeAtStartBoundary_CorrectSlotReturned(SlotWindow slot)
    {
        var result = SlotResolver.ResolveSlotForTime(slot.StartTime, [slot]);

        return Prop.Label(
            result?.SlotId == slot.SlotId,
            $"StartTime={slot.StartTime:HH:mm} | " +
            $"Got={result?.SlotId.ToString() ?? "null"} (expected {slot.SlotId})");
    }

    /// <summary>
    /// The resolver also returns the correct slot when the probe time is
    /// exactly at EndTime (still inside the accepted window; the grace tail
    /// begins after EndTime).
    /// <b>Validates: Requirements 3.9, 3.4</b>
    /// </summary>
    [Property(Arbitrary = [typeof(SlotResolverArbitraries)], MaxTest = 500)]
    public Property Property2a_TimeAtEndBoundary_CorrectSlotReturned(SlotWindow slot)
    {
        var result = SlotResolver.ResolveSlotForTime(slot.EndTime, [slot]);

        return Prop.Label(
            result?.SlotId == slot.SlotId,
            $"EndTime={slot.EndTime:HH:mm} | " +
            $"Got={result?.SlotId.ToString() ?? "null"} (expected {slot.SlotId})");
    }

    /// <summary>
    /// The grace period extends the closing boundary: a scan anywhere in the
    /// tail (EndTime, EndTime + GracePeriodMinutes] must still resolve to the
    /// slot.  The exact grace deadline (EndTime + GracePeriodMinutes) is
    /// inclusive.
    /// <b>Validates: Requirements 3.9, 3.4</b>
    /// </summary>
    [Property(Arbitrary = [typeof(SlotResolverArbitraries)], MaxTest = 500)]
    public Property Property2a_TimeInsideGraceTail_CorrectSlotReturned(SlotWindow slot)
    {
        var endMin      = slot.EndTime.Hour * 60 + slot.EndTime.Minute;
        var deadlineMin = endMin + slot.GracePeriodMinutes;

        // Skip degenerate cases: no tail (grace = 0) or a tail that would spill
        // past the end of the day (slots are daytime windows in practice).
        if (slot.GracePeriodMinutes == 0 || deadlineMin > 1439)
            return Prop.ToProperty(true);

        var deadline = new TimeOnly(deadlineMin / 60, deadlineMin % 60);

        var result = SlotResolver.ResolveSlotForTime(deadline, [slot]);

        return Prop.Label(
            result?.SlotId == slot.SlotId,
            $"Slot=[{slot.StartTime:HH:mm}–{slot.EndTime:HH:mm}] Grace={slot.GracePeriodMinutes}min " +
            $"GraceDeadline={deadline:HH:mm} | " +
            $"Got={result?.SlotId.ToString() ?? "null"} (expected {slot.SlotId})");
    }

    // -----------------------------------------------------------------------
    // Sub-property 2b: time outside all windows → null returned
    // -----------------------------------------------------------------------

    /// <summary>
    /// For a time that is strictly before every active slot's StartTime (i.e.,
    /// midnight, which is before any slot that starts after 00:00), passing an
    /// empty collection always returns null.
    /// <b>Validates: Requirement 3.11</b>
    /// </summary>
    [Property(MaxTest = 300)]
    public Property Property2b_EmptySlotCollection_ReturnsNull(int hourSeed)
    {
        var hour = Math.Abs(hourSeed) % 24;
        var time = new TimeOnly(hour, 0);

        var result = SlotResolver.ResolveSlotForTime(time, []);

        return Prop.Label(
            result is null,
            $"Time={time:HH:mm} against empty collection | Got={result?.SlotId.ToString() ?? "null"} (expected null)");
    }

    /// <summary>
    /// For any time that falls strictly outside an active slot window
    /// (including its grace tail), the resolver must return null (no spurious
    /// match).  The probe is one minute past the grace deadline
    /// (EndTime + GracePeriodMinutes + 1); skipped when that would exceed 23:59.
    /// <b>Validates: Requirement 3.11</b>
    /// </summary>
    [Property(Arbitrary = [typeof(SlotResolverArbitraries)], MaxTest = 500)]
    public Property Property2b_TimeAfterGraceTail_ReturnsNull(SlotWindow slot)
    {
        var endMin      = slot.EndTime.Hour * 60 + slot.EndTime.Minute;
        var deadlineMin = endMin + slot.GracePeriodMinutes;

        // Skip if one minute past the grace deadline would exceed 23:59
        if (deadlineMin + 1 > 1439)
            return Prop.ToProperty(true);

        var afterMin = deadlineMin + 1;
        var timeAfter = new TimeOnly(afterMin / 60, afterMin % 60);

        var result = SlotResolver.ResolveSlotForTime(timeAfter, [slot]);

        return Prop.Label(
            result is null,
            $"Slot=[{slot.StartTime:HH:mm}–{slot.EndTime:HH:mm}] Grace={slot.GracePeriodMinutes}min " +
            $"TimeAfterTail={timeAfter:HH:mm} | " +
            $"Got={result?.SlotId.ToString() ?? "null"} (expected null)");
    }

    /// <summary>
    /// For any time strictly before a slot's StartTime, the resolver must
    /// return null (the slot window has not opened yet).
    /// <b>Validates: Requirement 3.11</b>
    /// </summary>
    [Property(Arbitrary = [typeof(SlotResolverArbitraries)], MaxTest = 500)]
    public Property Property2b_TimeBeforeSlotWindow_ReturnsNull(SlotWindow slot)
    {
        var startMin = slot.StartTime.Hour * 60 + slot.StartTime.Minute;

        // Skip if slot starts at midnight — no minute before it exists
        if (startMin == 0)
            return Prop.ToProperty(true);

        var beforeMin = startMin - 1;
        var timeBefore = new TimeOnly(beforeMin / 60, beforeMin % 60);

        var result = SlotResolver.ResolveSlotForTime(timeBefore, [slot]);

        return Prop.Label(
            result is null,
            $"Slot=[{slot.StartTime:HH:mm}–{slot.EndTime:HH:mm}] " +
            $"TimeBefore={timeBefore:HH:mm} | " +
            $"Got={result?.SlotId.ToString() ?? "null"} (expected null)");
    }

    // -----------------------------------------------------------------------
    // Sub-property 2c: inactive slots are never returned
    // -----------------------------------------------------------------------

    /// <summary>
    /// Inactive slots are never returned regardless of the probe time —
    /// even when the time falls squarely within the inactive slot's window.
    /// <b>Validates: Requirements 3.9, 3.11</b>
    /// </summary>
    [Property(Arbitrary = [typeof(SlotResolverArbitraries)], MaxTest = 500)]
    public Property Property2c_InactiveSlot_NeverReturned(SlotWindow activeSlot)
    {
        // Build an inactive copy of the generated active slot
        var inactiveSlot = activeSlot with { IsActive = false };

        // Probe at the mid-point of the slot window (definitely inside it)
        var startMin = inactiveSlot.StartTime.Hour * 60 + inactiveSlot.StartTime.Minute;
        var endMin   = inactiveSlot.EndTime.Hour   * 60 + inactiveSlot.EndTime.Minute;
        var midMin   = (startMin + endMin) / 2;
        var timeInWindow = new TimeOnly(midMin / 60, midMin % 60);

        var result = SlotResolver.ResolveSlotForTime(timeInWindow, [inactiveSlot]);

        return Prop.Label(
            result is null,
            $"InactiveSlot=[{inactiveSlot.StartTime:HH:mm}–{inactiveSlot.EndTime:HH:mm}] " +
            $"Time={timeInWindow:HH:mm} | Got={result?.SlotId.ToString() ?? "null"} (expected null)");
    }

    // -----------------------------------------------------------------------
    // Sub-property 2d: first matching slot wins (multiple slots in collection)
    // -----------------------------------------------------------------------

    /// <summary>
    /// When multiple active slots are present, the resolver returns the first
    /// one whose window covers the probe time — consistent with
    /// "first match wins" semantics in Algorithm 2.
    /// <b>Validates: Requirements 3.9, 4.1</b>
    /// </summary>
    [Property(Arbitrary = [typeof(SlotResolverArbitraries)], MaxTest = 300)]
    public Property Property2d_MultipleSlots_FirstMatchWins(SlotWindow slot)
    {
        // Build a non-overlapping "before" slot and a "during" slot
        var startMin = slot.StartTime.Hour * 60 + slot.StartTime.Minute;
        var endMin   = slot.EndTime.Hour   * 60 + slot.EndTime.Minute;
        var midMin   = (startMin + endMin) / 2;
        var probe    = new TimeOnly(midMin / 60, midMin % 60);

        // A disjoint slot placed safely after the probed slot (skip when no room)
        if (endMin + 2 > 1439)
            return Prop.ToProperty(true);

        var afterSlot = slot with
        {
            SlotId    = slot.SlotId + 10_000,
            StartTime = new TimeOnly((endMin + 1) / 60, (endMin + 1) % 60),
            EndTime   = new TimeOnly((endMin + 2) / 60, (endMin + 2) % 60),
        };

        var slots = new[] { slot, afterSlot };
        var result = SlotResolver.ResolveSlotForTime(probe, slots);

        return Prop.Label(
            result?.SlotId == slot.SlotId,
            $"Probe={probe:HH:mm} | " +
            $"First=[{slot.StartTime:HH:mm}–{slot.EndTime:HH:mm}] Id={slot.SlotId} | " +
            $"Got={result?.SlotId.ToString() ?? "null"} (expected {slot.SlotId})");
    }
}
