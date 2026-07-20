using Attendance.Application.DTOs;
using Attendance.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Attendance.Api.Controllers;

/// <summary>
/// Staff registration and management (Requirements 1.1–1.8, 5.3, 5.4).
/// Thin controller: request-shape validation only, all business rules live in
/// <see cref="IStaffService"/>.
/// </summary>
[ApiController]
[Route("api/staff")]
public sealed class StaffController : ControllerBase
{
    //creating a variable that holds "staff service" called "IStaffService" or we make (Dependency Injection (DI).) .
    private readonly IStaffService _staffService;

    public StaffController(IStaffService staffService)
    {
        _staffService = staffService;
    }

    /// <summary>
    /// Registers a new staff member with a mandatory PNG profile photo
    /// (multipart/form-data).
    /// </summary>
    /// <response code="201">Staff created; body carries the new record with its EMP-XXXX code.</response>
    /// <response code="400">Photo failed PNG validation (extension / MIME / magic bytes).</response>
    /// <response code="409">Email already registered.</response>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType<StaffDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromForm] CreateStaffRequest request, IFormFile photo)  // bezi form tekebelachew.  The client sends data then ASP.NET automatically converts it into "CreateStaffRequest". this handles the clientinput data
                                                  //  example : FullName = Sami, Email = sami@gmail.com, DepartmentId = 2
         
    {
        StaffDto created = await _staffService.CreateStaffAsync(request, photo); // leservice interfasu yemiserawn sera createstaff blo setew. wait until "_staffService" returns "staff dto"
        return CreatedAtAction(nameof(GetById), new { id = created.StaffId }, created);// then it gives id, department, etc..
    }

    /// <summary>Updates the mutable fields of an existing staff record.</summary>
    /// or UPDATE STAFF with the created staff information.
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,HR")]
    [ProducesResponseType<StaffDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateStaffRequest request)
        => Ok(await _staffService.UpdateStaffAsync(id, request));

    /// <summary>
    /// Soft-deletes a staff member (sets status Inactive; attendance history
    /// is preserved — Requirement 1.6).
    /// </summary>
    [HttpPatch("{id:int}/deactivate")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Deactivate(int id)
    {
        await _staffService.DeactivateStaffAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Permanently deletes a staff member and all dependent data (attendance
    /// history, linked login, profile photo). Irreversible — prefer
    /// <see cref="Deactivate"/> to preserve history (Requirement 1.6).
    /// </summary>
    /// <response code="204">Staff and all dependent data were deleted.</response>
    /// <response code="404">No staff member with the given id exists.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(int id)
    {
        await _staffService.DeleteStaffAsync(id);
        return NoContent();
    }

    /// <summary>Returns one staff record.</summary>
    [HttpGet("{id:int}")]
    [Authorize(Roles = "Admin,HR")]
    [ProducesResponseType<StaffDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(int id)
        => Ok(await _staffService.GetByIdAsync(id));

    /// <summary>Returns a paginated, filterable staff list.</summary>
    [HttpGet]
    [Authorize(Roles = "Admin,HR")]
    [ProducesResponseType<PagedResult<StaffDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] StaffFilterRequest filter)
        => Ok(await _staffService.GetAllAsync(filter));
}
