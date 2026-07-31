using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;
using ServiceApp.Core.Interfaces;
using System.Security.Claims;

namespace ServiceApp.Web.Controllers.Api;

[ApiController]
[Route("api/admin")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
           Roles = "Admin")]
public class AdminApiController : ControllerBase
{
    private readonly IServiceRequestService _svc;
    private readonly UserManager<ApplicationUser> _users;

    public AdminApiController(
        IServiceRequestService svc,
        UserManager<ApplicationUser> users)
    {
        _svc = svc;
        _users = users;
    }

    private string AdminId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // ── GET /api/admin/dashboard ──────────────────────────────────
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var all = await _svc.GetAllRequestsAsync();
        var requests = all.IsSuccess ? all.Data!.ToList() : new();
        var allUsers = _users.Users.ToList();

        return Ok(new
        {
            success = true,
            stats = new
            {
                totalRequests = requests.Count,
                pending = requests.Count(r => r.Status == RequestStatus.Pending),
                inProgress = requests.Count(r => r.Status == RequestStatus.InProgress),
                completed = requests.Count(r => r.Status == RequestStatus.Completed),
                totalCustomers = allUsers.Count(u => u.Role == UserRole.Customer),
                totalTechnicians = allUsers.Count(u => u.Role == UserRole.Technician),
                revenue = requests
                    .Where(r => r.Bill != null && r.Bill.IsPaid)
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

    // ── GET /api/admin/requests ───────────────────────────────────
    [HttpGet("requests")]
    public async Task<IActionResult> GetRequests([FromQuery] string? status)
    {
        RequestStatus? filter = null;
        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<RequestStatus>(status, out var s))
            filter = s;

        var result = await _svc.GetAllRequestsAsync(filter);
        if (!result.IsSuccess)
            return StatusCode(500,
                new { success = false, error = result.ErrorMessage });

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
                    billPaid = r.Bill?.IsPaid ?? false,
                    billAmount = r.Bill?.TotalAmount,
                    isUnassigned = r.AssignedTechnicianProfileId == null
                                     && r.Status == RequestStatus.Pending,
                })
        });
    }

    // ── GET /api/admin/requests/{id} ──────────────────────────────
    [HttpGet("requests/{id}")]
    public async Task<IActionResult> GetDetail(int id)
    {
        var result = await _svc.GetRequestDetailsAsync(id);
        if (!result.IsSuccess)
            return NotFound(new { success = false, error = result.ErrorMessage });

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
                    isPaid = r.Bill.IsPaid,
                },
            }
        });
    }

    // ── GET /api/admin/technicians/available ──────────────────────
    [HttpGet("technicians/available")]
    public async Task<IActionResult> GetAvailableTechnicians(
        [FromQuery] string? category)
    {
        if (!Enum.TryParse<ServiceCategory>(category, out var cat))
            cat = ServiceCategory.General;

        var result = await _svc.GetAvailableTechniciansAsync(cat);

        // Return empty list if none — not an error
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

    // ── POST /api/admin/requests/{id}/assign ─────────────────────
    [HttpPost("requests/{id}/assign")]
    public async Task<IActionResult> Assign(int id, [FromBody] AssignDto dto)
    {
        var result = await _svc.AssignTechnicianAsync(
            id, dto.TechnicianProfileId, AdminId);

        if (!result.IsSuccess)
            return BadRequest(new { success = false, error = result.ErrorMessage });

        return Ok(new { success = true });
    }

    // ── GET /api/admin/users ──────────────────────────────────────
    [HttpGet("users")]
    public IActionResult GetUsers([FromQuery] string? role)
    {
        var q = _users.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(role) &&
            Enum.TryParse<UserRole>(role, out var r))
            q = q.Where(u => u.Role == r);

        return Ok(new
        {
            success = true,
            data = q.OrderByDescending(u => u.CreatedAt).Select(u => new
            {
                id = u.Id,
                name = u.FullName,
                email = u.Email,
                phone = u.Phone,
                role = u.Role.ToString(),
                isActive = u.IsActive,
                createdAt = u.CreatedAt.ToString("dd MMM yyyy"),
            }).ToList()
        });
    }

    // ── POST /api/admin/users/{id}/toggle-active ─────────────────
    [HttpPost("users/{id}/toggle-active")]
    public async Task<IActionResult> ToggleActive(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null)
            return NotFound(new { success = false, error = "User not found." });

        user.IsActive = !user.IsActive;
        await _users.UpdateAsync(user);
        return Ok(new { success = true, isActive = user.IsActive });
    }
}

public record AssignDto(int TechnicianProfileId);