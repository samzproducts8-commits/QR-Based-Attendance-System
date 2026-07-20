namespace Attendance.Application.Enums;

/// <summary>
/// Supported file formats for attendance report exports.
/// </summary>
public enum ExportFormat
{
    /// <summary>Excel spreadsheet (.xlsx)</summary>
    Xlsx = 0,

    /// <summary>PDF document (.pdf)</summary>
    Pdf = 1
}
