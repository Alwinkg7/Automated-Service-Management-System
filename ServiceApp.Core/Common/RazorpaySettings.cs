// =================================================================
//  RazorpaySettings.cs — ServiceApp.Core/Common
//
//  Strongly-typed configuration for Razorpay.
//  Registered in Program.cs via IOptions<RazorpaySettings>.
//  Values come from appsettings.json → "Razorpay" section.
//
//  NEVER hardcode API keys in source code.
//  In production: use Azure Key Vault or environment variables.
// =================================================================

namespace ServiceApp.Core.Common
{
    public class RazorpaySettings
    {
        // The section name in appsettings.json
        public const string SectionName = "Razorpay";

        // Public key — sent to frontend, used to init Razorpay JS
        public string KeyId { get; set; } = string.Empty;

        // Secret key — NEVER sent to frontend
        // Used server-side to create orders + verify signatures
        public string KeySecret { get; set; } = string.Empty;

        // Currency code — INR for India
        public string Currency { get; set; } = "INR";

        // Used to verify webhook calls from Razorpay
        public string WebhookSecret { get; set; } = string.Empty;
    }
}