using System.ComponentModel.DataAnnotations;

namespace ServiceApp.Api.Contracts;

/// <summary>POST /api/customer/disputes. Reason and length rules are enforced by DisputeService.</summary>
public record RaiseDisputeRequest(
    [Range(1, int.MaxValue)] int RequestId,
    [Required, StringLength(50)] string Reason,
    [Required, StringLength(2000)] string Description);

/// <summary>POST /api/admin/disputes/{id}/review.</summary>
public record DisputeReviewRequest([StringLength(1000)] string? AdminNote);

/// <summary>POST /api/admin/disputes/{id}/resolve. Resolution is Rejected or ClosedNoAction.</summary>
public record DisputeResolveRequest(
    [Required, StringLength(30)] string Resolution,
    [StringLength(1000)] string? AdminNote);

/// <summary>POST /api/admin/disputes/{id}/refund (Razorpay payments only).</summary>
public record DisputeRefundRequest(
    [Range(0.01, 10_000_000)] decimal Amount,
    [StringLength(1000)] string? AdminNote);
