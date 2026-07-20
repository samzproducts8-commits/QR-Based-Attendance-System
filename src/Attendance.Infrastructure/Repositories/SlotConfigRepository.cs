using Attendance.Application.DTOs;
using Attendance.Application.Exceptions;
using Attendance.Application.Interfaces;
using Attendance.Infrastructure.Data;
using Attendance.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ISlotConfigRepository"/>.
/// </summary>
public sealed class SlotConfigRepository : ISlotConfigRepository
{
    private readonly ApplicationDbContext _context;

    public SlotConfigRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SlotConfigDto>> GetAllAsync()
    {
        List<AttendanceSlotConfig> entities = await _context.AttendanceSlotConfigs
            .AsNoTracking()
            .OrderBy(s => s.StartTime)
            .ToListAsync();

        return entities.Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<SlotConfigDto?> GetByIdAsync(int slotId)
    {
        AttendanceSlotConfig? entity = await _context.AttendanceSlotConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SlotId == slotId);

        return entity is null ? null : ToDto(entity);
    }

    /// <inheritdoc />
    public async Task<SlotConfigDto> AddAsync(CreateSlotRequest request)
    {
        var entity = new AttendanceSlotConfig
        {
            SlotName           = request.SlotName,
            StartTime          = request.StartTime,
            EndTime            = request.EndTime,
            GracePeriodMinutes = request.GracePeriodMinutes,
            IsMandatory        = request.IsMandatory,
            IsActive           = true
        };

        _context.AttendanceSlotConfigs.Add(entity);
        await _context.SaveChangesAsync();

        return ToDto(entity);
    }

    /// <inheritdoc />
    public async Task<SlotConfigDto?> UpdateAsync(int slotId, UpdateSlotRequest request)
    {
        AttendanceSlotConfig? entity =
            await _context.AttendanceSlotConfigs.FirstOrDefaultAsync(s => s.SlotId == slotId);

        if (entity is null)
            return null;

        entity.SlotName           = request.SlotName;
        entity.StartTime          = request.StartTime;
        entity.EndTime            = request.EndTime;
        entity.GracePeriodMinutes = request.GracePeriodMinutes;
        entity.IsMandatory        = request.IsMandatory;

        await _context.SaveChangesAsync();
        return ToDto(entity);
    }

    /// <inheritdoc />
    public async Task<bool> DeactivateAsync(int slotId)
    {
        AttendanceSlotConfig? entity =
            await _context.AttendanceSlotConfigs.FirstOrDefaultAsync(s => s.SlotId == slotId);

        if (entity is null)
            return false;

        entity.IsActive = false;
        await _context.SaveChangesAsync();
        return true;
    }

    /// <inheritdoc />
    public async Task<int> CountAttendanceLogsAsync(int slotId)
        => await _context.AttendanceLogs.CountAsync(l => l.SlotId == slotId);

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(int slotId)
    {
        AttendanceSlotConfig? entity =
            await _context.AttendanceSlotConfigs.FirstOrDefaultAsync(s => s.SlotId == slotId);

        if (entity is null)
            return false;

        _context.AttendanceSlotConfigs.Remove(entity);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // A log referencing this slot was inserted after the caller's
            // pre-check; the Restrict foreign key rejected the delete.
            _context.Entry(entity).State = EntityState.Unchanged;
            throw new SlotInUseException(
                $"Cannot delete '{entity.SlotName}': attendance records reference this slot. Deactivate it instead.");
        }

        return true;
    }

    private static SlotConfigDto ToDto(AttendanceSlotConfig s) => new(
        s.SlotId,
        s.SlotName,
        s.StartTime,
        s.EndTime,
        s.GracePeriodMinutes,
        s.IsMandatory,
        s.IsActive);
}
