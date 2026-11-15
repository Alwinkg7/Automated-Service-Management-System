using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ServiceApp.Hosting.Seeding;

/// <summary>Logs a warning (key names only, never values) for secrets that are not configured.</summary>
public static class StartupDiagnostics
{
    private static readonly string[] SharedSecretKeys =
    [
        "Razorpay:KeyId",
        "Razorpay:KeySecret",
        "Razorpay:WebhookSecret",
        "SendGrid:ApiKey",
        "SendGrid:FromEmail"
    ];

    public static void WarnOnMissingSecrets(IConfiguration configuration, ILogger logger, params string[] hostSpecificKeys)
    {
        foreach (var key in SharedSecretKeys.Concat(hostSpecificKeys))
        {
            if (string.IsNullOrWhiteSpace(configuration[key]))
                logger.LogWarning("Configuration value '{Key}' is not set (see README: user-secrets setup).", key);
        }
    }
}
