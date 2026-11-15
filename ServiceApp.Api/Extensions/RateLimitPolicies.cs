namespace ServiceApp.Api.Extensions;

/// <summary>Named rate-limiting policies (see <see cref="ApiServiceCollectionExtensions.AddApiRateLimiting"/>).</summary>
public static class RateLimitPolicies
{
    /// <summary>Login, register, refresh: 10 requests/minute per client IP.</summary>
    public const string Auth = "auth";

    /// <summary>Razorpay order/verify: token bucket per user.</summary>
    public const string Payments = "payments";

    /// <summary>Global limiter for /api/*: 120 requests/minute per user or IP (not an endpoint policy).</summary>
    public const string GeneralApi = "general-api";
}
