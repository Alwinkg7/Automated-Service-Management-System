namespace ServiceApp.Hosting.Options;

/// <summary>
/// Startup seeding. Roles are always ensured; the first admin is only created
/// when <see cref="AdminPassword"/> is configured (user-secrets / env var).
/// </summary>
public class SeedSettings
{
    public const string SectionName = "Seed";

    public string AdminEmail { get; set; } = "admin@serviceapp.com";
    public string? AdminPassword { get; set; }
    public string AdminFullName { get; set; } = "System Admin";
}
