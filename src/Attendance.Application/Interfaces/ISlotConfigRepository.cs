using Attendance.Application.DTOs;

namespace Attendance.Application.Interfaces;

/// <summary>
/// Data-access operations needed by <c>SlotConfigService</c> for attendance
/// time-slot configuration CRUD.
/// Defined in the Application layer (implemented in Infrastructure).
/// </summary>
/// <remarks>
/// Satisfies Requirements 2.1–2.5.
/// </remarks>
public interface ISlotConfigRepository
{
    /// <summary>
    /// Returns all slot configurations (active and inactive), ordered by
    /// <c>StartTime</c>.
    /// </summary>
    Task<IReadOnlyList<SlotConfigDto>> GetAllAsync();

    /// <summary>
    /// Returns one slot configuration, or <see langword="null"/> when the id
    /// does not exist.
    /// </summary>
    Task<SlotConfigDto?> GetByIdAsync(int slotId);

    /// <summary>
    /// Inserts a new slot configuration and returns it with its generated id.
    /// </summary>
    Task<SlotConfigDto> AddAsync(CreateSlotRequest request);

    /// <summary>
    /// Overwrites an existing slot configuration.  Changes take effect
    /// immediately for subsequent scans (Requirement 2.3).
    /// Returns <see langword="null"/> when the id does not exist.
    /// </summary>
    Task<SlotConfigDto?> UpdateAsync(int slotId, UpdateSlotRequest request);

    /// <summary>
    /// Marks a slot configuration inactive (soft delete).
    /// Returns <see langword="false"/> when the id does not exist.
    /// </summary>
    Task<bool> DeactivateAsync(int slotId);

    /// <summary>
    /// Counts the attendance logs referencing a slot configuration.
    /// A non-zero result means the slot cannot be permanently deleted.
    /// </summary>
    Task<int> CountAttendanceLogsAsync(int slotId);

    /// <summary>
    /// Permanently removes a slot configuration.
    /// Returns <see langword="false"/> when the id does not exist.
    /// </summary>
    /// <exception cref="Exceptions.SlotInUseException">
    /// Thrown when attendance logs still reference the slot; the database
    /// foreign key uses <c>DeleteBehavior.Restrict</c>.
    /// </exception>
    Task<bool> DeleteAsync(int slotId);
}
