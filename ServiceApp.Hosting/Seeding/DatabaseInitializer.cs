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

        await services.SeedIdentityAsync();
    }
}
