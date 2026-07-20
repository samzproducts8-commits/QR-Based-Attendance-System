using Attendance.Application.DTOs;
using Attendance.Application.Models;

namespace Attendance.Application.Interfaces;

/// <summary>
/// Manages <c>AttendanceSlotConfig</c> CRUD operations and provides the
/// pure in-memory slot resolution algorithm used during scan processing.
/// Satisfies Requirements 2.1–2.5.
/// </summary>
public interface ISlotConfigService
{
    /// <summary>
    /// Creates a new attendance time slot configuration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Requires a <c>SlotName</c> (one of <c>MorningIn</c>, <c>LunchOut</c>,
    /// <c>LunchIn</c>, <c>EveningOut</c>), <c>StartTime</c>, <c>EndTime</c>,
    /// <c>GracePeriodMinutes</c>, and <c>IsMandatory</c> (Requirement 2.2).
    /// </para>
    /// <para>
    /// Rejects requests where <c>EndTime</c> is not after <c>StartTime</c>
    /// with a validation error (Requirement 2.5).
    /// </para>
    /// </remarks>
    /// <param name="request">Slot configuration payload.</param>
    /// <returns>The persisted slot as a <see cref="SlotConfigDto"/>.</returns>
    /// <exception cref="FluentValidation.ValidationException">
    /// Thrown when <c>EndTime &lt;= StartTime</c>.
    /// </exception>
    Task<SlotConfigDto> CreateSlotAsync(CreateSlotRequest request);

    /// <summary>
    /// Updates an existing attendance slot configuration.
    /// Changes take effect immediately without requiring an application restart
    /// (Requirement 2.3).
    /// </summary>
    /// <param name="slotId">Primary key of the slot to update.</param>
    /// <param name="request">Updated slot configuration payload.</param>
    /// <returns>The updated slot as a <see cref="SlotConfigDto"/>.</returns>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no slot with <paramref name="slotId"/> exists.
    /// </exception>
    /// <exception cref="FluentValidation.ValidationException">
    /// Thrown when <c>EndTime &lt;= StartTime</c>.
    /// </exception>
    Task<SlotConfigDto> UpdateSlotAsync(int slotId, UpdateSlotRequest request);

    /// <summary>
    /// Returns all slot configurations, including inactive ones, ordered by
    /// <c>StartTime</c>.
    /// </summary>
    /// <returns>
    /// An enumerable of <see cref="SlotConfigDto"/> representing every
    /// configured time slot.
    /// </returns>
    Task<IEnumerable<SlotConfigDto>> GetAllSlotsAsync();

    /// <summary>
    /// Marks a slot configuration inactive (soft delete): it stops
    /// participating in scan resolution and reports, but historical
    /// attendance logs referencing it are preserved.
    /// </summary>
    /// <param name="slotId">Primary key of the slot to deactivate.</param>
    /// <exception cref="Exceptions.NotFoundException">
    /// Thrown when no slot with <paramref name="slotId"/> exists.
    /// </exception>
    Task DeactivateSlotAsync(int slotId);

    /// <summary>
    /// Permanently removes a slot configuration. Only permitted while no
    /// attendance log references it, so historical records can never be
    /// destroyed; otherwise the caller should deactivate the slot instead.
    /// </summary>
    /// <param name="slotId">Primary key of the slot to delete.</param>
    /// <exception cref="Exceptions.NotFoundException">
    /// Thrown when no slot with <paramref name="slotId"/> exists.
    /// </exception>
    /// <exception cref="Exceptions.SlotInUseException">
    /// Thrown when attendance logs reference the slot.
    /// </exception>
    Task DeleteSlotAsync(int slotId);

    /// <summary>
    /// Pure, synchronous slot resolution algorithm: finds the single active
    /// slot whose window contains <paramref name="currentTime"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Slot windows are assumed to be non-overlapping (enforced at configuration
    /// time).  The method returns <see langword="null"/> when no slot window
    /// covers the supplied time, indicating a scan outside any open window
    /// (Requirement 3.11).
    /// </para>
    /// <para>
    /// This is a pure function with no I/O side effects; it accepts the slot
    /// collection as a parameter so callers control when data is fetched from
    /// the database.  The Application project defines <see cref="SlotWindow"/>
    /// as its own type to avoid coupling to Infrastructure entity types.
    /// </para>
    /// </remarks>
    /// <param name="currentTime">The server UTC time-of-day to evaluate.</param>
    /// <param name="slots">
    /// The collection of active slot windows to search.
    /// Typically loaded from <see cref="GetAllSlotsAsync"/> and filtered to
    /// <c>IsActive = true</c> by the caller.
    /// </param>
    /// <returns>
    /// The matching <see cref="SlotWindow"/>, or <see langword="null"/> if
    /// no slot window covers <paramref name="currentTime"/>.
    /// </returns>
    SlotWindow? ResolveSlotForTime(TimeOnly currentTime, IEnumerable<SlotWindow> slots);
}
