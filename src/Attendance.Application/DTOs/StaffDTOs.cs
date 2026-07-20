namespace Attendance.Application.DTOs;

/// <summary>
/// Payload for registering a new staff member.
/// Satisfies Requirement 1.2 — all mandatory profile fields.
/// </summary>
/// <param name="FullName">Employee's full legal name.</param>
/// <param name="Gender">Gender string (e.g. "Male", "Female").</param>
/// <param name="DateOfBirth">Employee's date of birth.</param>
/// <param name="PhoneNumber">Contact phone number.</param>
/// <param name="Email">Unique email address (Requirement 1.7).</param>
/// <param name="DepartmentId">Foreign key to an existing active Department.</param>
/// <param name="JobTitle">Job title / position.</param>
/// <param name="EmploymentDate">Date the employee joined the organisation.</param>
/// <param name="Address">Optional home address.</param>
/// <param name="EmergencyContact">Optional emergency contact name/phone.</param>
public record CreateStaffRequest(
    string FullName,
    string Gender,
    DateOnly DateOfBirth,
    string PhoneNumber,
    string Email,
    int DepartmentId,
    string JobTitle,
    DateOnly EmploymentDate,
    string? Address,
    string? EmergencyContact
);

/// <summary>
/// Payload for updating mutable fields on an existing staff profile.
/// UniqueCode and creation metadata are never changed.
/// </summary>
/// <param name="FullName">Updated full name.</param>
/// <param name="Gender">Updated gender.</param>
/// <param name="DateOfBirth">Updated date of birth.</param>
/// <param name="PhoneNumber">Updated phone number.</param>
/// <param name="Email">Updated email address (must remain unique).</param>
/// <param name="DepartmentId">Updated department reference.</param>
/// <param name="JobTitle">Updated job title.</param>
/// <param name="EmploymentDate">Updated employment date.</param>
/// <param name="Address">Updated optional address.</param>
/// <param name="EmergencyContact">Updated optional emergency contact.</param>
public record UpdateStaffRequest(
    string FullName,
    string Gender,
    DateOnly DateOfBirth,
    string PhoneNumber,
    string Email,
    int DepartmentId,
    string JobTitle,
    DateOnly EmploymentDate,
    string? Address,
    string? EmergencyContact
);

/// <summary>
/// Read model for a staff member returned from API responses.
/// </summary>
/// <param name="StaffId">Database primary key.</param>
/// <param name="UniqueCode">Auto-generated code in EMP-XXXX format (Requirement 1.1).</param>
/// <param name="FullName">Full name of the staff member.</param>
/// <param name="Department">Department name (denormalized for display).</param>
/// <param name="JobTitle">Current job title.</param>
/// <param name="Status">Employment status: 1 = Active, 0 = Inactive (Requirement 1.6).</param>
/// <param name="PhotoUrl">Optional URL path to the staff member's PNG profile photo.</param>
public record StaffDto(
    int StaffId,
    string UniqueCode,
    string FullName,
    string Department,
    string JobTitle,
    int Status,
    string? PhotoUrl
);

/// <summary>
/// Filter and pagination parameters for the staff list endpoint.
/// </summary>
/// <param name="Department">Optional department name filter.</param>
/// <param name="Status">Optional status filter (1 = Active, 0 = Inactive, null = all).</param>
/// <param name="SearchText">Optional free-text search across name / email / unique code.</param>
/// <param name="PageNumber">1-based page index (default 1).</param>
/// <param name="PageSize">Number of records per page (default 20).</param>
public record StaffFilterRequest(
    string? Department = null,
    int? Status = null,
    string? SearchText = null,
    int PageNumber = 1,
    int PageSize = 20
);

/// <summary>
/// Generic paged result wrapper returned by list endpoints.
/// </summary>
/// <typeparam name="T">The item type contained in this page.</typeparam>
/// <param name="Items">The records on the current page.</param>
/// <param name="TotalCount">Total number of matching records across all pages.</param>
/// <param name="PageNumber">Current 1-based page index.</param>
/// <param name="PageSize">Maximum number of items per page.</param>
public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int PageNumber,
    int PageSize
)
{
    /// <summary>Total number of pages given TotalCount and PageSize.</summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}
