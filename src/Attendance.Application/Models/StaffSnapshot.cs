namespace Attendance.Application.Models;

/// <summary>
/// Lightweight, layer-agnostic projection of a <c>Staff</c> row used by
/// Application services (attendance recording, report building) without
/// coupling the Application project to Infrastructure entity types.
/// </summary>
/// <param name="StaffId">Database primary key.</param>
/// <param name="UniqueCode">Auto-generated EMP-XXXX code.</param>
/// <param name="FullName">Full name of the staff member.</param>
/// <param name="DepartmentId">Owning department primary key.</param>
/// <param name="DepartmentName">Owning department display name.</param>
/// <param name="IsActive">Whether the staff member is currently active.</param>
public sealed record StaffSnapshot(
    int StaffId,
    string UniqueCode,
    string FullName,
    int DepartmentId,
    string DepartmentName,
    bool IsActive
);
