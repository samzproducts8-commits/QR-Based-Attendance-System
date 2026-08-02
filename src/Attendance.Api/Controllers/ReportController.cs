using Attendance.Application.DTOs;
using Attendance.Application.Enums;
using Attendance.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Attendance.Api.Controllers;

/// <summary>
/// Daily and monthly attendance reports plus Excel/PDF exports
/// (Requirements 4.1–4.5, 5.4).
/// </summary>
[ApiController]
[Route("api/reports")]
[Authorize(Roles = "Admin,HR")]
public sealed class ReportController : ControllerBase
{
    private const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string PdfContentType = "application/pdf";
    private const string CsvContentType = "text/csv";

    private readonly IAttendanceService _attendanceService;

    public ReportController(IAttendanceService attendanceService)
    {
        _attendanceService = attendanceService;
    }

    /// <summary>
    /// Returns real-time metrics and recent activities for the live HR dashboard.
    /// </summary>
    [HttpGet("live-dashboard")]
    [ProducesResponseType<LiveDashboardMetricsDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> LiveDashboard()
    {
        return Ok(await _attendanceService.GetLiveDashboardMetricsAsync());
    }

    /// <summary>
    /// Returns monthly aggregated payroll summary formatted for standard payroll processing.
    /// </summary>
    [HttpGet("payroll")]
    [ProducesResponseType<MonthlyPayrollSummaryDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Payroll(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] int? departmentId = null)
    {
        if (month is < 1 or > 12)
            return BadRequest(new { error = "Month must be between 1 and 12." });

        return Ok(await _attendanceService.GetMonthlyPayrollSummaryAsync(year, month, departmentId));
    }

    /// <summary>
    /// Streams the monthly payroll summary as a downloadable .csv or .xlsx file.
    /// </summary>
    [HttpGet("payroll/export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportPayroll(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] string format = "csv",
        [FromQuery] int? departmentId = null)
    {
        if (month is < 1 or > 12)
            return BadRequest(new { error = "Month must be between 1 and 12." });

        if (!TryParseFormat(format, out ExportFormat exportFormat))
            return BadRequest(new { error = "Format must be 'csv' or 'xlsx'." });

        byte[] bytes = await _attendanceService.ExportPayrollSummaryAsync(year, month, exportFormat, departmentId);
        return FileResult(bytes, exportFormat, $"payroll-summary-{year}-{month:D2}");
    }

    /// <summary>
    /// Daily attendance sheet(s): one staff member when <paramref name="staffId"/>
    /// is given, otherwise all active staff.
    /// </summary>
    [HttpGet("daily")]
    [ProducesResponseType<IReadOnlyList<DailyAttendanceSheet>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Daily(
        [FromQuery] DateOnly date,
        [FromQuery] int? staffId = null)
    {
        if (staffId is not null)
            return Ok(new[] { await _attendanceService.GetDailySheetAsync(staffId.Value, date) });

        return Ok(await _attendanceService.GetDailySheetsAsync(date));
    }

    /// <summary>Monthly summary, optionally filtered by staff or department.</summary>
    [HttpGet("monthly")]
    [ProducesResponseType<MonthlySummary>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Monthly(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] int? staffId = null,
        [FromQuery] int? departmentId = null)
    {
        if (month is < 1 or > 12)
            return BadRequest(new { error = "Month must be between 1 and 12." });

        return Ok(await _attendanceService.GetMonthlySummaryAsync(staffId, departmentId, year, month));
    }

    /// <summary>Streams the daily report as a downloadable .xlsx or .pdf file.</summary>
    [HttpGet("daily/export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportDaily(
        [FromQuery] DateOnly date,
        [FromQuery] string format = "xlsx")
    {
        if (!TryParseFormat(format, out ExportFormat exportFormat))
            return BadRequest(new { error = "Format must be 'xlsx' or 'pdf'." });

        byte[] bytes = await _attendanceService.ExportDailyReportAsync(date, exportFormat);
        return FileResult(bytes, exportFormat, $"daily-attendance-{date:yyyy-MM-dd}");
    }

    /// <summary>Streams the monthly report as a downloadable .xlsx or .pdf file.</summary>
    [HttpGet("monthly/export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportMonthly(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] string format = "xlsx")
    {
        if (month is < 1 or > 12)
            return BadRequest(new { error = "Month must be between 1 and 12." });

        if (!TryParseFormat(format, out ExportFormat exportFormat))
            return BadRequest(new { error = "Format must be 'xlsx' or 'pdf'." });

        byte[] bytes = await _attendanceService.ExportMonthlyReportAsync(year, month, exportFormat);
        return FileResult(bytes, exportFormat, $"monthly-attendance-{year}-{month:D2}");
    }

    /// <summary>Sets or updates the absence reason for an employee's missed mandatory slot.</summary>
    [HttpPut("absence-reason")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SetAbsenceReason([FromBody] SetAbsenceReasonDto dto)
    {
        await _attendanceService.SetAbsenceReasonAsync(dto);
        return NoContent();
    }

    private static bool TryParseFormat(string format, out ExportFormat exportFormat)
    {
        switch (format.ToLowerInvariant())
        {
            case "csv":  exportFormat = ExportFormat.Csv;  return true;
            case "xlsx": exportFormat = ExportFormat.Xlsx; return true;
            case "pdf":  exportFormat = ExportFormat.Pdf;  return true;
            default:     exportFormat = default;           return false;
        }
    }

    private FileContentResult FileResult(byte[] bytes, ExportFormat format, string baseName)
        => format switch
        {
            ExportFormat.Csv  => File(bytes, CsvContentType, $"{baseName}.csv"),
            ExportFormat.Xlsx => File(bytes, XlsxContentType, $"{baseName}.xlsx"),
            _                 => File(bytes, PdfContentType, $"{baseName}.pdf")
        };
}
