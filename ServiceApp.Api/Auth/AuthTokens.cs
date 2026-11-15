namespace ServiceApp.Api.Auth;

/// <summary>An issued access/refresh token pair.</summary>
public record AuthTokens(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt);
