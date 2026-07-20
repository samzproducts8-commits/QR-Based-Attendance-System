using Attendance.Application.DTOs;
using Attendance.Application.Enums;

namespace Attendance.Application.Interfaces;

/// <summary>
/// Handles attendance event recording, daily/monthly report retrieval,
/// and report exports.
/// Satisfies Requirements 3.4–3.12 and 4.1–4.5.
/// </summary>
public interface IAttendanceService
{
    /// <summary>
    /// Records an attendance event for the specified staff member against the
    /// QR session that was just consumed (Requirement 3.4).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Resolves the correct <c>AttendanceSlotConfig</c> by comparing the current
    /// office-local time against all active slot windows, including each slot's
    /// grace-period tail (Requirement 3.9).
    /// </para>
    /// <para>
    /// Computes <c>StatusFlag</c> as <em>OnTime</em> when the event time is at or
    /// before the slot's <c>EndTime</c>; a scan in the grace tail
    /// (<c>EndTime</c> to <c>EndTime + GracePeriodMinutes</c>) is still accepted
    /// and recorded as <em>Late</em>.
    /// </para>
    /// </remarks>
    /// <param name="staffId">
    /// The authenticated employee's <c>Staff.StaffId</c>.
    /// </param>
    /// <param name="qrSessionId">
    /// The <c>QrSession.QrSessionId</c> primary key of the just-consumed token.
    /// </param>
    /// <returns>
    /// An <see cref="AttendanceRecordDto"/> containing the greeting message,
    /// slot name, timestamp, and status label (Requirement 3.12).
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown with HTTP 422 when no slot window covers the current time
    /// (Requirement 3.11).
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown with HTTP 409 when the staff member has already recorded this
    /// slot today (Requirement 3.10).
    /// </exception>
    Task<AttendanceRecordDto> RecordAttendanceAsync(int staffId, int qrSessionId);

    /// <summary>
    /// Returns the full daily attendance sheet for one staff member on a
    /// specific date (Requirement 4.1).
    /// </summary>
    /// <remarks>
    /// Mandatory slots with no log entry are represented as <em>Absent</em>
    /// (Requirement 4.5).
    /// </remarks>
    /// <param name="staffId">Staff member primary key.</param>
    /// <param name="date">The calendar date to retrieve.</param>
    /// <returns>A <see cref="DailyAttendanceSheet"/> with one row per slot.</returns>
    Task<DailyAttendanceSheet> GetDailySheetAsync(int staffId, DateOnly date);

    /// <summary>
    /// Returns aggregated monthly attendance statistics, optionally filtered by
    /// staff member or department (Requirements 4.2, 4.4).
    /// </summary>
    /// <remarks>
    /// Pass both <paramref name="staffId"/> and <paramref name="departmentId"/> as
    /// <see langword="null"/> to retrieve statistics for all staff.
    /// </remarks>
    /// <param name="staffId">
    /// When provided, limits the summary to a single staff member.
    /// </param>
    /// <param name="departmentId">
    /// When provided (and <paramref name="staffId"/> is <see langword="null"/>),
    /// limits the summary to all staff in that department.
    /// </param>
    /// <param name="year">The four-digit year of the report period.</param>
    /// <param name="month">The month number (1–12) of the report period.</param>
    /// <returns>
    /// A <see cref="MonthlySummary"/> with per-staff and per-department aggregates.
    /// </returns>
    Task<MonthlySummary> GetMonthlySummaryAsync(int? staffId, int? departmentId, int year, int month);

    /// <summary>
    /// Generates and streams a downloadable daily attendance report file
    /// (Requirement 4.3).
    /// </summary>
    /// <param name="date">The calendar date to export.</param>
    /// <param name="format">
    /// The desired output format: <see cref="ExportFormat.Xlsx"/> or
    /// <see cref="ExportFormat.Pdf"/>.
    /// </param>
    /// <returns>The raw bytes of the generated file.</returns>
    Task<byte[]> ExportDailyReportAsync(DateOnly date, ExportFormat format);

    /// <summary>
    /// Generates and streams a downloadable monthly attendance report file
    /// (Requirement 4.3).
    /// </summary>
    /// <param name="year">The four-digit year of the report period.</param>
    /// <param name="month">The month number (1–12) of the report period.</param>
    /// <param name="format">
    /// The desired output format: <see cref="ExportFormat.Xlsx"/> or
    /// <see cref="ExportFormat.Pdf"/>.
    /// </param>
    /// <returns>The raw bytes of the generated file.</returns>
    Task<byte[]> ExportMonthlyReportAsync(int year, int month, ExportFormat format);

    /// <summary>
    /// Returns the daily attendance sheets of <em>all active staff</em> for one
    /// date — used by the HR daily report screen and the daily export
    /// (Requirements 4.1, 4.3).
    /// </summary>
    /// <param name="date">The calendar date to retrieve.</param>
    Task<IReadOnlyList<DailyAttendanceSheet>> GetDailySheetsAsync(DateOnly date);

    /// <summary>
    /// Returns the attendance history of a single staff member, most recent
    /// first — used by the employee "my history" endpoint (Requirement 5.5:
    /// employees only ever see their own records; the controller passes the
    /// staff id extracted from the caller's JWT).
    /// </summary>
    /// <param name="staffId">The authenticated employee's staff id.</param>
    /// <param name="fromDate">Optional inclusive lower bound.</param>
    /// <param name="toDate">Optional inclusive upper bound.</param>
    Task<IReadOnlyList<AttendanceHistoryEntry>> GetHistoryAsync(
        int staffId, DateOnly? fromDate = null, DateOnly? toDate = null);
}
