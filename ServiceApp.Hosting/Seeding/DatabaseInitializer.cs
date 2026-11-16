using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServiceApp.Data.Context;

namespace ServiceApp.Hosting.Seeding;

/// <summary>
/// Optional EF Core migration at startup, then identity seeding. Migration runs only
/// when <c>Database:MigrateOnStartup</c> is true (set in the API's Development
/// settings) so the two hosts never race to migrate the same database.
/// </summary>
public static class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, IConfiguration configuration)
    {
        if (configuration.GetValue("Database:MigrateOnStartup", false))
        {
            using var scope = services.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseInitializer));
            try
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await db.Database.MigrateAsync();
                logger.LogInformation("Database migrations applied.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Applying database migrations failed; continuing startup.");
            }
        }
        else
        {
            await WarnIfMigrationsPendingAsync(services);
        }

        await services.SeedIdentityAsync();
    }

    private static async Task WarnIfMigrationsPendingAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseInitializer));
        try
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
            if (pending.Count > 0)
                logger.LogWarning(
                    "Database is missing {Count} migration(s): {Migrations}. Start ServiceApp.Api in Development " +
                    "(it migrates on startup) or run: dotnet ef database update --project ServiceApp.Data --startup-project ServiceApp.Web",
                    pending.Count, string.Join(", ", pending));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not check for pending database migrations.");
        }
    }
}
