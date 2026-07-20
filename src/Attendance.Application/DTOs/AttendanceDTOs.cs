namespace Attendance.Application.DTOs;

/// <summary>
/// Payload submitted by an employee when scanning a QR code.
/// Satisfies Requirement 3.1 — token carries no employee identity.
/// </summary>
/// <param name="Token">The GUID value decoded from the scanned QR code.</param>
public record ScanRequestDto(Guid Token);

/// <summary>
/// Result returned to the employee's device after a successful scan.
/// Satisfies Requirement 3.12.
/// </summary>
/// <param name="StaffName">Full name of the authenticated staff member.</param>
/// <param name="SlotName">Name of the attendance slot that was recorded (e.g. "MorningIn").</param>
/// <param name="EventTimestamp">UTC timestamp at which the attendance event was recorded.</param>
/// <param name="StatusLabel">Human-readable status: "On Time" or "Late".</param>
/// <param name="GreetingMessage">
/// Personalised greeting including the staff name, slot, time, and status
/// (e.g. "Good morning, John — MorningIn recorded at 08:03 AM. On Time!").
/// </param>
public record AttendanceRecordDto(
    string StaffName,
    string SlotName,
    DateTime EventTimestamp,
    string StatusLabel,
    string GreetingMessage
);

/// <summary>
/// Response returned after generating a new QR session token.
/// Satisfies Requirements 3.1 and 3.2.
/// </summary>
/// <param name="TokenValue">The new single-use GUID token embedded in the QR code.</param>
/// <param name="QrImageBase64">Base64-encoded PNG image of the QR code for display on the kiosk.</param>
/// <param name="ExpiresAt">UTC timestamp at which this token expires (10–15 s from generation).</param>
public record QrCodeResponseDto(
    Guid TokenValue,
    string QrImageBase64,
    DateTime ExpiresAt
);

/// <summary>
/// One row of an employee's own attendance history
/// (GET /api/attendance/my-history — Requirement 5.5).
/// </summary>
/// <param name="AttendanceLogId">Log primary key.</param>
/// <param name="EventDate">Calendar date of the event.</param>
/// <param name="SlotName">Human-readable slot name (e.g. "MorningIn").</param>
/// <param name="EventTimestamp">UTC timestamp of the recorded scan.</param>
/// <param name="StatusLabel">"On Time", "Late", or "Manual Entry".</param>
public record AttendanceHistoryEntry(
    long AttendanceLogId,
    DateOnly EventDate,
    string SlotName,
    DateTime EventTimestamp,
    string StatusLabel
);
