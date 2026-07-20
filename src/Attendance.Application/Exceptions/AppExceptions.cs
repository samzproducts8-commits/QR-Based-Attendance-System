namespace Attendance.Application.Exceptions;

/// <summary>
/// Thrown when a requested resource (staff, slot, department, …) does not exist.
/// Mapped to HTTP 404 by the API exception-handling middleware (Requirement 7.5).
/// </summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

/// <summary>
/// Thrown when an uploaded profile photo fails PNG validation
/// (extension, MIME type, or magic-byte check).
/// Mapped to HTTP 400 (Requirements 1.3, 1.4, 7.5).
/// </summary>
public sealed class PhotoValidationException : Exception
{
    public PhotoValidationException(string message) : base(message) { }
}

/// <summary>
/// Thrown when a staff member attempts to record the same attendance slot
/// twice on the same day — whether detected by the friendly pre-check or by
/// the database unique constraint on (StaffId, SlotId, EventDate).
/// Mapped to HTTP 409 (Requirements 3.10, 7.3, 7.5).
/// </summary>
public sealed class DuplicateAttendanceException : Exception
{
    public DuplicateAttendanceException(string message) : base(message) { }
}

/// <summary>
/// Thrown when a scan arrives at a time not covered by any active
/// attendance slot window.
/// Mapped to HTTP 422 (Requirements 3.11, 7.5).
/// </summary>
public sealed class OutsideScheduleException : Exception
{
    public OutsideScheduleException(string message) : base(message) { }
}

/// <summary>
/// Thrown when a staff registration or update supplies an email address
/// already used by another staff member.
/// Mapped to HTTP 409 (Requirements 1.7, 7.5).
/// </summary>
public sealed class DuplicateEmailException : Exception
{
    public DuplicateEmailException(string message) : base(message) { }
}

/// <summary>
/// Thrown when a QR token has already been consumed by a previous scan.
/// Mapped to HTTP 409 (Requirements 3.5, 7.5).
/// </summary>
public sealed class TokenAlreadyUsedException : Exception
{
    public TokenAlreadyUsedException(string message) : base(message) { }
}

/// <summary>
/// Thrown when a QR token's expiry window has elapsed.
/// Mapped to HTTP 410 (Requirements 3.5, 7.5).
/// </summary>
public sealed class TokenExpiredException : Exception
{
    public TokenExpiredException(string message) : base(message) { }
}

/// <summary>
/// Thrown when a slot configuration cannot be permanently deleted because
/// attendance logs still reference it. The admin should deactivate it instead.
/// Mapped to HTTP 409 (Requirement 7.5).
/// </summary>
public sealed class SlotInUseException : Exception
{
    public SlotInUseException(string message) : base(message) { }
}
