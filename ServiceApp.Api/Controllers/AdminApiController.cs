using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceApp.Api.Contracts;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;
using ServiceApp.Core.Interfaces;
using ServiceApp.Core.Models.Queries;

namespace ServiceApp.Api.Controllers;

/// <summary>Admin endpoints: dashboard, request assignment and user management.</summary>
[Route("api/admin")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
public class AdminApiController : ApiControllerBase
{
    private readonly IServiceRequestService _svc;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IRequestQueries _queries;

    public AdminApiController(IServiceRequestService svc, UserManager<ApplicationUser> users, IRequestQueries queries)
    {
        _svc = svc;
        _users = users;
        _queries = queries;
    }

    [HttpGet("dashboard")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Dashboard([FromServices] IAnalyticsQueries analytics, CancellationToken ct)
    {
        // Counts and revenue are SQL aggregates; only the pending preview rows are loaded.
        var snapshot = await analytics.GetSnapshotAsync(ct);
        var pending = await _queries.SearchAsync(new RequestSearchCriteria
        {
            Statuses = [RequestStatus.Pending],
            PageSize = RequestSearchCriteria.MaxPageSize
        }, ct);

        return Ok(new
        {
            success = true,
            stats = new
            {
                totalRequests = snapshot.TotalRequests,
                pending = snapshot.PendingRequests,
                inProgress = snapshot.StatusCounts.GetValueOrDefault(nameof(RequestStatus.InProgress)),
                completed = snapshot.CompletedRequests,
                totalCustomers = snapshot.TotalCustomers,
                totalTechnicians = snapshot.TotalTechnicians,
                revenue = snapshot.TotalRevenue,
            },
            pendingRequests = pending.Items
                .OrderBy(r => r.PreferredDateTime)
                .Take(10)
                .Select(r => new
                {
                    id = r.RequestId,
                    category = r.Category.ToString(),
                    status = r.Status.ToString(),
                    customerName = r.Customer?.FullName,
                    scheduledDate = r.PreferredDateTime.ToString("dd MMM yyyy"),
                    createdAt = r.CreatedAt.ToString("dd MMM yyyy"),
                    address = r.Address,
                })
        });
    }

    /// <summary>
    /// Requests with SQL-side search/status/date filters. With <c>page</c> or
    /// <c>pageSize</c> the response is paged ({ items, page, pageSize, totalCount });
    /// without them it is the legacy full array (mobile client).
    /// </summary>
    [HttpGet("requests")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRequests(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        var paged = page.HasValue || pageSize.HasValue;
        var result = await _queries.SearchAsync(Criteria(status, search, from, to,
            page ?? 1, paged ? Math.Clamp(pageSize ?? 20, 1, RequestSearchCriteria.MaxPageSize) : 0), ct);

        var rows = result.Items.Select(RequestRow).ToList();
        if (!paged)
            return Ok(new { success = true, data = rows });

        return Ok(new
        {
            success = true,
            data = new { items = rows, page = result.Page, pageSize = result.PageSize, totalCount = result.TotalCount }
        });
    }

    /// <summary>CSV export (same filters as GET /requests; formula injection escaped).</summary>
    [HttpGet("requests/export")]
    [Produces("text/csv", "application/problem+json")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportRequests(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromServices] IRequestExportService export,
        CancellationToken ct)
    {
        var (content, _) = await export.ExportCsvAsync(Criteria(status, search, from, to, 1, 0), ct);
        return File(content, "text/csv", $"ServiceApp-Requests-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv");
    }

    /// <summary>Cancels a Pending or Assigned request (frees the technician). 409 otherwise.</summary>
    [HttpPost("requests/{id:int}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(int id)
    {
        var result = await _svc.CancelRequestAsync(id, UserId);
        return result.IsSuccess ? Ok(new { success = true }) : Fail(result);
    }

    /// <summary>Runs one auto-assignment cycle now and reports how many requests were assigned.</summary>
    [HttpPost("auto-assign")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RunAutoAssign([FromServices] IAutoAssignmentService autoAssign, CancellationToken ct)
    {
        var before = (await _queries.CountByStatusAsync(ct: ct))[RequestStatus.Pending];
        await autoAssign.RunAsync();
        var after = (await _queries.CountByStatusAsync(ct: ct))[RequestStatus.Pending];

        return Ok(new { success = true, data = new { assigned = Math.Max(0, before - after), stillPending = after } });
    }

    private static RequestSearchCriteria Criteria(
        string? status, string? search, DateTime? from, DateTime? to, int page, int pageSize) => new()
    {
        Statuses = Enum.TryParse<RequestStatus>(status, true, out var s) ? [s] : null,
        Search = search,
        CreatedFrom = from,
        CreatedTo = to,
        Page = Math.Max(1, page),
        PageSize = pageSize
    };

    private static object RequestRow(ServiceRequest r) => new
    {
        id = r.RequestId,
        category = r.Category.ToString(),
        status = r.Status.ToString(),
        customerName = r.Customer?.FullName,
        technicianName = r.AssignedTechnician?.User?.FullName,
        address = r.Address,
        pinCode = r.PinCode,
        scheduledDate = r.PreferredDateTime.ToString("dd MMM yyyy"),
        createdAt = r.CreatedAt.ToString("dd MMM yyyy"),
        createdAtUtc = r.CreatedAt,
        hasBill = r.Bill != null,
        billPaid = r.Bill != null && IsPaid(r.Bill),
        billAmount = r.Bill?.TotalAmount,
        paidAt = r.Bill?.PaidAt,
        isUnassigned = r.AssignedTechnicianProfileId == null && r.Status == RequestStatus.Pending,
    };

    [HttpGet("requests/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetail(int id)
    {
        var result = await _svc.GetRequestDetailsAsync(id);
        if (!result.IsSuccess)
            return Fail(StatusCodes.Status404NotFound, result.ErrorMessage);

        var r = result.Data!;
        return Ok(new
        {
            success = true,
            data = new
            {
                id = r.RequestId,
                category = r.Category.ToString(),
                status = r.Status.ToString(),
                description = r.Description,
                address = r.Address,
                scheduledDate = r.PreferredDateTime.ToString("dd MMM yyyy, hh:mm tt"),
                createdAt = r.CreatedAt.ToString("dd MMM yyyy"),
                canAssign = r.Status == RequestStatus.Pending,
                canCancel = r.Status is RequestStatus.Pending or RequestStatus.Assigned,
                pinCode = r.PinCode,
                feedback = r.CustomerFeedback,
                rating = r.CustomerRating,
                statusHistory = r.History.OrderBy(h => h.ChangedAt).Select(ResponseMappers.History),

                customer = new
                {
                    name = r.Customer?.FullName,
                    email = r.Customer?.Email,
                    phone = r.Customer?.Phone,
                },

                technician = r.AssignedTechnician == null ? null : new
                {
                    id = r.AssignedTechnician.TechnicianProfileId,
                    name = r.AssignedTechnician.User?.FullName,
                    phone = r.AssignedTechnician.User?.Phone,
                    rating = r.AssignedTechnician.Rating,
                    status = r.AssignedTechnician.Status.ToString(),
                },

                bill = r.Bill == null ? null : new
                {
                    id = r.Bill.Id,
                    amount = r.Bill.TotalAmount,
                    laborCost = r.Bill.LaborCost,
                    materialCost = r.Bill.MaterialCost,
                    discount = r.Bill.DiscountAmount,
                    payable = r.Bill.PayableAmount,
                    paidAt = r.Bill.PaidAt,
                    paymentMethod = r.Bill.Payment?.PaymentMethod,
                    items = r.Bill.BillItems.Select(ResponseMappers.BillItem),
                    isPaid = IsPaid(r.Bill),
                },
            }
        });
    }

    /// <summary>Available technicians for a category (defaults to General when missing/invalid).</summary>
    [HttpGet("technicians/available")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvailableTechnicians([FromQuery] string? category)
    {
        if (!Enum.TryParse<ServiceCategory>(category, true, out var cat))
            cat = ServiceCategory.General;

        var result = await _svc.GetAvailableTechniciansAsync(cat);
        var data = result.IsSuccess ? result.Data! : Enumerable.Empty<TechnicianProfile>();

        return Ok(new
        {
            success = true,
            data = data.Select(t => new
            {
                id = t.TechnicianProfileId,
                name = t.User?.FullName,
                phone = t.User?.Phone,
                rating = t.Rating,
                totalJobs = t.TotalJobsCompleted,
                skills = t.Skill.ToString(),
                status = t.Status.ToString(),
            })
        });
    }

    [HttpPost("requests/{id:int}/assign")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Assign(int id, [FromBody] AssignDto dto)
    {
        var result = await _svc.AssignTechnicianAsync(id, dto.TechnicianProfileId, UserId);
        if (!result.IsSuccess)
            return Fail(result);

        return Ok(new { success = true });
    }

    /// <summary>Lists users, optionally filtered by role name (Admin, Customer, Technician).</summary>
    [HttpGet("users")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers([FromQuery] string? role, CancellationToken ct)
    {
        var q = _users.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(role) && Enum.TryParse<UserRole>(role, true, out var r))
            q = q.Where(u => u.Role == r);

        var users = await q
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new { u.Id, u.FullName, u.Email, u.Phone, u.Role, u.IsActive, u.CreatedAt })
            .ToListAsync(ct);

        return Ok(new
        {
            success = true,
            data = users.Select(u => new
            {
                id = u.Id,
                name = u.FullName,
                email = u.Email,
                phone = u.Phone,
                role = u.Role.ToString(),
                isActive = u.IsActive,
                createdAt = u.CreatedAt.ToString("dd MMM yyyy"),
            })
        });
    }

    [HttpPost("users/{id}/toggle-active")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ToggleActive(string id, [FromServices] IAccountService accounts)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null)
            return Fail(StatusCodes.Status404NotFound, "User not found.");

        var result = await accounts.SetActiveAsync(id, !user.IsActive, UserId);
        if (!result.IsSuccess)
            return Fail(result);

        return Ok(new { success = true, isActive = result.Data!.IsActive });
    }

    private static bool IsPaid(Bill bill) => bill.IsPaid;
}
