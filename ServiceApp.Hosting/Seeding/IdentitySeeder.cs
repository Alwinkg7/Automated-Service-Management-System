using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;
using ServiceApp.Hosting.Options;

namespace ServiceApp.Hosting.Seeding;

/// <summary>
/// Idempotent startup seeding: ensures the three roles exist and creates the first
/// admin when <c>Seed:AdminPassword</c> is configured and that admin is missing.
/// Failures are logged, never fatal, so a host can still start (e.g. DB offline).
/// </summary>
public static class IdentitySeeder
{
    private static readonly string[] Roles = ["Admin", "Technician", "Customer"];

    public static async Task SeedIdentityAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(IdentitySeeder));

        try
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var settings = scope.ServiceProvider.GetRequiredService<IOptions<SeedSettings>>().Value;

            foreach (var role in Roles)
            {
                if (await roleManager.RoleExistsAsync(role)) continue;
                await roleManager.CreateAsync(new IdentityRole(role));
                logger.LogInformation("Role created: {Role}", role);
            }

            if (await userManager.FindByEmailAsync(settings.AdminEmail) != null)
                return;

            if (string.IsNullOrWhiteSpace(settings.AdminPassword))
            {
                logger.LogWarning(
                    "No admin account {Email} exists and Seed:AdminPassword is not configured; skipping admin seed.",
                    settings.AdminEmail);
                return;
            }

            var admin = new ApplicationUser
            {
                UserName = settings.AdminEmail,
                Email = settings.AdminEmail,
                FullName = settings.AdminFullName,
                Phone = "0000000000",
                Role = UserRole.Admin,
                EmailConfirmed = true,
                IsActive = true
            };

            var result = await userManager.CreateAsync(admin, settings.AdminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, "Admin");
                logger.LogInformation("Default admin seeded: {Email}", settings.AdminEmail);
            }
            else
            {
                foreach (var err in result.Errors)
                    logger.LogError("Admin seed failed: {Error}", err.Description);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Identity seeding failed; continuing startup.");
        }
    }
}
