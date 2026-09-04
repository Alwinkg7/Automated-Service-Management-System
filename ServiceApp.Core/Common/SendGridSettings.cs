// =================================================================
//  SendGridSettings.cs — ServiceApp.Core/Common
//
//  Strongly typed config for SendGrid.
//  Bound from appsettings.json "SendGrid" section in Program.cs.
//  Injected via IOptions<SendGridSettings>.
// =================================================================

namespace ServiceApp.Core.Common
{
    public class SendGridSettings
    {
        public const string SectionName = "SendGrid";

        // Secret API key — NEVER send to frontend
        public string ApiKey { get; set; } = string.Empty;

        // The sender email address verified in SendGrid dashboard
        public string FromEmail { get; set; } = string.Empty;

        // Display name shown in email client
        public string FromName { get; set; } = string.Empty;
    }
}