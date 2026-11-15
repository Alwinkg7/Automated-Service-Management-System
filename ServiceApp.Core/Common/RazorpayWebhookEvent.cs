namespace ServiceApp.Core.Common;

/// <summary>Relevant fields extracted from a verified Razorpay webhook payload.</summary>
public class RazorpayWebhookEvent
{
    /// <summary>Event name, e.g. <c>order.paid</c> or <c>payment.captured</c>.</summary>
    public string Event { get; init; } = string.Empty;
    public string? PaymentId { get; init; }
    public string? OrderId { get; init; }

    /// <summary>Captured amount in the smallest currency unit (paise).</summary>
    public long AmountPaise { get; init; }

    /// <summary>Bill id resolved from the order receipt / notes, if present.</summary>
    public int? BillId { get; init; }
}
