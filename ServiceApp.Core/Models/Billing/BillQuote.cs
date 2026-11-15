namespace ServiceApp.Core.Models.Billing;

/// <summary>Price breakdown of a bill after discounts. Payable is what the customer is charged.</summary>
public record BillQuote(
    int BillId,
    decimal Subtotal,
    decimal Discount,
    decimal Payable,
    string? PromoCode,
    int PointsRedeemed);
