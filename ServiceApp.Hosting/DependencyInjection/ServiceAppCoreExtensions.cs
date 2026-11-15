using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure;
using ServiceApp.Core.Common;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Interfaces;
using ServiceApp.Data;
using ServiceApp.Data.Context;
using ServiceApp.Hosting.Options;
using ServiceApp.Hosting.Realtime;
using ServiceApp.Infrastructure;
using ServiceApp.Infrastructure.Pdf;
using ServiceApp.Services.Implementations;
using StackExchange.Redis;

namespace ServiceApp.Hosting.DependencyInjection;

/// <summary>
/// Shared composition used by both hosts (ServiceApp.Api and ServiceApp.Web) so
/// they run on exactly the same domain, data and infrastructure registrations.
/// Host-specific concerns (authentication schemes, MVC vs. API controllers,
/// Hangfire server) stay in each host's Program.cs.
/// </summary>
public static class ServiceAppCoreExtensions
{
    public const string ConnectionStringName = "DefaultConnection";

    /// <summary>
    /// Registers EF Core, ASP.NET Identity, repositories/UoW, domain services,
    /// infrastructure adapters (Razorpay, SendGrid, QuestPDF), validated options,
    /// shared Data Protection and the database health check.
    /// </summary>
    public static IServiceCollection AddServiceAppCore(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = GetRequiredConnectionString(configuration);

        // ── Persistence ──
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.MigrationsAssembly("ServiceApp.Data")));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // ── Identity (schemes/cookies are configured per host) ──
        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireUppercase = true;
                options.Password.RequireDigit = true;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.AllowedForNewUsers = true;

                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        // Deactivation rotates the security stamp; re-validate cookies every minute.
        services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(1));

        // Same application name (and optionally a shared key ring) lets both hosts
        // read each other's Identity cookie, e.g. for the Hangfire dashboard.
        var dataProtection = services.AddDataProtection().SetApplicationName("ASMS");
        var keysPath = configuration["DataProtection:KeysPath"];
        if (!string.IsNullOrWhiteSpace(keysPath))
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));

        // ── Options ──
        // Secrets are not validated on start so a developer can run without e.g. SendGrid;
        // StartupDiagnostics logs a warning for every missing secret instead.
        services.Configure<RazorpaySettings>(configuration.GetSection(RazorpaySettings.SectionName));
        services.Configure<SendGridSettings>(configuration.GetSection(SendGridSettings.SectionName));
        services.Configure<AutoAssignmentSettings>(configuration.GetSection(AutoAssignmentSettings.SectionName));
        services.Configure<SeedSettings>(configuration.GetSection(SeedSettings.SectionName));

        // ── Domain services ──
        services.AddScoped<IServiceRequestService, ServiceRequestService>();
        services.AddScoped<IBillService, BillService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<ITechnicianService, TechnicianService>();
        services.AddScoped<IAutoAssignmentService, AutoAssignmentService>();
        services.AddScoped<IPromotionService, PromotionService>();
        services.AddScoped<IDisputeService, DisputeService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IWalletService, WalletService>();

        // ── Infrastructure ──
        services.AddHttpClient<IRazorpayService, RazorpayService>(client =>
            client.Timeout = TimeSpan.FromSeconds(30));
        services.AddScoped<IEmailService, EmailService>();

        QuestPDF.Settings.License = LicenseType.Community;
        services.AddScoped<IBillPdfService, BillPdfService>();

        services.AddHealthChecks().AddDbContextCheck<ApplicationDbContext>("database");

        return services;
    }

    /// <summary>
    /// Registers SignalR, the shared <see cref="ServiceHub"/> notification service and,
    /// when <c>SignalR:RedisConnectionString</c> is configured, a Redis backplane so
    /// notifications reach clients of both hosts.
    /// </summary>
    public static ISignalRServerBuilder AddServiceAppRealtime(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection(RealtimeSettings.SectionName).Get<RealtimeSettings>()
                       ?? new RealtimeSettings();

        var signalR = services.AddSignalR();
        if (!string.IsNullOrWhiteSpace(settings.RedisConnectionString))
        {
            signalR.AddStackExchangeRedis(settings.RedisConnectionString, options =>
                options.Configuration.ChannelPrefix = RedisChannel.Literal(settings.ChannelPrefix));
        }

        services.AddScoped<INotificationService, SignalRNotificationService>();
        return signalR;
    }

    /// <summary>
    /// Registers Hangfire SQL Server storage and the job client (enqueue only).
    /// The processing server is added by the API host alone.
    /// </summary>
    public static IServiceCollection AddServiceAppHangfireStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = GetRequiredConnectionString(configuration);

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
            {
                CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                QueuePollInterval = TimeSpan.Zero,
                UseRecommendedIsolationLevel = true,
                DisableGlobalLocks = true
            }));

        return services;
    }

    private static string GetRequiredConnectionString(IConfiguration configuration) =>
        configuration.GetConnectionString(ConnectionStringName) is { Length: > 0 } cs
            ? cs
            : throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured. " +
                "Set it with: dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"<value>\"");
}
