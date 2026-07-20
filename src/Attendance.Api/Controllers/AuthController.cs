using Attendance.Api.Services;
using Attendance.Application.DTOs;
using Attendance.Infrastructure.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Attendance.Api.Controllers;

/// <summary>
/// Issues and refreshes JWT tokens against the ASP.NET Identity store
/// (Requirement 5.1).
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly JwtTokenService _tokenService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        JwtTokenService tokenService,
        ILogger<AuthController> logger)
    {
        _userManager  = userManager;
        _tokenService = tokenService;
        _logger       = logger;
    }

    /// <summary>Validates credentials and returns an access/refresh token pair.</summary>
    /// <response code="200">Credentials valid — token pair issued.</response>
    /// <response code="401">Unknown user or wrong password.</response>
    [HttpPost("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        ApplicationUser? user = await _userManager.FindByNameAsync(request.Username);

        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            _logger.LogWarning("Failed login attempt for user '{Username}' from {Ip}.",
                request.Username, HttpContext.Connection.RemoteIpAddress);
            return Unauthorized(new { error = "Invalid username or password." });
        }

        return Ok(await BuildAuthResponseAsync(user));
    }

    /// <summary>
    /// Exchanges a valid refresh token for a brand-new access/refresh pair
    /// (refresh token rotation).
    /// </summary>
    /// <response code="200">Refresh token valid — new pair issued.</response>
    /// <response code="401">Refresh token invalid or expired.</response>
    [HttpPost("refresh")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        string? userId = _tokenService.ValidateRefreshToken(request.RefreshToken);
        if (userId is null)
            return Unauthorized(new { error = "Invalid or expired refresh token." });

        ApplicationUser? user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return Unauthorized(new { error = "User no longer exists." });

        return Ok(await BuildAuthResponseAsync(user));
    }

    private async Task<AuthResponse> BuildAuthResponseAsync(ApplicationUser user)
    {
        IList<string> roles = await _userManager.GetRolesAsync(user);

        (string accessToken, DateTime expiresAt) = _tokenService.CreateAccessToken(user, roles);
        (string refreshToken, _) = _tokenService.CreateRefreshToken(user);

        return new AuthResponse(
            accessToken,
            expiresAt,
            refreshToken,
            user.UserName ?? string.Empty,
            roles.ToList(),
            user.StaffId);
    }
}
