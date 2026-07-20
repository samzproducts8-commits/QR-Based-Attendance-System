using Attendance.Application.DTOs;
using Attendance.Application.Exceptions;
using Attendance.Application.Helpers;
using Attendance.Application.Interfaces;
using Attendance.Application.Models;
using FluentValidation;
using FluentValidation.Results;

namespace Attendance.Application.Services;

/// <summary>
/// CRUD for attendance time-slot configurations plus the pure in-memory slot
/// resolution used during scan processing.
/// </summary>
/// <remarks>
/// Satisfies Requirements 2.1–2.5.
/// </remarks>
public sealed class SlotConfigService : ISlotConfigService
{
    /// <summary>The four named slots allowed by Requirement 2.2.</summary>
    private static readonly string[] AllowedSlotNames =
        ["MorningIn", "LunchOut", "LunchIn", "EveningOut"];

    private readonly ISlotConfigRepository _repository;

    public SlotConfigService(ISlotConfigRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc/>
    public async Task<SlotConfigDto> CreateSlotAsync(CreateSlotRequest request)
    {
        await ValidateAsync(request.SlotName, request.StartTime, request.EndTime, excludeSlotId: null);
        return await _repository.AddAsync(request);
    }

    /// <inheritdoc/>
    public async Task<SlotConfigDto> UpdateSlotAsync(int slotId, UpdateSlotRequest request)
    {
        await ValidateAsync(request.SlotName, request.StartTime, request.EndTime, excludeSlotId: slotId);

        SlotConfigDto? updated = await _repository.UpdateAsync(slotId, request);
        return updated ?? throw new NotFoundException($"Slot configuration {slotId} was not found.");
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<SlotConfigDto>> GetAllSlotsAsync()
        => await _repository.GetAllAsync();

    /// <inheritdoc/>
    public async Task DeactivateSlotAsync(int slotId)
    {
        if (!await _repository.DeactivateAsync(slotId))
            throw new NotFoundException($"Slot configuration {slotId} was not found.");
    }

    /// <inheritdoc/>
    public async Task DeleteSlotAsync(int slotId)
    {
        SlotConfigDto slot = await _repository.GetByIdAsync(slotId)
            ?? throw new NotFoundException($"Slot configuration {slotId} was not found.");

        int logCount = await _repository.CountAttendanceLogsAsync(slotId);
        if (logCount > 0)
            throw new SlotInUseException(
                $"Cannot delete '{slot.SlotName}': {logCount} attendance record(s) reference this slot. Deactivate it instead.");

        if (!await _repository.DeleteAsync(slotId))
            throw new NotFoundException($"Slot configuration {slotId} was not found.");
    }

    /// <inheritdoc/>
    public SlotWindow? ResolveSlotForTime(TimeOnly currentTime, IEnumerable<SlotWindow> slots)
        => SlotResolver.ResolveSlotForTime(currentTime, slots);

    // -------------------------------------------------------------------------
    // Validation (Requirements 2.2, 2.5; Property 10)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Enforces: known slot name, <c>EndTime &gt; StartTime</c> (Property 10),
    /// and no overlap with other active slots (the design's non-overlap
    /// precondition for deterministic slot resolution).
    /// </summary>
    private async Task ValidateAsync(
        string slotName, TimeOnly startTime, TimeOnly endTime, int? excludeSlotId)
    {
        var failures = new List<ValidationFailure>();

        if (!AllowedSlotNames.Contains(slotName))
            failures.Add(new ValidationFailure(
                nameof(SlotConfigDto.SlotName),
                $"SlotName must be one of: {string.Join(", ", AllowedSlotNames)}."));

        if (endTime <= startTime)
            failures.Add(new ValidationFailure(
                nameof(SlotConfigDto.EndTime),
                "EndTime must be after StartTime."));

        if (failures.Count == 0)
        {
            IReadOnlyList<SlotConfigDto> existing = await _repository.GetAllAsync();
            bool overlaps = existing.Any(s =>
                s.SlotId != excludeSlotId &&
                s.IsActive &&
                startTime <= s.EndTime &&
                endTime >= s.StartTime);

            if (overlaps)
                failures.Add(new ValidationFailure(
                    nameof(SlotConfigDto.StartTime),
                    "The slot window overlaps an existing active slot."));
        }

        if (failures.Count > 0)
            throw new ValidationException(failures);
    }
}
