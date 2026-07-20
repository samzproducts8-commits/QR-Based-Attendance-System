using Attendance.Application.Enums;

namespace Attendance.Application.Models;

/// <summary>
/// A lightweight, layer-agnostic projection of a <c>QrSession</c> row returned
/// by <see cref="Interfaces.IQrSessionRepository"/>.
/// Carries only the fields needed by <c>QrSessionService</c> to classify a
/// consume outcome (Success / AlreadyUsed / Expired) without creating a
/// dependency from Application on the Infrastructure data model.
/// </summary>
public sealed record 
QrSessionSnapshot(
    int QrSessionId,
    Guid TokenValue,
    DateTime GeneratedAt,
    DateTime ExpiresAt,
    QrSessionStatusCode Status,
    int? UsedByStaffId,
    DateTime? UsedAt
);
