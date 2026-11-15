using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceApp.Api.Contracts;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;
using ServiceApp.Core.Interfaces;

namespace ServiceApp.Api.Controllers;

/// <summary>Admin endpoints: dashboard, request assignment and user management.</summary>
[Route("api/admin")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
public class AdminApiController : ApiControllerBase
{
    private readonly IServiceRequestService _svc;
    private readonly UserManager<ApplicationUser> _users;

    public AdminApiController(IServiceRequestService svc, UserManager<ApplicationUser> users)
    {
        _svc = svc;
        _users = users;
    }

    [HttpGet("dashboard")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var all = await _svc.GetAllRequestsAsync();
        var requests = all.IsSuccess ? all.Data!.ToList() : new List<ServiceRequest>();

        var totalCustomers = await _users.Users.CountAsync(u => u.Role == UserRole.Customer, ct);
        var totalTechnicians = await _users.Users.CountAsync(u => u.Role == UserRole.Technician, ct);

        return Ok(new
        {
            success = true,
            stats = new
            {
                totalRequests = requests.Count,
                pending = requests.Count(r => r.Status == RequestStatus.Pending),
                inProgress = requests.Count(r => r.Status == RequestStatus.InProgress),
                completed = requests.Count(r => r.Status == RequestStatus.Completed),
                totalCustomers,
                totalTechnicians,
                revenue = requests
                    .Where(r => r.Bill != null && IsPaid(r.Bill))
                    .Sum(r => r.Bill!.TotalAmount),
            },
            pendingRequests = requests
                .Where(r => r.Status == RequestStatus.Pending)
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

    [HttpGet("requests")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRequests([FromQuery] string? status)
    {
        RequestStatus? filter = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<RequestStatus>(status, true, out var s))
            filter = s;

        var result = await _svc.GetAllRequestsAsync(filter);
        if (!result.IsSuccess)
            return Fail(StatusCodes.Status500InternalServerError, result.ErrorMessage);

        return Ok(new
        {
            success = true,
            data = result.Data!
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    id = r.RequestId,
                    category = r.Category.ToString(),
                    status = r.Status.ToString(),
                    customerName = r.Customer?.FullName,
                    technicianName = r.AssignedTechnician?.User?.FullName,
                    scheduledDate = r.PreferredDateTime.ToString("dd MMM yyyy"),
                    createdAt = r.CreatedAt.ToString("dd MMM yyyy"),
                    hasBill = r.Bill != null,
                    billPaid = r.Bill != null && IsPaid(r.Bill),
                    billAmount = r.Bill?.TotalAmount,
                    isUnassigned = r.AssignedTechnicianProfileId == null && r.Status == RequestStatus.Pending,
                })
        });
    }

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
                    amount = r.Bill.TotalAmount,
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
