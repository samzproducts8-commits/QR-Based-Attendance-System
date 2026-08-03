using Attendance.Application.DTOs;
using Microsoft.AspNetCore.Http;

namespace Attendance.Application.Interfaces;

/// <summary>
/// Encapsulates all staff management operations: registration, profile updates,
/// deactivation, and paginated retrieval.
/// Satisfies Requirements 1.1–1.8.
/// </summary>
public interface IStaffService
{
    /// <summary>
    /// Creates a new staff member record along with an associated profile photo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Generates a monotonically increasing <c>UniqueCode</c> in the format
    /// <c>EMP-XXXX</c> (Requirement 1.1).
    /// </para>
    /// <para>
    /// Validates that <paramref name="photo"/> has a <c>.png</c> extension,
    /// <c>image/png</c> MIME type, and correct PNG magic bytes before persisting
    /// (Requirement 1.3).
    /// </para>
    /// </remarks>
    /// <param name="request">Validated staff registration payload.</param>
    /// <param name="photo">Mandatory PNG profile photo uploaded via multipart form.</param>
    /// <returns>The newly created staff record as a <see cref="StaffDto"/>.</returns>
    /// <exception cref="FluentValidation.ValidationException">
    /// Thrown when photo validation fails (Requirement 1.4).
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the supplied email already exists (Requirement 1.7 — mapped to HTTP 409).
    /// </exception>
    Task<StaffDto> CreateStaffAsync(CreateStaffRequest request, IFormFile photo);

    /// <summary>
    /// Updates mutable fields on an existing staff profile.
    /// The <c>UniqueCode</c> and original creation metadata are never changed.
    /// </summary>
    /// <param name="staffId">Primary key of the staff record to update.</param>
    /// <param name="request">Fields to overwrite on the staff record.</param>
    /// <returns>The updated staff record as a <see cref="StaffDto"/>.</returns>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no staff record with <paramref name="staffId"/> exists.
    /// </exception>
    Task<StaffDto> UpdateStaffAsync(int staffId, UpdateStaffRequest request);

    /// <summary>
    /// Soft-deletes a staff member by setting their status to <em>Inactive</em>.
    /// No records are physically deleted; all attendance history is preserved
    /// (Requirement 1.6).
    /// </summary>
    /// <param name="staffId">Primary key of the staff record to deactivate.</param>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no staff record with <paramref name="staffId"/> exists.
    /// </exception>
    Task DeactivateStaffAsync(int staffId);

    /// <summary>
    /// Permanently deletes a staff member and all dependent data (attendance
    /// history, linked login, profile, and the stored profile photo).
    /// This is irreversible — use <see cref="DeactivateStaffAsync"/> when the
    /// history must be preserved (Requirement 1.6).
    /// </summary>
    /// <param name="staffId">Primary key of the staff record to delete.</param>
    /// <exception cref="Attendance.Application.Exceptions.NotFoundException">
    /// Thrown when no staff record with <paramref name="staffId"/> exists.
    /// </exception>
    Task DeleteStaffAsync(int staffId);

    /// <summary>
    /// Retrieves a single staff record by primary key.
    /// </summary>
    /// <param name="staffId">Primary key of the staff record to fetch.</param>
    /// <returns>The matching staff record as a <see cref="StaffDto"/>.</returns>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no staff record with <paramref name="staffId"/> exists.
    /// </exception>
    Task<StaffDto> GetByIdAsync(int staffId);

    /// <summary>
    /// Returns a paginated, optionally filtered list of staff records.
    /// </summary>
    /// <param name="filter">
    /// Filter and pagination parameters (department, status, search text,
    /// page number, page size).
    /// </param>
    /// <returns>
    /// A <see cref="PagedResult{StaffDto}"/> containing the current page of
    /// results and total count metadata.
    /// </returns>
    Task<PagedResult<StaffDto>> GetAllAsync(StaffFilterRequest filter);

    /// <summary>
    /// Generates a base64-encoded PNG QR code for the staff member identified
    /// by <paramref name="staffId"/>.  The QR payload is a URL pointing to the
    /// public employee identification page, built from <paramref name="employeeIdBaseUrl"/>.
    /// </summary>
    /// <param name="staffId">Primary key of the staff record.</param>
    /// <param name="employeeIdBaseUrl">
    /// The base URL for the public employee ID page (e.g.
    /// <c>http://192.168.1.5:4200/employee-id</c>).
    /// </param>
    /// <returns>Base64 string of the QR PNG image.</returns>
    Task<string> GetQrCodeBase64Async(int staffId, string employeeIdBaseUrl);

    /// <summary>
    /// Returns the employee identity card data for the staff member with the
    /// given <paramref name="uniqueCode"/> (e.g. <c>EMP-0001</c>).
    /// Used by the public (anonymous) employee identification endpoint.
    /// </summary>
    Task<StaffIdentityCardDto> GetIdentityCardAsync(string uniqueCode);
}
