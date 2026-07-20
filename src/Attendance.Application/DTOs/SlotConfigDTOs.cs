namespace Attendance.Application.DTOs;

/// <summary>
/// Read model for an attendance slot configuration.
/// Satisfies Requirements 2.1–2.4.
/// </summary>
/// <param name="SlotId">Database primary key.</param>
/// <param name="SlotName">
/// Named slot identifier, one of: MorningIn, LunchOut, LunchIn, EveningOut
/// (Requirement 2.2).
/// </param>
/// <param name="StartTime">Earliest time at which this slot accepts a scan.</param>
/// <param name="EndTime">
/// Latest On Time boundary; scans up to GracePeriodMinutes past this are still
/// accepted (as Late).
/// </param>
/// <param name="GracePeriodMinutes">
/// Minutes after EndTime during which a late scan is still accepted (recorded
/// as Late).
/// </param>
/// <param name="IsMandatory">
/// When true, absence is reported as Absent in daily and monthly reports
/// (Requirement 2.4).
/// </param>
/// <param name="IsActive">Whether this slot is currently active.</param>
public record SlotConfigDto(
    int SlotId,
    string SlotName,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int GracePeriodMinutes,
    bool IsMandatory,
    bool IsActive
);

/// <summary>
/// Payload for creating a new attendance slot configuration.
/// Satisfies Requirement 2.2.
/// </summary>
/// <param name="SlotName">
/// Named slot: MorningIn, LunchOut, LunchIn, or EveningOut.
/// </param>
/// <param name="StartTime">Start of the slot window.</param>
/// <param name="EndTime">End of the slot window. Must be after StartTime (Requirement 2.5).</param>
/// <param name="GracePeriodMinutes">Grace period in minutes (default 0).</param>
/// <param name="IsMandatory">Whether absence in this slot is reportable (default true).</param>
public record CreateSlotRequest(
    string SlotName,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int GracePeriodMinutes = 0,
    bool IsMandatory = true
);

/// <summary>
/// Payload for updating an existing attendance slot configuration.
/// Changes take effect immediately (Requirement 2.3).
/// </summary>
/// <param name="SlotName">Updated slot name.</param>
/// <param name="StartTime">Updated start time.</param>
/// <param name="EndTime">Updated end time. Must be after StartTime (Requirement 2.5).</param>
/// <param name="GracePeriodMinutes">Updated grace period in minutes (default 0).</param>
/// <param name="IsMandatory">Updated mandatory flag (default true).</param>
public record UpdateSlotRequest(
    string SlotName,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int GracePeriodMinutes = 0,
    bool IsMandatory = true
);
