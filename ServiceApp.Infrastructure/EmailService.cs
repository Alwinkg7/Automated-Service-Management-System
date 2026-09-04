// =================================================================
//  EmailService.cs — ServiceApp.Infrastructure
//
//  Sends HTML emails via SendGrid API.
//
//  EACH EMAIL METHOD:
//  1. Builds an HTML body using BuildHtmlEmail() helper
//  2. Creates a SendGridMessage
//  3. Calls _client.SendEmailAsync()
//  4. Logs the result (success or failure)
//
//  HTML EMAIL TEMPLATE:
//  All emails share one branded wrapper (ComposeWrapper) with:
//    - ServiceApp header bar (indigo)
//    - White content card
//    - CTA button (optional)
//    - Footer with support email
//
//  IMPORTANT:
//  SendGrid requires your FROM email to be verified as a
//  Single Sender in the SendGrid dashboard before it will
//  deliver any emails. Do this first at:
//  app.sendgrid.com → Settings → Sender Authentication
// =================================================================

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;
using ServiceApp.Core.Common;
using ServiceApp.Core.Interfaces;

namespace ServiceApp.Infrastructure
{
    public class EmailService : IEmailService
    {
        private readonly SendGridSettings _settings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(
            IOptions<SendGridSettings> settings,
            ILogger<EmailService> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        // =============================================================
        //  1. Request confirmation → Customer
        // =============================================================
        public async Task SendRequestConfirmationAsync(
            string toEmail,
            string customerName,
            int requestId,
            string category,
            string preferredDateTime)
        {
            var subject = $"Request #{requestId} received — ServiceApp";
            var body = ComposeWrapper(
                heading: $"We received your request!",
                preheader: $"Your {category} request #{requestId} is in the queue.",
                bodyHtml: $@"
<p>Hi <strong>{customerName}</strong>,</p>
<p>Your <strong>{category}</strong> service request has been submitted successfully.</p>
<table style='width:100%;border-collapse:collapse;margin:16px 0'>
  <tr>
    <td style='padding:8px;color:#6B7280;font-size:13px'>Request ID</td>
    <td style='padding:8px;font-weight:500'>#{requestId}</td>
  </tr>
  <tr style='background:#F9FAFB'>
    <td style='padding:8px;color:#6B7280;font-size:13px'>Service</td>
    <td style='padding:8px;font-weight:500'>{category}</td>
  </tr>
  <tr>
    <td style='padding:8px;color:#6B7280;font-size:13px'>Preferred time</td>
    <td style='padding:8px;font-weight:500'>{preferredDateTime}</td>
  </tr>
</table>
<p>Our team will assign a verified technician shortly. 
   You will receive another email once a technician is assigned.</p>",
                ctaText: "View my requests",
                ctaUrl: "https://serviceapp.com/Customer/Requests/Index");

            await SendAsync(toEmail, customerName, subject, body);
        }

        // =============================================================
        //  2a. Technician assigned → Customer
        // =============================================================
        public async Task SendTechnicianAssignedToCustomerAsync(
            string toEmail,
            string customerName,
            int requestId,
            string technicianName,
            string technicianPhone,
            string category)
        {
            var subject = $"Technician assigned for request #{requestId}";
            var body = ComposeWrapper(
                heading: "Your technician is assigned!",
                preheader: $"{technicianName} will handle your {category} request.",
                bodyHtml: $@"
<p>Hi <strong>{customerName}</strong>,</p>
<p>Great news! A verified technician has been assigned to your request.</p>
<div style='background:#EEF2FF;border-radius:8px;padding:16px;margin:16px 0'>
  <p style='margin:0 0 4px;font-size:13px;color:#6B7280'>Your technician</p>
  <p style='margin:0;font-size:18px;font-weight:600;color:#1E1B4B'>{technicianName}</p>
  <p style='margin:4px 0 0;font-size:14px;color:#4F46E5'>{category} expert</p>
  <p style='margin:8px 0 0;font-size:14px'>📞 {technicianPhone}</p>
</div>
<p>The technician will accept the job shortly and head to your location. 
   You will be notified when they're on the way.</p>",
                ctaText: "View request details",
                ctaUrl: $"https://serviceapp.com/Customer/Requests/Details/{requestId}");

            await SendAsync(toEmail, customerName, subject, body);
        }

        // =============================================================
        //  2b. Job assigned → Technician
        // =============================================================
        public async Task SendJobAssignedToTechnicianAsync(
            string toEmail,
            string technicianName,
            int requestId,
            string customerName,
            string customerPhone,
            string address,
            string category,
            string preferredDateTime)
        {
            var subject = $"New job assigned — Request #{requestId}";
            var body = ComposeWrapper(
                heading: "New job assigned to you!",
                preheader: $"{category} job for {customerName}.",
                bodyHtml: $@"
<p>Hi <strong>{technicianName}</strong>,</p>
<p>A new <strong>{category}</strong> job has been assigned to you. 
   Please accept or reject it from your dashboard.</p>
<table style='width:100%;border-collapse:collapse;margin:16px 0'>
  <tr>
    <td style='padding:8px;color:#6B7280;font-size:13px'>Request ID</td>
    <td style='padding:8px;font-weight:500'>#{requestId}</td>
  </tr>
  <tr style='background:#F9FAFB'>
    <td style='padding:8px;color:#6B7280;font-size:13px'>Customer</td>
    <td style='padding:8px;font-weight:500'>{customerName}</td>
  </tr>
  <tr>
    <td style='padding:8px;color:#6B7280;font-size:13px'>Customer phone</td>
    <td style='padding:8px;font-weight:500'>{customerPhone}</td>
  </tr>
  <tr style='background:#F9FAFB'>
    <td style='padding:8px;color:#6B7280;font-size:13px'>Address</td>
    <td style='padding:8px;font-weight:500'>{address}</td>
  </tr>
  <tr>
    <td style='padding:8px;color:#6B7280;font-size:13px'>Preferred time</td>
    <td style='padding:8px;font-weight:500'>{preferredDateTime}</td>
  </tr>
</table>
<p>Log in to accept the job and view full details.</p>",
                ctaText: "View my jobs",
                ctaUrl: "https://serviceapp.com/Technician/Jobs/Index");

            await SendAsync(toEmail, technicianName, subject, body);
        }

        // =============================================================
        //  3. Job accepted → Customer
        // =============================================================
        public async Task SendJobAcceptedToCustomerAsync(
            string toEmail,
            string customerName,
            int requestId,
            string technicianName,
            string technicianPhone)
        {
            var subject = $"Your technician is on the way — Request #{requestId}";
            var body = ComposeWrapper(
                heading: "Your technician accepted the job!",
                preheader: $"{technicianName} is on the way to your location.",
                bodyHtml: $@"
<p>Hi <strong>{customerName}</strong>,</p>
<p><strong>{technicianName}</strong> has accepted your service request 
   and is on the way to your location.</p>
<div style='background:#F0FDF4;border:1px solid #BBF7D0;
            border-radius:8px;padding:16px;margin:16px 0'>
  <p style='margin:0;font-size:15px;font-weight:600;color:#166534'>
    ✓ Technician is on the way
  </p>
  <p style='margin:8px 0 0;font-size:14px;color:#166534'>
    📞 You can reach them at: {technicianPhone}
  </p>
</div>
<p>Please make sure someone is available at the service address. 
   Once the work is done, the technician will create a bill for your approval.</p>",
                ctaText: "View request",
                ctaUrl: $"https://serviceapp.com/Customer/Requests/Details/{requestId}");

            await SendAsync(toEmail, customerName, subject, body);
        }

        // =============================================================
        //  4. Bill created → Customer
        // =============================================================
        public async Task SendBillCreatedToCustomerAsync(
            string toEmail,
            string customerName,
            int requestId,
            int billId,
            decimal totalAmount)
        {
            var subject = $"Your bill is ready — ₹{totalAmount:N2} due";
            var body = ComposeWrapper(
                heading: "Your bill is ready to pay",
                preheader: $"₹{totalAmount:N2} due for request #{requestId}.",
                bodyHtml: $@"
<p>Hi <strong>{customerName}</strong>,</p>
<p>Your technician has completed the work and created a bill. 
   Please review and pay to close this request.</p>
<div style='background:#EEF2FF;border-radius:8px;
            padding:20px;margin:16px 0;text-align:center'>
  <p style='margin:0;font-size:14px;color:#6B7280'>Total amount due</p>
  <p style='margin:8px 0 0;font-size:32px;font-weight:700;color:#4F46E5'>
    ₹{totalAmount:N2}
  </p>
  <p style='margin:4px 0 0;font-size:13px;color:#6B7280'>
    Bill #{billId} · Request #{requestId}
  </p>
</div>
<p>You can pay via UPI, debit/credit card, or confirm a cash payment 
   through the ServiceApp platform.</p>",
                ctaText: "Pay now",
                ctaUrl: $"https://serviceapp.com/Customer/Bills/Pay?requestId={requestId}");

            await SendAsync(toEmail, customerName, subject, body);
        }

        // =============================================================
        //  5a. Payment receipt → Customer
        // =============================================================
        public async Task SendPaymentReceiptToCustomerAsync(
            string toEmail,
            string customerName,
            int requestId,
            int billId,
            decimal amount,
            string paymentMethod,
            string transactionId,
            DateTime paidAt)
        {
            var subject = $"Payment confirmed — ₹{amount:N2} — ServiceApp";
            var body = ComposeWrapper(
                heading: "Payment confirmed!",
                preheader: $"₹{amount:N2} received. Request #{requestId} is complete.",
                bodyHtml: $@"
<p>Hi <strong>{customerName}</strong>,</p>
<p>Your payment has been received and your service request is now complete. 
   Thank you for using ServiceApp!</p>
<div style='background:#F0FDF4;border:1px solid #BBF7D0;
            border-radius:8px;padding:16px;margin:16px 0'>
  <p style='margin:0;font-size:15px;font-weight:600;color:#166534'>
    ✓ Payment confirmed
  </p>
</div>
<table style='width:100%;border-collapse:collapse;margin:16px 0'>
  <tr>
    <td style='padding:8px;color:#6B7280;font-size:13px'>Amount paid</td>
    <td style='padding:8px;font-weight:600;font-size:16px;color:#059669'>
      ₹{amount:N2}
    </td>
  </tr>
  <tr style='background:#F9FAFB'>
    <td style='padding:8px;color:#6B7280;font-size:13px'>Payment method</td>
    <td style='padding:8px;font-weight:500'>{paymentMethod}</td>
  </tr>
  <tr>
    <td style='padding:8px;color:#6B7280;font-size:13px'>Transaction ref</td>
    <td style='padding:8px;font-size:12px;font-family:monospace'>
      {transactionId}
    </td>
  </tr>
  <tr style='background:#F9FAFB'>
    <td style='padding:8px;color:#6B7280;font-size:13px'>Paid at</td>
    <td style='padding:8px;font-weight:500'>
      {paidAt:dd MMM yyyy, hh:mm tt}
    </td>
  </tr>
  <tr>
    <td style='padding:8px;color:#6B7280;font-size:13px'>Bill ID</td>
    <td style='padding:8px'>#{billId}</td>
  </tr>
  <tr style='background:#F9FAFB'>
    <td style='padding:8px;color:#6B7280;font-size:13px'>Request ID</td>
    <td style='padding:8px'>#{requestId}</td>
  </tr>
</table>
<p>You can download your PDF invoice from the ServiceApp platform.</p>",
                ctaText: "Download invoice",
                ctaUrl: $"https://serviceapp.com/Customer/Bills/Download/{billId}");

            await SendAsync(toEmail, customerName, subject, body);
        }

        // =============================================================
        //  5b. Payment receipt → Technician
        // =============================================================
        public async Task SendPaymentReceiptToTechnicianAsync(
            string toEmail,
            string technicianName,
            int requestId,
            decimal amount)
        {
            var subject = $"Payment received — ₹{amount:N2} — Job #{requestId}";
            var body = ComposeWrapper(
                heading: "Payment received!",
                preheader: $"₹{amount:N2} earned for job #{requestId}.",
                bodyHtml: $@"
<p>Hi <strong>{technicianName}</strong>,</p>
<p>The customer has paid for job #{requestId}. 
   Your earnings will be credited to your wallet within 24 hours.</p>
<div style='background:#EEF2FF;border-radius:8px;
            padding:20px;margin:16px 0;text-align:center'>
  <p style='margin:0;font-size:14px;color:#6B7280'>Amount earned</p>
  <p style='margin:8px 0 0;font-size:32px;font-weight:700;color:#4F46E5'>
    ₹{amount:N2}
  </p>
  <p style='margin:4px 0 0;font-size:13px;color:#6B7280'>
    Job #{requestId}
  </p>
</div>
<p>You are now Available and ready to receive new job assignments. 
   Great work!</p>",
                ctaText: "View my jobs",
                ctaUrl: "https://serviceapp.com/Technician/Jobs/Index");

            await SendAsync(toEmail, technicianName, subject, body);
        }

        // =============================================================
        //  CORE SEND METHOD
        //  All email methods funnel through here.
        // =============================================================
        private async Task SendAsync(
            string toEmail,
            string toName,
            string subject,
            string htmlBody)
        {
            try
            {
                var client = new SendGridClient(_settings.ApiKey);
                var from = new EmailAddress(
                    _settings.FromEmail, _settings.FromName);
                var to = new EmailAddress(toEmail, toName);
                var message = MailHelper.CreateSingleEmail(
                    from, to, subject, plainTextContent: null, htmlBody);

                var response = await client.SendEmailAsync(message);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation(
                        "Email sent to {Email}: {Subject}",
                        toEmail, subject);
                }
                else
                {
                    var body = await response.Body.ReadAsStringAsync();
                    _logger.LogWarning(
                        "SendGrid returned {StatusCode} for {Email}: {Body}",
                        response.StatusCode, toEmail, body);
                }
            }
            catch (Exception ex)
            {
                // Log but NEVER throw — email failure must not
                // break the main business flow
                _logger.LogError(ex,
                    "Failed to send email to {Email}: {Subject}",
                    toEmail, subject);
            }
        }

        // =============================================================
        //  HTML EMAIL WRAPPER
        //  Wraps all email content in a consistent branded template.
        //
        //  Uses inline CSS throughout — email clients (Gmail, Outlook)
        //  strip <style> tags and class attributes. Every style must
        //  be inline on the element itself.
        // =============================================================
        private static string ComposeWrapper(
            string heading,
            string preheader,
            string bodyHtml,
            string ctaText,
            string ctaUrl)
        {
            return $@"<!DOCTYPE html>
<html lang='en'>
<head>
  <meta charset='UTF-8'>
  <meta name='viewport' content='width=device-width,initial-scale=1'>
  <title>{heading}</title>
</head>
<body style='margin:0;padding:0;background:#F3F4F6;
             font-family:-apple-system,BlinkMacSystemFont,
             ""Segoe UI"",Roboto,sans-serif'>

  <!-- Preheader text (shown in email client preview) -->
  <span style='display:none;max-height:0;overflow:hidden;
               color:#F3F4F6'>
    {preheader}
  </span>

  <!-- Outer wrapper -->
  <table width='100%' cellpadding='0' cellspacing='0'>
    <tr>
      <td align='center' style='padding:40px 16px'>

        <!-- Email card -->
        <table width='600' cellpadding='0' cellspacing='0'
               style='max-width:600px;width:100%'>

          <!-- Header bar -->
          <tr>
            <td style='background:#4F46E5;border-radius:12px 12px 0 0;
                       padding:24px 32px'>
              <span style='font-size:22px;font-weight:700;
                           color:#ffffff'>
                ServiceApp
              </span>
            </td>
          </tr>

          <!-- Body card -->
          <tr>
            <td style='background:#ffffff;padding:32px;
                       border-left:1px solid #E5E7EB;
                       border-right:1px solid #E5E7EB'>

              <h1 style='margin:0 0 20px;font-size:22px;
                         font-weight:600;color:#111827'>
                {heading}
              </h1>

              <div style='font-size:14px;line-height:1.7;color:#374151'>
                {bodyHtml}
              </div>

              <!-- CTA button -->
              <div style='text-align:center;margin:28px 0 8px'>
                <a href='{ctaUrl}'
                   style='display:inline-block;padding:12px 28px;
                          background:#4F46E5;color:#ffffff;
                          text-decoration:none;border-radius:8px;
                          font-size:15px;font-weight:600'>
                  {ctaText}
                </a>
              </div>
            </td>
          </tr>

          <!-- Footer -->
          <tr>
            <td style='background:#F9FAFB;border:1px solid #E5E7EB;
                       border-top:none;border-radius:0 0 12px 12px;
                       padding:20px 32px;text-align:center'>
              <p style='margin:0;font-size:12px;color:#9CA3AF'>
                ServiceApp · support@serviceapp.com
              </p>
              <p style='margin:6px 0 0;font-size:11px;color:#D1D5DB'>
                You received this email because you have an account 
                on ServiceApp.
              </p>
            </td>
          </tr>

        </table>
      </td>
    </tr>
  </table>
</body>
</html>";
        }
    }
}