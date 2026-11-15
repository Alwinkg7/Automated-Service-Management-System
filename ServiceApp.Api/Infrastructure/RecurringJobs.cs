using Hangfire;
using ServiceApp.Core.Interfaces;

namespace ServiceApp.Api.Infrastructure;

/// <summary>Recurring Hangfire jobs. Registered only by the API host (the only Hangfire server).</summary>
public static class RecurringJobs
{
    public const string AutoAssignQueue = "auto-assign";

    public static void Register(IServiceProvider services, ILogger logger)
    {
        try
        {
            var manager = services.GetRequiredService<IRecurringJobManager>();
            manager.AddOrUpdate<IAutoAssignmentService>(
                "auto-assign",
                AutoAssignQueue,
                service => service.RunAsync(),
                Cron.MinuteInterval(2));

            logger.LogInformation("Hangfire auto-assignment job registered (every 2 minutes)");
        }
        catch (Exception ex)
        {
            // Storage unreachable at startup: the host still serves requests; the
            // job is registered on the next successful start.
            logger.LogError(ex, "Registering recurring Hangfire jobs failed.");
        }
    }
}
