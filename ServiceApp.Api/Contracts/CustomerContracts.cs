using System.ComponentModel.DataAnnotations;

namespace ServiceApp.Api.Contracts;

/// <summary>POST /api/customer/requests</summary>
public record CreateRequestDto(
    [Required] string Category,
    [Required, StringLength(1000, MinimumLength = 5)] string Description,
    DateTime ScheduledDate,
    [Required, StringLength(300, MinimumLength = 5)] string Address,
    [StringLength(10)] string? PinCode);

/// <summary>POST /api/customer/requests/{id}/rate</summary>
public record RateDto(
    [Range(1, 5)] int Stars,
    [StringLength(1000)] string? Comment);

/// <summary>POST /api/customer/bills/{id}/razorpay/verify</summary>
public record VerifyRazorpayPaymentRequest(
    [Required] string RazorpayOrderId,
    [Required] string RazorpayPaymentId,
    [Required] string RazorpaySignature);
