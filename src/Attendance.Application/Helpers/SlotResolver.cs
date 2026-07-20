using Attendance.Application.Models;

namespace Attendance.Application.Helpers;

/// <summary>
/// Pure, stateless helper for resolving which attendance slot is open at a
/// given point in time.
/// </summary>
/// <remarks>
/// Satisfies Requirements 3.9 and 3.11.
/// </remarks>
public static class SlotResolver
{
    /// <summary>
    /// Returns the first active <see cref="SlotWindow"/> whose time window
    /// (including its grace-period tail) covers <paramref name="time"/>, or
    /// <see langword="null"/> when no active slot is open at that time.
    /// </summary>
    /// <param name="time">The office-local time-of-day to evaluate.</param>
    /// <param name="slots">
    /// The collection of slot windows to search.  Only slots whose
    /// <see cref="SlotWindow.IsActive"/> flag is <see langword="true"/> are
    /// considered; callers may pass the full list without pre-filtering.
    /// </param>
    /// <returns>
    /// The first matching active slot where
    /// <c>slot.StartTime &lt;= time &lt;= slot.EndTime + GracePeriodMinutes</c>,
    /// or <see langword="null"/> if none match (triggers a 422 outside-schedule
    /// response — Requirement 3.11).
    /// </returns>
    /// <remarks>
    /// The grace period extends the <em>closing</em> boundary: a scan is still
    /// accepted for up to <c>GracePeriodMinutes</c> after <c>EndTime</c>, and
    /// <see cref="DateTimeHelper.ComputeStatusFlag"/> marks any scan past
    /// <c>EndTime</c> as <c>Late</c>.  If the grace tail would cross midnight it
    /// is clamped to the end of the day — a scan cannot roll into the next day
    /// and still count toward today's slot.
    /// </remarks>
    public static SlotWindow? ResolveSlotForTime(
        TimeOnly time,
        IEnumerable<SlotWindow> slots)
    {
        foreach (var slot in slots)
        {
            if (!slot.IsActive)
                continue;

            TimeOnly closeTime = slot.EndTime.AddMinutes(slot.GracePeriodMinutes);

            // AddMinutes wraps past midnight; a wrapped close boundary would be
            // earlier than EndTime, so clamp it to end-of-day instead.
            if (closeTime < slot.EndTime)
                closeTime = TimeOnly.MaxValue;

            if (time >= slot.StartTime && time <= closeTime)
                return slot;
        }

        return null;
    }
}
