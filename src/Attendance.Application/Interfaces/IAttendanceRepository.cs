using Attendance.Application.Enums;
using Attendance.Application.Models;

namespace Attendance.Application.Interfaces;

/// <summary>
/// Data-access operations needed by <c>AttendanceService</c> for recording
/// attendance events and building daily/monthly reports.
/// Defined in the Application layer (implemented in Infrastructure) following
/// the same dependency-inversion pattern as <see cref="IQrSessionRepository"/>.
/// </summary>
/// <remarks>
/// Satisfies Requirements 3.4, 3.9–3.12 and 4.1–4.5.
/// </remarks>
public interface IAttendanceRepository
{
    /// <summary>
    /// Returns all <c>IsActive = true</c> slot configurations projected as
    /// <see cref="SlotWindow"/> records, ordered by <c>StartTime</c>.
    /// </summary>
    Task<IReadOnlyList<SlotWindow>> GetActiveSlotWindowsAsync();

    /// <summary>
    /// Returns whether an attendance log already exists for the given
    /// (staff, slot, date) combination — the friendly duplicate pre-check
    /// (Requirement 3.10).
    /// </summary>
    Task<bool> LogExistsAsync(int staffId, int slotId, DateOnly date);

    /// <summary>
    /// Inserts a new attendance log row.
    /// </summary>
    /// <exception cref="Exceptions.DuplicateAttendanceException">
    /// Thrown when the database unique constraint on
    /// (StaffId, SlotId, EventDate) is violated by a concurrent insert
    /// (Requirement 7.3 / Error Scenario 7).
    /// </exception>
    Task InsertLogAsync(
        int staffId,
        int slotId,
        int? qrSessionId,
        DateTime eventTimestamp,
        DateOnly eventDate,
        AttendanceStatus statusFlag);

    /// <summary>
    /// Returns a lightweight snapshot of one staff member, or
    /// <see langword="null"/> when the id does not exist.
    /// </summary>
    Task<StaffSnapshot?> GetStaffSnapshotAsync(int staffId);

    /// <summary>
    /// Returns snapshots of all <em>active</em> staff, optionally narrowed to a
    /// single staff member and/or department, ordered by <c>UniqueCode</c>.
    /// </summary>
    Task<IReadOnlyList<StaffSnapshot>> GetActiveStaffAsync(
        int? staffId = null,
        int? departmentId = null);

    /// <summary>
    /// Returns all log entries for one staff member on one date.
    /// </summary>
    Task<IReadOnlyList<AttendanceLogEntry>> GetLogsForStaffDateAsync(
        int staffId,
        DateOnly date);

    /// <summary>
    /// Returns all log entries in the inclusive date range, optionally
    /// narrowed to a single staff member and/or department.
    /// </summary>
    Task<IReadOnlyList<AttendanceLogEntry>> GetLogsForRangeAsync(
        DateOnly fromDate,
        DateOnly toDate,
        int? staffId = null,
        int? departmentId = null);

    /// <summary>
    /// Upserts an <c>AttendanceLog</c> row with <c>StatusFlag = Absent</c>
    /// and the provided reason text.  If a log already exists for the given
    /// (staff, slot, date) with a non-Absent status, throws
    /// <see cref="Exceptions.BusinessRuleException"/>.
    /// </summary>
    Task SetAbsenceReasonAsync(int staffId, int slotId, DateOnly date, string reason);
}
