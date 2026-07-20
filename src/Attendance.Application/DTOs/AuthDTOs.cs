namespace Attendance.Application.DTOs;

/// <summary>
/// Credentials payload for <c>POST /api/auth/login</c> (Requirement 5.1).
/// </summary>
/// <param name="Username">Identity user name (e.g. "admin" or a staff EMP-XXXX code).</param>
/// <param name="Password">Plain-text password, verified against the Identity store.</param>
public record LoginRequest(string Username, string Password);

/// <summary>
/// Payload for <c>POST /api/auth/refresh</c> — refresh token rotation
/// (Requirement 5.1).
/// </summary>
/// <param name="RefreshToken">The refresh JWT issued alongside the last access token.</param>
public record RefreshRequest(string RefreshToken);

/// <summary>
/// Successful authentication response carrying the JWT pair and the
/// caller's identity summary.
/// </summary>
/// <param name="AccessToken">Short-lived JWT bearer token for API calls.</param>
/// <param name="ExpiresAt">UTC expiry of the access token.</param>
/// <param name="RefreshToken">Longer-lived JWT used to obtain a new pair.</param>
/// <param name="Username">Authenticated user name.</param>
/// <param name="Roles">Role names assigned to the user (Admin / HR / Employee).</param>
/// <param name="StaffId">
/// Linked <c>Staff.StaffId</c> for Employee-role users; <see langword="null"/>
/// for pure admin/HR accounts with no staff record.
/// </param>
public record AuthResponse(
    string AccessToken,
    DateTime ExpiresAt,
    string RefreshToken,
    string Username,
    IReadOnlyList<string> Roles,
    int? StaffId
);
