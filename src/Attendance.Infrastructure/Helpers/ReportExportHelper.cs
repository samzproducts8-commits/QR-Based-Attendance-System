using Attendance.Application.DTOs;
using Attendance.Application.Enums;
using Attendance.Application.Interfaces;
using ClosedXML.Excel;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

namespace Attendance.Infrastructure.Helpers;

/// <summary>
/// Renders daily and monthly attendance reports as Excel (ClosedXML) or
/// PDF (PdfSharpCore) files.
/// </summary>
/// <remarks>
/// Satisfies Requirement 4.3.
/// </remarks>
public sealed class ReportExportHelper : IReportExporter
{
    private const string PdfFont = "Arial";

    // -------------------------------------------------------------------------
    // Daily report
    // -------------------------------------------------------------------------

    /// <inheritdoc />
    public byte[] ExportDaily(
        IReadOnlyList<DailyAttendanceSheet> sheets, DateOnly date, ExportFormat format)
    {
        // Column layout: Staff | one column per slot (union of all slot names, by sheet order)
        List<string> slotNames = sheets
            .SelectMany(s => s.Entries.Select(e => e.SlotName))
            .Distinct()
            .ToList();

        string title = $"Daily Attendance — {date:yyyy-MM-dd}";
        var header = new List<string> { "Staff" };
        header.AddRange(slotNames);

        var rows = sheets.Select(sheet =>
        {
            var row = new List<string> { sheet.StaffName };
            foreach (string slotName in slotNames)
            {
                DailySlotEntry? entry = sheet.Entries.FirstOrDefault(e => e.SlotName == slotName);
                if (entry is null)
                {
                    row.Add("-");
                }
                else if (entry.EventTimestamp is not null)
                {
                    row.Add($"{entry.EventTimestamp:HH:mm} ({entry.StatusLabel})");
                }
                else
                {
                    // Use admin-provided absence reason when available; fall back to status label
                    row.Add(!string.IsNullOrWhiteSpace(entry.AbsenceReason)
                        ? entry.AbsenceReason
                        : entry.StatusLabel);
                }
            }
            return row;
        }).ToList();

        return format == ExportFormat.Xlsx
            ? BuildXlsx(title, "Daily", header, rows)
            : BuildPdf(title, header, rows);
    }

    // -------------------------------------------------------------------------
    // Monthly report
    // -------------------------------------------------------------------------

    /// <inheritdoc />
    public byte[] ExportMonthly(MonthlySummary summary, ExportFormat format)
    {
        string title = $"Monthly Attendance Summary — {summary.Year}-{summary.Month:D2}";

        // One row per staff × slot with OnTime / Late / Absent counts.
        var header = new List<string> { "Staff", "Department", "Slot", "On Time", "Late", "Absent" };

        var rows = summary.StaffSummaries
            .SelectMany(staff => staff.SlotSummaries.Select(slot => new List<string>
            {
                staff.StaffName,
                staff.Department,
                slot.SlotName,
                slot.OnTimeCount.ToString(),
                slot.LateCount.ToString(),
                slot.AbsentCount.ToString()
            }))
            .ToList();

        return format == ExportFormat.Xlsx
            ? BuildXlsx(title, "Monthly", header, rows)
            : BuildPdf(title, header, rows);
    }

    // -------------------------------------------------------------------------
    // Payroll Summary Export (CSV / Excel)
    // -------------------------------------------------------------------------

    /// <inheritdoc />
    public byte[] ExportPayroll(MonthlyPayrollSummaryDto summary, ExportFormat format)
    {
        string title = $"Monthly Payroll Summary — {summary.Year}-{summary.Month:D2}";
        var header = new List<string>
        {
            "Employee Code",
            "Employee Name",
            "Department",
            "Total Days Worked",
            "Total Hours",
            "Overtime Hours",
            "Late Penalties",
            "Unpaid Absences"
        };

        var rows = summary.StaffSummaries.Select(staff => new List<string>
        {
            staff.UniqueCode,
            staff.FullName,
            staff.Department,
            staff.TotalDaysWorked.ToString(),
            staff.TotalHours.ToString("F2"),
            staff.OvertimeHours.ToString("F2"),
            staff.LatePenalties.ToString(),
            staff.UnpaidAbsences.ToString()
        }).ToList();

        if (format == ExportFormat.Csv)
        {
            return BuildCsv(header, rows);
        }

        // Include summary row for Excel
        var summaryRow = new List<string>
        {
            "TOTAL",
            $"{summary.TotalStaff} Staff",
            "-",
            summary.TotalDaysWorked.ToString(),
            summary.TotalHoursWorked.ToString("F2"),
            summary.TotalOvertimeHours.ToString("F2"),
            summary.TotalLatePenalties.ToString(),
            summary.TotalUnpaidAbsences.ToString()
        };

        var allRows = new List<List<string>>(rows) { summaryRow };
        return BuildXlsx(title, "Payroll Summary", header, allRows);
    }

    private static byte[] BuildCsv(List<string> header, List<List<string>> rows)
    {
        var sb = new System.Text.StringBuilder();

        // Write header
        sb.AppendLine(string.Join(",", header.Select(EscapeCsvField)));

        // Write rows
        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(",", row.Select(EscapeCsvField)));
        }

        return System.Text.Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field)) return "";
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
    }

    // -------------------------------------------------------------------------
    // Excel rendering (ClosedXML)
    // -------------------------------------------------------------------------

    private static byte[] BuildXlsx(
        string title, string sheetName, List<string> header, List<List<string>> rows)
    {
        using var workbook = new XLWorkbook();
        IXLWorksheet ws = workbook.Worksheets.Add(sheetName);

        ws.Cell(1, 1).Value = title;
        ws.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);
        ws.Range(1, 1, 1, header.Count).Merge();

        for (int c = 0; c < header.Count; c++)
        {
            IXLCell cell = ws.Cell(3, c + 1);
            cell.Value = header[c];
            cell.Style.Font.SetBold();
            cell.Style.Fill.SetBackgroundColor(XLColor.LightGray);
        }

        for (int r = 0; r < rows.Count; r++)
        {
            for (int c = 0; c < rows[r].Count; c++)
                ws.Cell(4 + r, c + 1).Value = rows[r][c];
        }

        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    // -------------------------------------------------------------------------
    // PDF rendering (PdfSharpCore) — simple tabular layout with page breaks
    // -------------------------------------------------------------------------

    private static byte[] BuildPdf(
        string title, List<string> header, List<List<string>> rows)
    {
        const double margin = 40;
        const double rowHeight = 18;

        var document = new PdfDocument();
        document.Info.Title = title;

        var titleFont  = new XFont(PdfFont, 14, XFontStyle.Bold);
        var headerFont = new XFont(PdfFont, 9, XFontStyle.Bold);
        var cellFont   = new XFont(PdfFont, 9, XFontStyle.Regular);

        PdfPage page = document.AddPage();
        XGraphics gfx = XGraphics.FromPdfPage(page);

        double usableWidth = page.Width - 2 * margin;
        double colWidth = usableWidth / header.Count;
        double y = margin;

        gfx.DrawString(title, titleFont, XBrushes.Black,
            new XRect(margin, y, usableWidth, rowHeight), XStringFormats.TopLeft);
        y += rowHeight * 2;

        DrawRow(gfx, header, headerFont, margin, y, colWidth, rowHeight);
        y += rowHeight;

        foreach (List<string> row in rows)
        {
            if (y + rowHeight > page.Height - margin)
            {
                gfx.Dispose();
                page = document.AddPage();
                gfx = XGraphics.FromPdfPage(page);
                y = margin;
                DrawRow(gfx, header, headerFont, margin, y, colWidth, rowHeight);
                y += rowHeight;
            }

            DrawRow(gfx, row, cellFont, margin, y, colWidth, rowHeight);
            y += rowHeight;
        }

        gfx.Dispose();

        using var stream = new MemoryStream();
        document.Save(stream, closeStream: false);
        return stream.ToArray();
    }

    private static void DrawRow(
        XGraphics gfx, List<string> cells, XFont font,
        double x, double y, double colWidth, double rowHeight)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            gfx.DrawString(cells[i], font, XBrushes.Black,
                new XRect(x + i * colWidth + 2, y, colWidth - 4, rowHeight),
                XStringFormats.TopLeft);
        }
    }
}
