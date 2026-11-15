using System.ComponentModel.DataAnnotations;

namespace ServiceApp.Api.Contracts;

/// <summary>POST /api/technician/jobs/{id}/bill</summary>
public record CreateBillDto(
    [Range(0, 10_000_000)] decimal LaborCost,
    [Range(0, 10_000_000)] decimal MaterialCost,
    [StringLength(500)] string? Description);
