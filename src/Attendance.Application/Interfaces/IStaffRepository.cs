using Attendance.Application.DTOs;

namespace Attendance.Application.Interfaces;

/// <summary>
/// Data-access operations needed by <c>StaffService</c> for staff
/// registration, updates, deactivation, and paginated retrieval.
/// Defined in the Application layer (implemented in Infrastructure).
/// </summary>
/// <remarks>
/// Satisfies Requirements 1.1–1.8.
/// </remarks>
public interface IStaffRepository
{
    /// <summary>
    /// Returns the highest numeric suffix currently used by any
    /// <c>Staff.UniqueCode</c> (e.g. 12 for <c>EMP-0012</c>), or 0 when no
    /// staff exist yet.  Used to generate the next monotonic code
    /// (Requirement 1.1).
    /// </summary>
    Task<int> GetMaxUniqueCodeNumberAsync();

    /// <summary>
    /// Returns whether the given email is already used by a staff member
    /// other than <paramref name="excludeStaffId"/> (Requirement 1.7).
    /// </summary>
    Task<bool> EmailExistsAsync(string email, int? excludeStaffId = null);

    /// <summary>
    /// Returns whether an active department with the given id exists.
    /// </summary>
    Task<bool> DepartmentExistsAsync(int departmentId);

    /// <summary>
    /// Inserts the <c>Staff</c> row, its one-to-one <c>StaffProfile</c>
    /// (photo metadata), and a linked Identity user with the
    /// <c>Employee</c> role — all within a single transaction so a failure
    /// leaves no partial data (Requirements 1.1–1.5, 5.4).
    /// </summary>
    /// <param name="request">Validated registration payload.</param>
    /// <param name="uniqueCode">The pre-generated EMP-XXXX code.</param>
    /// <param name="photoFileName">Original uploaded file name.</param>
    /// <param name="photoPath">Relative storage path returned by <see cref="IFileStorageHelper"/>.</param>
    /// <returns>The created staff record as a <see cref="StaffDto"/>.</returns>
    Task<StaffDto> CreateStaffWithProfileAsync(
        CreateStaffRequest request,
        string uniqueCode,
        string photoFileName,
        string photoPath);

    /// <summary>
    /// Returns one staff member as a <see cref="StaffDto"/>, or
    /// <see langword="null"/> when the id does not exist.
    /// </summary>
    Task<StaffDto?> GetStaffDtoByIdAsync(int staffId);

    /// <summary>
    /// Returns a paginated, filtered staff list (Requirement 1.2).
    /// </summary>
    Task<PagedResult<StaffDto>> GetPagedAsync(StaffFilterRequest filter);

    /// <summary>
    /// Overwrites the mutable fields of an existing staff record.
    /// Returns the updated <see cref="StaffDto"/>, or <see langword="null"/>
    /// when the id does not exist.
    /// </summary>
    Task<StaffDto?> UpdateStaffAsync(int staffId, UpdateStaffRequest request);

    /// <summary>
    /// Sets the staff member's status to Inactive (soft delete — historical
    /// attendance is preserved, Requirement 1.6).
    /// Returns <see langword="false"/> when the id does not exist.
    /// </summary>
    Task<bool> DeactivateStaffAsync(int staffId);

    /// <summary>
    /// Permanently deletes a staff member and every record that depends on it
    /// — attendance logs, the linked Identity login, and the one-to-one profile
    /// — in a single transaction. QR-session references are nulled by their
    /// foreign key. Unlike <see cref="DeactivateStaffAsync"/> this is
    /// irreversible and discards attendance history.
    /// </summary>
    /// <returns>
    /// <c>(true, photoPath)</c> when a record was deleted (<c>photoPath</c> is
    /// the removed profile photo's storage path, or <see langword="null"/> when
    /// none), or <c>(false, null)</c> when no staff with the id exists.
    /// </returns>
    Task<(bool Deleted, string? PhotoPath)> DeleteStaffAsync(int staffId);
}
