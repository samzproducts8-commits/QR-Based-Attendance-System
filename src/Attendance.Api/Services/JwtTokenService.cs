using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Attendance.Infrastructure.Models;
using Microsoft.IdentityModel.Tokens;

namespace Attendance.Api.Services;

/// <summary>
/// Issues and validates the JWT pair (access + refresh) used by the Angular
/// SPA.  Access tokens are short-lived; refresh tokens are longer-lived JWTs
/// carrying a <c>token_type=refresh</c> claim and are rotated on every
/// refresh call (Requirement 5.1).
/// </summary>
public sealed class JwtTokenService
{
    /// <summary>Custom claim carrying the linked Staff primary key.</summary>
    public const string StaffIdClaim = "staffId";
    private const string TokenTypeClaim = "token_type";
    private const string RefreshTokenType = "refresh";

    private readonly string _issuer;
    private readonly string _audience;
    private readonly SymmetricSecurityKey _signingKey;
    private readonly TimeSpan _accessTokenLifetime;
    private readonly TimeSpan _refreshTokenLifetime;

    public JwtTokenService(IConfiguration configuration)
    {
        IConfigurationSection jwt = configuration.GetSection("Jwt");

        _issuer   = jwt["Issuer"] ?? "AttendanceApi";
        _audience = jwt["Audience"] ?? "AttendanceSpa";
        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            jwt["SecretKey"] ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.")));
        _accessTokenLifetime  = TimeSpan.FromMinutes(jwt.GetValue("AccessTokenMinutes", 15));
        _refreshTokenLifetime = TimeSpan.FromDays(jwt.GetValue("RefreshTokenDays", 7));
    }

    /// <summary>
    /// Creates the access token for an authenticated user.
    /// Claims: name identifier, user name, one role claim per role, and the
    /// linked <c>staffId</c> for Employee accounts (used by the scan endpoint
    /// to attribute attendance — Requirement 5.5).
    /// </summary>
    public (string Token, DateTime ExpiresAt) CreateAccessToken(
        ApplicationUser user, IEnumerable<string> roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? string.Empty)
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        if (user.StaffId is not null)
            claims.Add(new Claim(StaffIdClaim, user.StaffId.Value.ToString()));

        return CreateToken(claims, _accessTokenLifetime);
    }

    /// <summary>
    /// Creates the refresh token: same identity, no roles, marked with
    /// <c>token_type=refresh</c> so it can never be used as an access token
    /// against role-guarded endpoints.
    /// </summary>
    public (string Token, DateTime ExpiresAt) CreateRefreshToken(ApplicationUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(TokenTypeClaim, RefreshTokenType)
        };

        return CreateToken(claims, _refreshTokenLifetime);
    }

    /// <summary>
    /// Validates a refresh token and returns the user id it was issued to,
    /// or <see langword="null"/> when the token is invalid, expired, or not a
    /// refresh token.
    /// </summary>
    public string? ValidateRefreshToken(string refreshToken)
    {
        var handler = new JwtSecurityTokenHandler();
        try
        {
            ClaimsPrincipal principal = handler.ValidateToken(
                refreshToken,
                new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = _issuer,
                    ValidateAudience = true,
                    ValidAudience = _audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = _signingKey,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                },
                out _);

            bool isRefresh = principal.FindFirstValue(TokenTypeClaim) == RefreshTokenType;
            return isRefresh ? principal.FindFirstValue(ClaimTypes.NameIdentifier) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private (string Token, DateTime ExpiresAt) CreateToken(
        IEnumerable<Claim> claims, TimeSpan lifetime)
    {
        DateTime expiresAt = DateTime.UtcNow.Add(lifetime);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
