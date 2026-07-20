using Attendance.Infrastructure.Data;
using Attendance.Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Api.Controllers;

/// <summary>
/// Minimal department lookup/management supporting the staff registration
/// form's department dropdown (Requirement 1.2 — staff must belong to a
/// department).
/// </summary>
[ApiController]
[Route("api/departments")]
public sealed class DepartmentController : ControllerBase
{
    /// <summary>Read model for a department.</summary>
    public sealed record DepartmentDto(int DepartmentId, string DepartmentName, bool IsActive);

    /// <summary>Payload for creating a department.</summary>
    public sealed record CreateDepartmentRequest(string DepartmentName);

    private readonly ApplicationDbContext _context;

    public DepartmentController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>Returns all active departments ordered by name.</summary>
    [HttpGet]
    [Authorize(Roles = "Admin,HR")]
    [ProducesResponseType<IEnumerable<DepartmentDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        List<DepartmentDto> departments = await _context.Departments
            .AsNoTracking()
            .Where(d => d.IsActive)
            .OrderBy(d => d.DepartmentName)
            .Select(d => new DepartmentDto(d.DepartmentId, d.DepartmentName, d.IsActive))
            .ToListAsync();

        return Ok(departments);
    }

    /// <summary>Creates a new department.</summary>
    /// <response code="409">A department with the same name already exists.</response>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType<DepartmentDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateDepartmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DepartmentName))
            return BadRequest(new { error = "DepartmentName is required." });

        string name = request.DepartmentName.Trim();

        if (await _context.Departments.AnyAsync(d => d.DepartmentName == name))
            return Conflict(new { error = $"Department '{name}' already exists." });

        var department = new Department { DepartmentName = name, IsActive = true };
        _context.Departments.Add(department);
        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetAll),
            new { id = department.DepartmentId },
            new DepartmentDto(department.DepartmentId, department.DepartmentName, department.IsActive));
    }
}
