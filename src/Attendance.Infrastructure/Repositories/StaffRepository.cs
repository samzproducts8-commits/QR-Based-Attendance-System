using Attendance.Application.DTOs;
using Attendance.Application.Interfaces;
using Attendance.Infrastructure.Data;
using Attendance.Infrastructure.Enums;
using Attendance.Infrastructure.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Attendance.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IStaffRepository"/>.
/// Staff creation also provisions a linked ASP.NET Identity user with the
/// <c>Employee</c> role (username = the staff's EMP-XXXX code) so the new
/// employee can immediately authenticate and scan (Requirements 5.1, 5.4).
/// </summary>
public sealed class StaffRepository : IStaffRepository
{
    private const string EmployeeRole = "Employee";
    private const string DefaultEmployeePasswordKey = "Auth:DefaultEmployeePassword";
    private const string FallbackEmployeePassword = "Employee@123!";

    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly string _defaultEmployeePassword;

    public StaffRepository(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration)
    {
        _context     = context;
        _userManager = userManager;
        _defaultEmployeePassword =
            configuration[DefaultEmployeePasswordKey] ?? FallbackEmployeePassword;
    }

    /// <inheritdoc />
    public async Task<int> GetMaxUniqueCodeNumberAsync()
    {
        // EMP-0001 codes are zero-padded to 4 digits; ordering by length first
        // keeps the ordering correct if the sequence ever exceeds EMP-9999.
        string? maxCode = await _context.Staff
            .AsNoTracking()
            .OrderByDescending(s => s.UniqueCode.Length)
            .ThenByDescending(s => s.UniqueCode)
            .Select(s => s.UniqueCode)
            .FirstOrDefaultAsync();

        if (maxCode is null)
            return 0;

        int dashIndex = maxCode.IndexOf('-');
        return dashIndex >= 0 && int.TryParse(maxCode[(dashIndex + 1)..], out int number)
            ? number
            : 0;
    }

    /// <inheritdoc />
    public Task<bool> EmailExistsAsync(string email, int? excludeStaffId = null)
    {
        return _context.Staff
            .AsNoTracking()
            .AnyAsync(s => s.Email == email
                           && (excludeStaffId == null || s.StaffId != excludeStaffId));
    }

    /// <inheritdoc />
    public Task<bool> DepartmentExistsAsync(int departmentId)
    {
        return _context.Departments
            .AsNoTracking()
            .AnyAsync(d => d.DepartmentId == departmentId && d.IsActive);
    }

    /// <inheritdoc />
    public async Task<StaffDto> CreateStaffWithProfileAsync(
        CreateStaffRequest request,
        string uniqueCode,
        string photoFileName,
        string photoPath)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        var staff = new Staff
        {
            UniqueCode     = uniqueCode,
            FullName       = request.FullName,
            Gender         = request.Gender,
            DateOfBirth    = request.DateOfBirth,
            PhoneNumber    = request.PhoneNumber,
            Email          = request.Email,
            DepartmentId   = request.DepartmentId,
            JobTitle       = request.JobTitle,
            EmploymentDate = request.EmploymentDate,
            Status         = StaffStatus.Active,
            CreatedAt      = DateTime.UtcNow,
            Profile = new StaffProfile
            {
                PhotoFileName    = photoFileName,
                PhotoContentType = "image/png",
                PhotoPath        = photoPath,
                Address          = request.Address,
                EmergencyContact = request.EmergencyContact
            }
        };

        _context.Staff.Add(staff);
        await _context.SaveChangesAsync();

        // Provision the linked Identity login (username = EMP-XXXX).
        // UserManager shares this DbContext, so it participates in the
        // ambient transaction — a failure rolls back the staff rows too.
        var user = new ApplicationUser
        {
            UserName       = uniqueCode,
            Email          = request.Email,
            EmailConfirmed = true,
            StaffId        = staff.StaffId
        };

        IdentityResult created = await _userManager.CreateAsync(user, _defaultEmployeePassword);
        if (!created.Succeeded)
            throw new InvalidOperationException(
                "Failed to create the employee login: "
                + string.Join(", ", created.Errors.Select(e => e.Description)));

        IdentityResult roleAssigned = await _userManager.AddToRoleAsync(user, EmployeeRole);
        if (!roleAssigned.Succeeded)
            throw new InvalidOperationException(
                "Failed to assign the Employee role: "
                + string.Join(", ", roleAssigned.Errors.Select(e => e.Description)));

        await transaction.CommitAsync();

        string departmentName = await _context.Departments
            .Where(d => d.DepartmentId == staff.DepartmentId)
            .Select(d => d.DepartmentName)
            .FirstAsync();

        return new StaffDto(
            staff.StaffId,
            staff.UniqueCode,
            staff.FullName,
            departmentName,
            staff.JobTitle ?? string.Empty,
            (int)staff.Status,
            photoPath);
    }

    /// <inheritdoc />
    public Task<StaffDto?> GetStaffDtoByIdAsync(int staffId)
    {
        return _context.Staff
            .AsNoTracking()
            .Where(s => s.StaffId == staffId)
            .Select(s => new StaffDto(
                s.StaffId,
                s.UniqueCode,
                s.FullName,
                s.Department.DepartmentName,
                s.JobTitle ?? string.Empty,
                (int)s.Status,
                s.Profile != null ? s.Profile.PhotoPath : null))
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public async Task<PagedResult<StaffDto>> GetPagedAsync(StaffFilterRequest filter)
    {
        int pageNumber = Math.Max(1, filter.PageNumber);
        int pageSize   = Math.Clamp(filter.PageSize, 1, 200);

        IQueryable<Staff> query = _context.Staff.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Department))
            query = query.Where(s => s.Department.DepartmentName == filter.Department);

        if (filter.Status is not null)
            query = query.Where(s => (int)s.Status == filter.Status);

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            string text = filter.SearchText.Trim();
            query = query.Where(s =>
                s.FullName.Contains(text)
                || s.UniqueCode.Contains(text)
                || (s.Email != null && s.Email.Contains(text)));
        }

        int totalCount = await query.CountAsync();

        List<StaffDto> items = await query
            .OrderBy(s => s.UniqueCode)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new StaffDto(
                s.StaffId,
                s.UniqueCode,
                s.FullName,
                s.Department.DepartmentName,
                s.JobTitle ?? string.Empty,
                (int)s.Status,
                s.Profile != null ? s.Profile.PhotoPath : null))
            .ToListAsync();

        return new PagedResult<StaffDto>(items, totalCount, pageNumber, pageSize);
    }

    /// <inheritdoc />
    public async Task<StaffDto?> UpdateStaffAsync(int staffId, UpdateStaffRequest request)
    {
        Staff? staff = await _context.Staff
            .Include(s => s.Department)
            .Include(s => s.Profile)
            .FirstOrDefaultAsync(s => s.StaffId == staffId);

        if (staff is null)
            return null;

        staff.FullName       = request.FullName;
        staff.Gender         = request.Gender;
        staff.DateOfBirth    = request.DateOfBirth;
        staff.PhoneNumber    = request.PhoneNumber;
        staff.Email          = request.Email;
        staff.DepartmentId   = request.DepartmentId;
        staff.JobTitle       = request.JobTitle;
        staff.EmploymentDate = request.EmploymentDate;

        if (staff.Profile is not null)
        {
            staff.Profile.Address          = request.Address;
            staff.Profile.EmergencyContact = request.EmergencyContact;
        }

        await _context.SaveChangesAsync();

        string departmentName = await _context.Departments
            .Where(d => d.DepartmentId == staff.DepartmentId)
            .Select(d => d.DepartmentName)
            .FirstAsync();

        return new StaffDto(
            staff.StaffId,
            staff.UniqueCode,
            staff.FullName,
            departmentName,
            staff.JobTitle ?? string.Empty,
            (int)staff.Status,
            staff.Profile?.PhotoPath);
    }

    /// <inheritdoc />
    public async Task<bool> DeactivateStaffAsync(int staffId)
    {
        Staff? staff = await _context.Staff.FirstOrDefaultAsync(s => s.StaffId == staffId);
        if (staff is null)
            return false;

        staff.Status = StaffStatus.Inactive;   // soft delete — Requirement 1.6
        await _context.SaveChangesAsync();
        return true;
    }

    /// <inheritdoc />
    public async Task<(bool Deleted, string? PhotoPath)> DeleteStaffAsync(int staffId)
    {
        Staff? staff = await _context.Staff
            .Include(s => s.Profile)
            .FirstOrDefaultAsync(s => s.StaffId == staffId);

        if (staff is null)
            return (false, null);

        string? photoPath = staff.Profile?.PhotoPath;

        await using var transaction = await _context.Database.BeginTransactionAsync();

        // Remove the dependents whose foreign keys block a Staff delete:
        //   • AttendanceLog  → Staff is Restrict — must be deleted first.
        //   • AspNetUsers    → Staff is No Action — the linked login must go too.
        // StaffProfile (Cascade) and QrSession.UsedByStaffId (Set Null) are
        // handled automatically by their foreign-key rules.
        await _context.AttendanceLogs
            .Where(l => l.StaffId == staffId)
            .ExecuteDeleteAsync();

        await _context.Users
            .Where(u => u.StaffId == staffId)
            .ExecuteDeleteAsync();

        _context.Staff.Remove(staff);
        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        return (true, photoPath);
    }

    /// <inheritdoc />
    public Task<StaffIdentityCardDto?> GetIdentityCardByCodeAsync(string uniqueCode)
    {
        return _context.Staff
            .AsNoTracking()
            .Where(s => s.UniqueCode == uniqueCode)
            .Select(s => new StaffIdentityCardDto(
                s.UniqueCode,
                s.FullName,
                s.Department.DepartmentName,
                s.JobTitle ?? string.Empty,
                s.Profile != null ? s.Profile.PhotoPath : null,
                s.Gender,
                s.DateOfBirth,
                s.PhoneNumber,
                s.Profile != null ? s.Profile.Address : null,
                s.Profile != null ? s.Profile.EmergencyContact : null))
            .FirstOrDefaultAsync();
    }
}
