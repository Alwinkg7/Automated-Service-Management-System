using System.ComponentModel.DataAnnotations;

namespace ServiceApp.Api.Contracts;

/// <summary>POST /api/technician/wallet/withdrawals. The balance check is server-side.</summary>
public record WithdrawalRequestDto(
    [Range(0.01, 10_000_000)] decimal Amount,
    [Required, StringLength(100)] string UpiId);

/// <summary>POST /api/technician/jobs/{id}/location.</summary>
public record LocationUpdateRequest(
    [Range(-90, 90)] double Lat,
    [Range(-180, 180)] double Lng);

/// <summary>POST /api/customer/requests/{id}/rebook. Description/address default to the original.</summary>
public record RebookRequest(
    DateTime ScheduledDate,
    [StringLength(1000, MinimumLength = 5)] string? Description,
    [StringLength(300, MinimumLength = 5)] string? Address);
