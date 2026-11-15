using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceApp.Api.Contracts;
using ServiceApp.Core.Interfaces;

namespace ServiceApp.Api.Controllers;

/// <summary>Admin catalogue: service zones and promo codes (rules in IZoneService / IPromoCodeService).</summary>
[Route("api/admin")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
public class AdminCatalogApiController : ApiControllerBase
{
    private readonly IZoneService _zones;
    private readonly IPromoCodeService _codes;
    private readonly ITechnicianQueries _technicians;

    public AdminCatalogApiController(IZoneService zones, IPromoCodeService codes, ITechnicianQueries technicians)
    {
        _zones = zones;
        _codes = codes;
        _technicians = technicians;
    }

    // ── Zones ─────────────────────────────────────────────────────────

    /// <summary>All zones (active and inactive) with technician counts.</summary>
    [HttpGet("zones")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Zones(CancellationToken ct)
    {
        var zones = await _zones.GetAllAsync(includeInactive: true);
        var techs = (await _technicians.SearchAsync(new Core.Models.Queries.TechnicianSearchCriteria { PageSize = 0 }, ct)).Items;
        return Ok(new
        {
            success = true,
            data = zones.Select(z => ResponseMappers.Zone(z, techs.Count(t => t.ServiceZoneId == z.ZoneId)))
        });
    }

    [HttpGet("zones/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Zone(int id)
    {
        var result = await _zones.GetAsync(id);
        return result.IsSuccess ? Ok(new { success = true, data = ResponseMappers.Zone(result.Data!) }) : Fail(result);
    }

    /// <summary>Creates a zone. 400 for invalid 6-digit pin codes, 409 for a duplicate name or a pin already in another zone.</summary>
    [HttpPost("zones")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateZone([FromBody] ZoneRequest dto)
    {
        var result = await _zones.CreateAsync(ToInput(dto));
        if (!result.IsSuccess) return Fail(result);
        return CreatedAtAction(nameof(Zone), new { id = result.Data!.ZoneId },
            new { success = true, data = ResponseMappers.Zone(result.Data) });
    }

    [HttpPut("zones/{id:int}")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateZone(int id, [FromBody] ZoneRequest dto)
    {
        var result = await _zones.UpdateAsync(id, ToInput(dto));
        return result.IsSuccess ? Ok(new { success = true, data = ResponseMappers.Zone(result.Data!) }) : Fail(result);
    }

    [HttpPost("zones/{id:int}/toggle")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ToggleZone(int id)
    {
        var result = await _zones.ToggleAsync(id);
        return result.IsSuccess ? Ok(new { success = true, data = ResponseMappers.Zone(result.Data!) }) : Fail(result);
    }

    // ── Promo codes ───────────────────────────────────────────────────

    [HttpGet("promo-codes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> PromoCodes() =>
        Ok(new { success = true, data = (await _codes.GetAllAsync()).Select(ResponseMappers.PromoCode) });

    [HttpGet("promo-codes/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PromoCode(int id)
    {
        var result = await _codes.GetAsync(id);
        return result.IsSuccess ? Ok(new { success = true, data = ResponseMappers.PromoCode(result.Data!) }) : Fail(result);
    }

    /// <summary>Creates a code. 409 if it exists; 400 for invalid values (e.g. percentage over 100).</summary>
    [HttpPost("promo-codes")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreatePromoCode([FromBody] PromoCodeRequest dto)
    {
        var result = await _codes.CreateAsync(ToInput(dto));
        if (!result.IsSuccess) return Fail(result);
        return CreatedAtAction(nameof(PromoCode), new { id = result.Data!.PromoCodeId },
            new { success = true, data = ResponseMappers.PromoCode(result.Data) });
    }

    /// <summary>Updates a code. A used code cannot be renamed (409).</summary>
    [HttpPut("promo-codes/{id:int}")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdatePromoCode(int id, [FromBody] PromoCodeRequest dto)
    {
        var result = await _codes.UpdateAsync(id, ToInput(dto));
        return result.IsSuccess ? Ok(new { success = true, data = ResponseMappers.PromoCode(result.Data!) }) : Fail(result);
    }

    [HttpPost("promo-codes/{id:int}/toggle")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> TogglePromoCode(int id)
    {
        var result = await _codes.ToggleAsync(id);
        return result.IsSuccess ? Ok(new { success = true, data = ResponseMappers.PromoCode(result.Data!) }) : Fail(result);
    }

    /// <summary>Deletes an unused code (204). A used code returns 409: deactivate it instead.</summary>
    [HttpDelete("promo-codes/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeletePromoCode(int id)
    {
        var result = await _codes.DeleteAsync(id);
        return result.IsSuccess ? NoContent() : Fail(result);
    }

    private static ZoneInput ToInput(ZoneRequest d) => new(d.ZoneName, d.City, d.State, d.PinCodes, d.IsActive);

    private static PromoCodeInput ToInput(PromoCodeRequest d) => new(
        d.Code, d.Description ?? string.Empty, d.DiscountType, d.DiscountValue, d.MaxDiscount,
        d.MinimumAmount, d.MaxUses, d.ExpiresAt, d.IsActive);
}
