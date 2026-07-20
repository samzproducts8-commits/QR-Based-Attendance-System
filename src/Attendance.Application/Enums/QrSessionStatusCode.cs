namespace Attendance.Application.Enums;

/// <summary>
/// Application-layer mirror of <c>Attendance.Infrastructure.Enums.QrSessionStatus</c>.
/// Defined here so the Application layer has no compile-time dependency on
/// the Infrastructure assembly.
/// </summary>
public enum QrSessionStatusCode : byte
{
    Active  = 0,
    Used    = 1,
    Expired = 2
}
