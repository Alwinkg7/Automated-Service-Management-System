using System.ComponentModel.DataAnnotations;
using ServiceApp.Core.Enums;

namespace ServiceApp.Api.Contracts;

/// <summary>POST/PUT /api/admin/zones. PinCodes: comma, space or newline separated 6-digit codes.</summary>
public record ZoneRequest(
    [Required, StringLength(100)] string ZoneName,
    [Required, StringLength(100)] string City,
    [Required, StringLength(100)] string State,
    [Required, StringLength(4000)] string PinCodes,
    bool IsActive = true);

/// <summary>POST/PUT /api/admin/promo-codes. DiscountType is Flat, Percentage or FirstOnly.</summary>
public record PromoCodeRequest(
    [Required, StringLength(30)] string Code,
    [StringLength(200)] string? Description,
    [Required] string DiscountType,
    [Range(0.01, 1_000_000)] decimal DiscountValue,
    decimal? MaxDiscount,
    [Range(0, 1_000_000)] decimal MinimumAmount,
    int? MaxUses,
    DateTime? ExpiresAt,
    bool IsActive = true);

/// <summary>POST /api/admin/technicians/{id}/status.</summary>
public record TechnicianStatusRequest([Required] TechnicianStatus Status);

/// <summary>POST /api/admin/technicians/{id}/skill.</summary>
public record TechnicianSkillRequest([Required] ServiceCategory Skill);

/// <summary>POST /api/admin/withdrawals/{id}/reject.</summary>
public record WithdrawalRejectRequest([StringLength(500)] string? AdminNote);
