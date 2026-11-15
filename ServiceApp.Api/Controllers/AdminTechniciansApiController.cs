using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceApp.Api.Contracts;
using ServiceApp.Core.Enums;
using ServiceApp.Core.Interfaces;
using ServiceApp.Core.Models.Queries;

namespace ServiceApp.Api.Controllers;

/// <summary>Admin technician management and analytics. Lists are filtered, paged and summed in SQL.</summary>
[Route("api/admin")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
public class AdminTechniciansApiController : ApiControllerBase
{
    private readonly ITechnicianQueries _queries;
    private readonly ITechnicianService _technicians;
    private readonly IZoneService _zones;

    public AdminTechniciansApiController(ITechnicianQueries queries, ITechnicianService technicians, IZoneService zones)
    {
        _queries = queries;
        _technicians = technicians;
        _zones = zones;
    }

    /// <summary>
    /// Technicians with earnings and workload. Without page/pageSize returns the
    /// full array; with them returns { items, page, pageSize, totalCount }.
    /// </summary>
    [HttpGet("technicians")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? skill,
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        var paged = page.HasValue || pageSize.HasValue;
        var result = await _queries.SearchAsync(new TechnicianSearchCriteria
        {
            Skill = Enum.TryParse<ServiceCategory>(skill, true, out var sk) ? sk : null,
            Status = Enum.TryParse<TechnicianStatus>(status, true, out var st) ? st : null,
            IsActive = isActive,
            Search = search,
            Page = Math.Max(1, page ?? 1),
            PageSize = paged ? Math.Clamp(pageSize ?? 20, 1, RequestSearchCriteria.MaxPageSize) : 0
        }, ct);

        var zoneNames = (await _zones.GetAllAsync(includeInactive: true)).ToDictionary(z => z.ZoneId, z => z.ZoneName);
        var rows = result.Items
            .Select(t => ResponseMappers.Technician(t, t.ServiceZoneId is int z ? zoneNames.GetValueOrDefault(z) : null))
            .ToList();
        var counts = await _queries.CountByStatusAsync(ct);

        if (!paged)
            return Ok(new { success = true, data = rows, counts });

        return Ok(new
        {
            success = true,
            data = new { items = rows, page = result.Page, pageSize = result.PageSize, totalCount = result.TotalCount },
            counts
        });
    }

    /// <summary>Technician profile, 6-month earnings and the 20 most recent jobs.</summary>
    [HttpGet("technicians/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Detail(int id, [FromServices] IRequestQueries requests, CancellationToken ct)
    {
        var summary = await _queries.GetSummaryAsync(id, ct);
        if (summary == null)
            return Fail(StatusCodes.Status404NotFound, "Technician not found.");

        var earnings = await _queries.GetMonthlyEarningsAsync(id, 6, ct);
        var jobs = await requests.SearchAsync(new RequestSearchCriteria { TechnicianProfileId = id, PageSize = 20 }, ct);

        return Ok(new
        {
            success = true,
            data = new
            {
                technician = ResponseMappers.Technician(summary),
                monthlyEarnings = earnings.Select(e => new { label = e.Label, amount = e.Amount }),
                recentJobs = jobs.Items.Select(j => new
                {
                    id = j.RequestId,
                    category = j.Category.ToString(),
                    status = j.Status.ToString(),
                    customerName = j.Customer?.FullName,
                    createdAt = j.CreatedAt,
                    billAmount = j.Bill?.TotalAmount,
                    billPaid = j.Bill?.IsPaid ?? false
                })
            }
        });
    }

    /// <summary>Admin override of availability (Available, Busy, Offline).</summary>
    [HttpPost("technicians/{id:int}/status")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetStatus(int id, [FromBody] TechnicianStatusRequest dto)
    {
        var result = await _technicians.OverrideStatusAsync(id, dto.Status);
        return result.IsSuccess ? Ok(new { success = true, status = result.Data!.Status.ToString() }) : Fail(result);
    }

    /// <summary>Changes the skill. 409 while the technician has an Assigned, InProgress or Billed job.</summary>
    [HttpPost("technicians/{id:int}/skill")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SetSkill(int id, [FromBody] TechnicianSkillRequest dto)
    {
        var result = await _technicians.UpdateSkillAsync(id, dto.Skill);
        return result.IsSuccess ? Ok(new { success = true, skill = result.Data!.Skill.ToString() }) : Fail(result);
    }

    /// <summary>Activates the technician login (same rules as POST /users/{id}/toggle-active).</summary>
    [HttpPost("technicians/{id:int}/activate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Task<IActionResult> Activate(int id, [FromServices] IAccountService accounts) => SetActive(id, true, accounts);

    /// <summary>Deactivates the technician login; existing sessions end within a minute.</summary>
    [HttpPost("technicians/{id:int}/deactivate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Task<IActionResult> Deactivate(int id, [FromServices] IAccountService accounts) => SetActive(id, false, accounts);

    /// <summary>Analytics dashboard (same metrics as the MVC page). Revenue is grouped by bill paidAt.</summary>
    [HttpGet("analytics")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Analytics([FromServices] IAnalyticsQueries analytics, CancellationToken ct)
    {
        var s = await analytics.GetSnapshotAsync(ct);
        return Ok(new
        {
            success = true,
            data = new
            {
                totals = new
                {
                    totalRequests = s.TotalRequests,
                    completedRequests = s.CompletedRequests,
                    pendingRequests = s.PendingRequests,
                    totalCustomers = s.TotalCustomers,
                    totalTechnicians = s.TotalTechnicians,
                    totalRevenue = s.TotalRevenue,
                    completionRate = s.TotalRequests == 0 ? 0 : Math.Round(100.0 * s.CompletedRequests / s.TotalRequests, 1)
                },
                statusBreakdown = s.StatusCounts,
                revenueByDay = s.RevenueLast7Days.Select(x => new { label = x.Label, amount = x.Amount }),
                requestsByDay = s.RequestsLast7Days.Select(x => new { label = x.Label, count = x.Count }),
                categories = s.Categories.Select(x => new { category = x.Label, count = x.Count }),
                topTechnicians = s.TopTechnicians.Select(t => ResponseMappers.Technician(t)),
                recentActivity = s.RecentActivity.Select(a => new
                {
                    requestId = a.RequestId,
                    status = a.Status,
                    note = a.Note,
                    changedAt = a.ChangedAt,
                    category = a.Category
                })
            }
        });
    }

    private async Task<IActionResult> SetActive(int id, bool active, IAccountService accounts)
    {
        var summary = await _queries.GetSummaryAsync(id);
        if (summary == null)
            return Fail(StatusCodes.Status404NotFound, "Technician not found.");

        var result = await accounts.SetActiveAsync(summary.UserId, active, UserId);
        return result.IsSuccess ? Ok(new { success = true, isActive = result.Data!.IsActive }) : Fail(result);
    }
}
