using Attendance.Application.DTOs;
using Attendance.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Attendance.Api.Controllers;

/// <summary>
/// Attendance time-slot configuration CRUD (Requirements 2.1–2.5, 5.4).
/// Changes take effect immediately — no redeploy needed (Requirement 2.3).
/// </summary>
[ApiController]
[Route("api/slots")]
[Authorize(Roles = "Admin")]
public sealed class SlotConfigController : ControllerBase
{
    private readonly ISlotConfigService _slotConfigService;

    public SlotConfigController(ISlotConfigService slotConfigService)
    {
        _slotConfigService = slotConfigService;
    }

    /// <summary>Returns all slot configurations ordered by start time.</summary>
    [HttpGet]
    [ProducesResponseType<IEnumerable<SlotConfigDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
        => Ok(await _slotConfigService.GetAllSlotsAsync());

    /// <summary>Creates a new slot configuration.</summary>
    /// <response code="400">EndTime not after StartTime, unknown slot name, or overlapping window.</response>
    [HttpPost]
    [ProducesResponseType<SlotConfigDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateSlotRequest request)
    {
        SlotConfigDto created = await _slotConfigService.CreateSlotAsync(request);
        return CreatedAtAction(nameof(GetAll), new { id = created.SlotId }, created);
    }

    /// <summary>Updates an existing slot configuration.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<SlotConfigDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateSlotRequest request)
        => Ok(await _slotConfigService.UpdateSlotAsync(id, request));

    /// <summary>
    /// Deactivates a slot configuration (soft delete — historical logs
    /// referencing it are preserved).
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Deactivate(int id)
    {
        await _slotConfigService.DeactivateSlotAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Permanently deletes a slot configuration. Allowed only when no
    /// attendance log references it.
    /// </summary>
    /// <response code="409">Attendance records reference this slot; deactivate it instead.</response>
    [HttpDelete("{id:int}/permanent")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        await _slotConfigService.DeleteSlotAsync(id);
        return NoContent();
    }
}
