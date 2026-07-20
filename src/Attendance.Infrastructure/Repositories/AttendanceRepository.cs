using Attendance.Application.Exceptions;
using Attendance.Application.Interfaces;
using Attendance.Application.Models;
using Attendance.Infrastructure.Data;
using Attendance.Infrastructure.Enums;
using Attendance.Infrastructure.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IAttendanceRepository"/>.
/// Projects entities into the Application layer's layer-agnostic records
/// (<see cref="SlotWindow"/>, <see cref="StaffSnapshot"/>,
/// <see cref="AttendanceLogEntry"/>).
/// </summary>
public sealed class AttendanceRepository : IAttendanceRepository
{
    // SQL Server error numbers for unique-constraint violations.
    private const int UniqueIndexViolation  = 2601;
    private const int UniqueConstraintViolation = 2627;

    private readonly ApplicationDbContext _context;

    public AttendanceRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SlotWindow>> GetActiveSlotWindowsAsync()
    {
        return await _context.AttendanceSlotConfigs
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.StartTime)
            .Select(s => new SlotWindow(
                s.SlotId,
                s.SlotName,
                s.StartTime,
                s.EndTime,
                s.GracePeriodMinutes,
                s.IsMandatory,
                s.IsActive))
            .ToListAsync();
    }

    /// <inheritdoc />
    public Task<bool> LogExistsAsync(int staffId, int slotId, DateOnly date)
    {
        return _context.AttendanceLogs
            .AsNoTracking()
            .AnyAsync(l => l.StaffId == staffId && l.SlotId == slotId && l.EventDate == date);
    }

    /// <inheritdoc />
    public async Task InsertLogAsync(
        int staffId,
        int slotId,
        int? qrSessionId,
        DateTime eventTimestamp,
        DateOnly eventDate,
        Application.Enums.AttendanceStatus statusFlag)
    {
        var log = new AttendanceLog
        {
            StaffId        = staffId,
            SlotId         = slotId,
            QrSessionId    = qrSessionId,
            EventTimestamp = eventTimestamp,
            EventDate      = eventDate,
            StatusFlag     = (AttendanceStatus)(byte)statusFlag
        };

        _context.AttendanceLogs.Add(log);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Error Scenario 7: two inserts for the same (StaffId, SlotId, EventDate)
            // raced — the unique constraint stopped the second one.
            _context.Entry(log).State = EntityState.Detached;
            throw new DuplicateAttendanceException(
                "You have already checked in for this slot today.");
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException is SqlException sql
           && (sql.Number == UniqueIndexViolation || sql.Number == UniqueConstraintViolation);

    /// <inheritdoc />
    public async Task<StaffSnapshot?> GetStaffSnapshotAsync(int staffId)
    {
        return await _context.Staff
            .AsNoTracking()
            .Where(s => s.StaffId == staffId)
            .Select(s => new StaffSnapshot(
                s.StaffId,
                s.UniqueCode,
                s.FullName,
                s.DepartmentId,
                s.Department.DepartmentName,
                s.Status == StaffStatus.Active))
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StaffSnapshot>> GetActiveStaffAsync(
        int? staffId = null, int? departmentId = null)
    {
        IQueryable<Staff> query = _context.Staff
            .AsNoTracking()
            .Where(s => s.Status == StaffStatus.Active);

        if (staffId is not null)
            query = query.Where(s => s.StaffId == staffId);

        if (departmentId is not null)
            query = query.Where(s => s.DepartmentId == departmentId);

        return await query
            .OrderBy(s => s.UniqueCode)
            .Select(s => new StaffSnapshot(
                s.StaffId,
                s.UniqueCode,
                s.FullName,
                s.DepartmentId,
                s.Department.DepartmentName,
                true))
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AttendanceLogEntry>> GetLogsForStaffDateAsync(
        int staffId, DateOnly date)
    {
        return await _context.AttendanceLogs
            .AsNoTracking()
            .Where(l => l.StaffId == staffId && l.EventDate == date)
            .Select(l => new AttendanceLogEntry(
                l.AttendanceLogId,
                l.StaffId,
                l.SlotId,
                l.EventTimestamp,
                l.EventDate,
                (Application.Enums.AttendanceStatus)(byte)l.StatusFlag))
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AttendanceLogEntry>> GetLogsForRangeAsync(
        DateOnly fromDate, DateOnly toDate, int? staffId = null, int? departmentId = null)
    {
        IQueryable<AttendanceLog> query = _context.AttendanceLogs
            .AsNoTracking()
            .Where(l => l.EventDate >= fromDate && l.EventDate <= toDate);

        if (staffId is not null)
            query = query.Where(l => l.StaffId == staffId);

        if (departmentId is not null)
            query = query.Where(l => l.Staff.DepartmentId == departmentId);

        return await query
            .Select(l => new AttendanceLogEntry(
                l.AttendanceLogId,
                l.StaffId,
                l.SlotId,
                l.EventTimestamp,
                l.EventDate,
                (Application.Enums.AttendanceStatus)(byte)l.StatusFlag))
            .ToListAsync();
    }
}
