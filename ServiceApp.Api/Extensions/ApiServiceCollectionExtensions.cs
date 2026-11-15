using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ServiceApp.Api.Auth;
using ServiceApp.Api.Infrastructure;
using ServiceApp.Core.Entities;

namespace ServiceApp.Api.Extensions;

/// <summary>API-host-only registrations. Shared domain wiring lives in ServiceApp.Hosting.</summary>
public static class ApiServiceCollectionExtensions
{
    public const string CorsPolicy = "Frontend";
    private const string HubPathPrefix = "/hubs";

    /// <summary>
    /// JWT bearer as the default scheme for authenticate/challenge/forbid (overriding
    /// the Identity cookie defaults), SignalR <c>access_token</c> support, and a
    /// per-request check that the user still exists and is active.
    /// </summary>
    public static IServiceCollection AddApiAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<RefreshTokenCleanupJob>();

        services.AddAuthentication(options =>
            {
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtSettings>>((options, jwt) =>
            {
                var settings = jwt.Value;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = settings.Issuer,
                    ValidAudience = settings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)),
                    ClockSkew = TimeSpan.FromMinutes(1)
                };

                options.Events = new JwtBearerEvents
                {
                    // Browsers cannot set headers on WebSockets: accept ?access_token= for the hub only.
                    OnMessageReceived = ctx =>
                    {
                        var token = ctx.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments(HubPathPrefix))
                            ctx.Token = token;
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = ValidateActiveUserAsync
                };
            });

        // The Identity cookie is only used to authorise the Hangfire dashboard;
        // it must never redirect API callers.
        services.ConfigureApplicationCookie(options =>
        {
            options.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
            options.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };
        });

        services.AddAuthorization();
        services.AddMemoryCache(o => o.SizeLimit = 10_000);
        return services;
    }

    /// <summary>
    /// Rejects tokens of deleted / deactivated / locked-out users. Result is cached
    /// for 30 seconds per user to avoid a DB hit on every request.
    /// </summary>
    private static async Task ValidateActiveUserAsync(TokenValidatedContext ctx)
    {
        var userId = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            ctx.Fail("Token has no subject.");
            return;
        }

        var cache = ctx.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
        var isActive = await cache.GetOrCreateAsync($"auth:active:{userId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
            entry.Size = 1;
            var users = ctx.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByIdAsync(userId);
            return user is { IsActive: true } && !await users.IsLockedOutAsync(user);
        });

        if (!isActive)
            ctx.Fail("User is inactive or no longer exists.");
    }

    /// <summary>
    /// CORS for the Next.js frontend: origins from <c>Cors:AllowedOrigins</c>
    /// (default http://localhost:3000). In Development any loopback origin is also
    /// allowed (Flutter web dev servers use random ports).
    /// </summary>
    public static IServiceCollection AddApiCors(this IServiceCollection services, IConfiguration configuration, IHostEnvironment env)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() is { Length: > 0 } configured
            ? configured
            : ["http://localhost:3000"];
        var allowed = new HashSet<string>(origins.Select(o => o.TrimEnd('/')), StringComparer.OrdinalIgnoreCase);

        services.AddCors(options => options.AddPolicy(CorsPolicy, policy => policy
            .SetIsOriginAllowed(origin =>
                allowed.Contains(origin)
                || (env.IsDevelopment() && Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.IsLoopback))
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .WithExposedHeaders("Retry-After", "Content-Disposition")));

        return services;
    }

    /// <summary>Named rate-limit policies; rejections return 429 ProblemDetails with Retry-After.</summary>
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(RateLimitPolicies.Auth, ctx =>
                RateLimitPartition.GetFixedWindowLimiter(ClientIp(ctx), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

            options.AddPolicy(RateLimitPolicies.Payments, ctx =>
                RateLimitPartition.GetTokenBucketLimiter(UserOrIp(ctx), _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 10,
                    TokensPerPeriod = 5,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

            // Global limiter for /api/* (chains with the named endpoint policies above).
            // Hubs, health, Swagger, Hangfire and the webhook are not limited.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
                !ctx.Request.Path.StartsWithSegments("/api") || ctx.Request.Path.StartsWithSegments("/api/payments/webhook")
                    ? RateLimitPartition.GetNoLimiter("unlimited")
                    : RateLimitPartition.GetSlidingWindowLimiter(UserOrIp(ctx), _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 120,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 6,
                    QueueLimit = 0
                }));

            options.OnRejected = async (ctx, ct) =>
            {
                if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    ctx.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();

                var problems = ctx.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                await problems.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = ctx.HttpContext,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too Many Requests",
                        Detail = "Rate limit exceeded. Retry later."
                    }
                });
            };
        });

        return services;
    }

    /// <summary>
    /// RFC 7807 everywhere. Every problem body also carries <c>traceId</c> and the
    /// legacy <c>success: false</c> / <c>error</c> fields existing clients read.
    /// </summary>
    public static IServiceCollection AddApiProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = ctx =>
        {
            var pd = ctx.ProblemDetails;
            pd.Instance ??= ctx.HttpContext.Request.Path;
            pd.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
            pd.Extensions["success"] = false;
            pd.Extensions["error"] = pd is HttpValidationProblemDetails v && v.Errors.Count > 0
                ? v.Errors.First().Value.FirstOrDefault() ?? pd.Title
                : pd.Detail ?? pd.Title;
        });
        services.AddExceptionHandler<GlobalExceptionHandler>();
        return services;
    }

    /// <summary>Controllers with string enums and the default ApiController validation (400 ValidationProblemDetails).</summary>
    public static IServiceCollection AddApiControllers(this IServiceCollection services)
    {
        services.AddControllers()
            .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        return services;
    }

    /// <summary>Swagger/OpenAPI with a Bearer security scheme (Development only).</summary>
    public static IServiceCollection AddApiSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "ASMS API", Version = "v1" });
            c.CustomSchemaIds(t => t.FullName?.Replace('+', '.'));

            var scheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste the access token from POST /api/auth/login.",
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            };
            c.AddSecurityDefinition("Bearer", scheme);
            c.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = Array.Empty<string>() });
        });
        return services;
    }

    /// <summary>The Hangfire processing server — registered only in the API host.</summary>
    public static IServiceCollection AddApiHangfireServer(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHangfireServer(options =>
        {
            options.WorkerCount = configuration.GetValue("Hangfire:WorkerCount", 2);
            options.Queues = [RecurringJobs.AutoAssignQueue, "default"];
        });
        return services;
    }

    private static string ClientIp(HttpContext ctx) =>
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static string UserOrIp(HttpContext ctx) =>
        ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) is { } id ? $"user:{id}" : $"ip:{ClientIp(ctx)}";
}
