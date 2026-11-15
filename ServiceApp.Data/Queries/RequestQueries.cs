using Microsoft.EntityFrameworkCore;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;
using ServiceApp.Core.Interfaces;
using ServiceApp.Core.Models.Queries;
using ServiceApp.Core.Models.Reports;
using ServiceApp.Data.Context;

namespace ServiceApp.Data.Queries;

/// <summary>SQL-side request search, status counts and history totals (no tracking).</summary>
public class RequestQueries : IRequestQueries
{
    private readonly ApplicationDbContext _db;

    public RequestQueries(ApplicationDbContext db) => _db = db;

    public async Task<PagedResult<ServiceRequest>> SearchAsync(RequestSearchCriteria c, CancellationToken ct = default)
    {
        var query = Filter(_db.ServiceRequests.AsNoTracking(), c);

        var total = await query.CountAsync(ct);

        var ordered = query
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.RequestId)
            .Include(r => r.Customer)
            .Include(r => r.AssignedTechnician).ThenInclude(t => t!.User)
            .Include(r => r.Bill).ThenInclude(b => b!.Payment)
            .AsSplitQuery();

        var page = Math.Max(1, c.Page);
        var size = c.PageSize <= 0 ? 0 : Math.Min(c.PageSize, RequestSearchCriteria.MaxPageSize);

        var items = size == 0
            ? await ordered.ToListAsync(ct)
            : await ordered.Skip((page - 1) * size).Take(size).ToListAsync(ct);

        return new PagedResult<ServiceRequest>(items, total, page, size == 0 ? total : size);
    }

    public async Task<IReadOnlyDictionary<RequestStatus, int>> CountByStatusAsync(
        string? customerId = null, int? technicianProfileId = null, CancellationToken ct = default)
    {
        var query = _db.ServiceRequests.AsNoTracking();
        if (customerId != null) query = query.Where(r => r.CustomerId == customerId);
        if (technicianProfileId != null) query = query.Where(r => r.AssignedTechnicianProfileId == technicianProfileId);

        var counts = await query
            .GroupBy(r => r.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return Enum.GetValues<RequestStatus>()
            .ToDictionary(s => s, s => counts.FirstOrDefault(x => x.Key == s)?.Count ?? 0);
    }

    public async Task<CustomerHistorySummary> GetCustomerHistorySummaryAsync(string customerId, CancellationToken ct = default)
    {
        var finished = _db.ServiceRequests.AsNoTracking()
            .Where(r => r.CustomerId == customerId
                        && (r.Status == RequestStatus.Completed || r.Status == RequestStatus.Cancelled));

        var completed = await finished.CountAsync(r => r.Status == RequestStatus.Completed, ct);
        var cancelled = await finished.CountAsync(r => r.Status == RequestStatus.Cancelled, ct);

        var totalSpent = await finished
            .Where(r => r.Status == RequestStatus.Completed && r.Bill != null)
            .SumAsync(r => (decimal?)r.Bill!.TotalAmount, ct) ?? 0m;

        var avgRating = await finished
            .Where(r => r.Status == RequestStatus.Completed && r.CustomerRating != null)
            .AverageAsync(r => (double?)r.CustomerRating, ct) ?? 0d;

        return new CustomerHistorySummary(completed + cancelled, completed, cancelled, totalSpent, avgRating);
    }

    /// <summary>Applies every criteria filter as SQL predicates.</summary>
    internal static IQueryable<ServiceRequest> Filter(IQueryable<ServiceRequest> query, RequestSearchCriteria c)
    {
        if (c.CustomerId != null)
            query = query.Where(r => r.CustomerId == c.CustomerId);

        if (c.TechnicianProfileId != null)
            query = query.Where(r => r.AssignedTechnicianProfileId == c.TechnicianProfileId);

        if (c.Statuses is { Count: > 0 } statuses)
            query = query.Where(r => statuses.Contains(r.Status));

        if (c.Category != null)
            query = query.Where(r => r.Category == c.Category);

        if (c.CreatedFrom is { } from)
            query = query.Where(r => r.CreatedAt >= from.Date);

        if (c.CreatedTo is { } to)
        {
            var end = to.Date.AddDays(1);
            query = query.Where(r => r.CreatedAt < end);
        }

        if (!string.IsNullOrWhiteSpace(c.Search))
        {
            var term = c.Search.Trim();
            var id = int.TryParse(term, out var parsed) ? parsed : (int?)null;
            query = query.Where(r =>
                r.RequestId == id
                || r.Description.Contains(term)
                || r.Address.Contains(term)
                || r.PinCode.Contains(term)
                || r.Customer.FullName.Contains(term)
                || (r.AssignedTechnician != null && r.AssignedTechnician.User.FullName.Contains(term)));
        }

        return query;
    }
}
