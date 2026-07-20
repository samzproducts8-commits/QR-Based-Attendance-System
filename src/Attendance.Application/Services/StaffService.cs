using Attendance.Application.DTOs;
using Attendance.Application.Exceptions;
using Attendance.Application.Helpers;
using Attendance.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Attendance.Application.Services;

/// <summary>
/// Staff registration, updates, deactivation, and paginated retrieval.
/// </summary>
/// <remarks>
/// Satisfies Requirements 1.1–1.8.
/// </remarks>
public sealed class StaffService : IStaffService
{
    private const string CodePrefix = "EMP-";

    private readonly IStaffRepository _repository;
    private readonly IFileStorageHelper _fileStorage;

    public StaffService(IStaffRepository repository, IFileStorageHelper fileStorage)
    {
        _repository  = repository;
        _fileStorage = fileStorage;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Pipeline (see design sequence "Staff Registration with Photo Upload"):
    /// <list type="number">
    ///   <item>Photo validation first — extension, MIME, magic bytes
    ///         (Requirements 1.3, 1.4; Property 4 delegation).  Nothing is
    ///         persisted when validation fails.</item>
    ///   <item>Department existence and email uniqueness checks
    ///         (Requirement 1.7).</item>
    ///   <item>Generate the next monotonic EMP-XXXX code (Requirement 1.1;
    ///         Property 5).  The unique index on <c>Staff.UniqueCode</c> is the
    ///         final guard against a rare concurrent-registration collision.</item>
    ///   <item>Save the photo, then insert Staff + StaffProfile + linked
    ///         Identity user in one transaction (Requirement 1.5).</item>
    /// </list>
    /// </remarks>
    public async Task<StaffDto> CreateStaffAsync(CreateStaffRequest request, IFormFile photo)
    {
        ValidationResult photoResult = PhotoValidationHelper.Validate(photo);
        if (!photoResult.IsValid)
            throw new PhotoValidationException(photoResult.ErrorMessage!);

        if (!await _repository.DepartmentExistsAsync(request.DepartmentId))
            throw new NotFoundException($"Department {request.DepartmentId} was not found or is inactive.");

        if (await _repository.EmailExistsAsync(request.Email))
            throw new DuplicateEmailException($"A staff member with email '{request.Email}' already exists.");

        int nextNumber   = await _repository.GetMaxUniqueCodeNumberAsync() + 1; //Repository asks database Highest Code if code is EMP-0008, it creats new +1
        string uniqueCode = $"{CodePrefix}{nextNumber:D4}";

        string photoPath = await _fileStorage.SavePhotoAsync(photo, uniqueCode);// save photofileto disk storage
        

        try
        {
            return await _repository.CreateStaffWithProfileAsync(
                request, uniqueCode, photo.FileName, photoPath);
        }
        catch
        {
            // Roll back the orphaned photo file when the DB transaction fails.
            _fileStorage.DeletePhoto(photoPath);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<StaffDto> UpdateStaffAsync(int staffId, UpdateStaffRequest request)
    {
        if (await _repository.EmailExistsAsync(request.Email, excludeStaffId: staffId))
            throw new DuplicateEmailException($"A staff member with email '{request.Email}' already exists.");

        if (!await _repository.DepartmentExistsAsync(request.DepartmentId))
            throw new NotFoundException($"Department {request.DepartmentId} was not found or is inactive.");

        StaffDto? updated = await _repository.UpdateStaffAsync(staffId, request);
        return updated ?? throw new NotFoundException($"Staff member {staffId} was not found.");
    }

    /// <inheritdoc/>
    public async Task DeactivateStaffAsync(int staffId)
    {
        if (!await _repository.DeactivateStaffAsync(staffId))
            throw new NotFoundException($"Staff member {staffId} was not found.");
    }

    /// <inheritdoc/>
    public async Task DeleteStaffAsync(int staffId)
    {
        (bool deleted, string? photoPath) = await _repository.DeleteStaffAsync(staffId);
        if (!deleted)
            throw new NotFoundException($"Staff member {staffId} was not found.");

        // The DB rows are gone; remove the orphaned photo file last so a storage
        // failure cannot leave a deleted-in-DB-but-not-in-UI record behind.
        if (!string.IsNullOrEmpty(photoPath))
            _fileStorage.DeletePhoto(photoPath);
    }

    /// <inheritdoc/>
    public async Task<StaffDto> GetByIdAsync(int staffId)
    {
        StaffDto? staff = await _repository.GetStaffDtoByIdAsync(staffId);
        return staff ?? throw new NotFoundException($"Staff member {staffId} was not found.");
    }

    /// <inheritdoc/>
    public Task<PagedResult<StaffDto>> GetAllAsync(StaffFilterRequest filter)
        => _repository.GetPagedAsync(filter);
}
