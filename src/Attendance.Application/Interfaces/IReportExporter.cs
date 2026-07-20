using Attendance.Application.DTOs;
using Attendance.Application.Enums;

namespace Attendance.Application.Interfaces;

/// <summary>
/// Renders assembled report data into downloadable file formats
/// (Excel via ClosedXML, PDF via PdfSharpCore).
/// Defined in the Application layer so <c>AttendanceService</c> can export
/// without referencing the rendering libraries directly; the implementation
/// lives in Infrastructure where those packages are installed.
/// </summary>
/// <remarks>
/// Satisfies Requirement 4.3.
/// </remarks>
public interface IReportExporter
{
    /// <summary>
    /// Renders the daily attendance sheets of all staff for one date.
    /// </summary>
    /// <param name="sheets">One sheet per staff member.</param>
    /// <param name="date">The report date (used for titles/file headers).</param>
    /// <param name="format">Xlsx or Pdf.</param>
    /// <returns>The raw bytes of the generated file.</returns>
    byte[] ExportDaily(IReadOnlyList<DailyAttendanceSheet> sheets, DateOnly date, ExportFormat format);

    /// <summary>
    /// Renders a monthly summary report.
    /// </summary>
    /// <param name="summary">The aggregated monthly data.</param>
    /// <param name="format">Xlsx or Pdf.</param>
    /// <returns>The raw bytes of the generated file.</returns>
    byte[] ExportMonthly(MonthlySummary summary, ExportFormat format);
}
