using ServiceApp.Core.Entities;

namespace ServiceApp.Api.Auth;

/// <summary>Issues JWT access tokens and rotating refresh tokens.</summary>
public interface ITokenService
{
    /// <summary>Issues a new access token and a new refresh token for the user.</summary>
    Task<AuthTokens> IssueAsync(ApplicationUser user);

    /// <summary>
    /// Validates and consumes a refresh token (single use) and issues a new pair.
    /// Returns null when the token is unknown, expired, revoked or the user is inactive.
    /// </summary>
    Task<(ApplicationUser User, AuthTokens Tokens)?> RefreshAsync(string refreshToken);

    /// <summary>Revokes a refresh token. Unknown tokens are ignored.</summary>
    Task RevokeAsync(string refreshToken);
}
