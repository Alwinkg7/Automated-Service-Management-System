using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ServiceApp.Core.Entities;

namespace ServiceApp.Api.Auth;

/// <summary>
/// JWT access tokens plus opaque, single-use refresh tokens.
/// Refresh tokens are stored hashed (SHA-256) in the existing Identity
/// <c>AspNetUserTokens</c> table, so no schema change is required.
/// Token format: <c>{userId}.{tokenId}.{secret}</c>.
/// </summary>
public class TokenService : ITokenService
{
    /// <summary>LoginProvider value of refresh-token rows in AspNetUserTokens.</summary>
    public const string RefreshProvider = "ASMS:RefreshToken";

    private readonly UserManager<ApplicationUser> _users;
    private readonly JwtSettings _settings;
    private readonly ILogger<TokenService> _logger;

    public TokenService(
        UserManager<ApplicationUser> users,
        IOptions<JwtSettings> settings,
        ILogger<TokenService> logger)
    {
        _users = users;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<AuthTokens> IssueAsync(ApplicationUser user)
    {
        var (accessToken, accessExpires) = CreateAccessToken(user);

        var tokenId = Guid.NewGuid().ToString("N");
        var secret = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var refreshExpires = DateTime.UtcNow.AddDays(_settings.RefreshTokenDays);

        await _users.SetAuthenticationTokenAsync(
            user, RefreshProvider, tokenId, $"{refreshExpires.Ticks}|{Hash(secret)}");

        return new AuthTokens(accessToken, accessExpires, $"{user.Id}.{tokenId}.{secret}", refreshExpires);
    }

    public async Task<(ApplicationUser User, AuthTokens Tokens)?> RefreshAsync(string refreshToken)
    {
        var lookup = await FindAsync(refreshToken);
        if (lookup is not { } found)
            return null;

        // Single use: the presented token is always consumed.
        await _users.RemoveAuthenticationTokenAsync(found.User, RefreshProvider, found.TokenId);

        if (!found.IsValid || !found.User.IsActive || await _users.IsLockedOutAsync(found.User))
            return null;

        return (found.User, await IssueAsync(found.User));
    }

    public async Task RevokeAsync(string refreshToken)
    {
        if (await FindAsync(refreshToken) is { } found)
            await _users.RemoveAuthenticationTokenAsync(found.User, RefreshProvider, found.TokenId);
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private (string Token, DateTime ExpiresAt) CreateAccessToken(ApplicationUser user)
    {
        var expires = DateTime.UtcNow.AddMinutes(_settings.AccessTokenMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("fullName", user.FullName ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    private async Task<(ApplicationUser User, string TokenId, bool IsValid)?> FindAsync(string refreshToken)
    {
        var parts = refreshToken.Split('.');
        if (parts.Length != 3 || parts.Any(string.IsNullOrEmpty))
            return null;

        var user = await _users.FindByIdAsync(parts[0]);
        if (user == null)
            return null;

        var stored = await _users.GetAuthenticationTokenAsync(user, RefreshProvider, parts[1]);
        if (stored == null)
        {
            _logger.LogWarning("Unknown or already-used refresh token presented for user {UserId}", user.Id);
            return null;
        }

        var segments = stored.Split('|');
        var isValid = segments.Length == 2
                      && long.TryParse(segments[0], out var ticks)
                      && new DateTime(ticks, DateTimeKind.Utc) > DateTime.UtcNow
                      && CryptographicOperations.FixedTimeEquals(
                          Encoding.ASCII.GetBytes(segments[1]),
                          Encoding.ASCII.GetBytes(Hash(parts[2])));

        return (user, parts[1], isValid);
    }

    private static string Hash(string secret) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
}
