using Microsoft.EntityFrameworkCore;
using ServiceApp.Core.Enums;
using ServiceApp.Core.Interfaces;
using ServiceApp.Core.Models.Queries;
using ServiceApp.Core.Models.Reports;
using ServiceApp.Data.Context;

namespace ServiceApp.Data.Queries;

/// <summary>
/// Analytics dashboard figures. Every metric is a SQL aggregate; only small
/// grouped results are materialised.
/// </summary>
public class AnalyticsQueries : IAnalyticsQueries
{
    private readonly ApplicationDbContext _db;
    private readonly IRequestQueries _requests;
    private readonly ITechnicianQueries _technicians;

    public AnalyticsQueries(ApplicationDbContext db, IRequestQueries requests, ITechnicianQueries technicians)
    {
        _db = db;
        _requests = requests;
        _technicians = technicians;
    }

    public async Task<AnalyticsSnapshot> GetSnapshotAsync(CancellationToken ct = default)
    {
        var statusCounts = await _requests.CountByStatusAsync(ct: ct);

        var totalCustomers = await _db.Users.CountAsync(u => u.Role == UserRole.Customer && u.IsActive, ct);
        var totalTechnicians = await _db.TechnicianProfiles.CountAsync(ct);
        var totalRevenue = await _db.Bills
            .Where(b => b.PaymentStatus == PaymentStatus.Paid)
            .SumAsync(b => (decimal?)b.TotalAmount, ct) ?? 0m;

        var today = DateTime.UtcNow.Date;
        var weekStart = today.AddDays(-6);

        var revenueByDay = await _db.Bills.AsNoTracking()
            .Where(b => b.PaymentStatus == PaymentStatus.Paid && b.PaidAt >= weekStart)
            .GroupBy(b => b.PaidAt!.Value.Date)
            .Select(g => new { Day = g.Key, Total = g.Sum(b => b.TotalAmount) })
            .ToListAsync(ct);

        var requestsByDay = await _db.ServiceRequests.AsNoTracking()
            .Where(r => r.CreatedAt >= weekStart)
            .GroupBy(r => r.CreatedAt.Date)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var days = Enumerable.Range(0, 7).Select(i => weekStart.AddDays(i)).ToList();

        var categories = await _db.ServiceRequests.AsNoTracking()
            .GroupBy(r => r.Category)
            .Select(g => new { g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync(ct);

        var top = await _technicians.SearchAsync(new TechnicianSearchCriteria { Page = 1, PageSize = 5 }, ct);

        var recent = await _db.ServiceHistories.AsNoTracking()
            .OrderByDescending(h => h.ChangedAt)
            .Take(10)
            .Select(h => new
            {
                h.RequestId,
                h.Status,
                h.Note,
                h.ChangedAt,
                Category = _db.ServiceRequests
                    .Where(r => r.RequestId == h.RequestId)
                    .Select(r => (ServiceCategory?)r.Category)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        return new AnalyticsSnapshot
        {
            TotalRequests = statusCounts.Values.Sum(),
            CompletedRequests = statusCounts[RequestStatus.Completed],
            PendingRequests = statusCounts[RequestStatus.Pending],
            TotalCustomers = totalCustomers,
            TotalTechnicians = totalTechnicians,
            TotalRevenue = totalRevenue,
            StatusCounts = statusCounts.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
            RevenueLast7Days = days
                .Select(d => new LabelledAmount(d.ToString("dd MMM"), revenueByDay.FirstOrDefault(x => x.Day == d)?.Total ?? 0m))
                .ToList(),
            RequestsLast7Days = days
                .Select(d => new LabelledCount(d.ToString("dd MMM"), requestsByDay.FirstOrDefault(x => x.Day == d)?.Count ?? 0))
                .ToList(),
            Categories = categories.Select(c => new LabelledCount(c.Key.ToString(), c.Count)).ToList(),
            TopTechnicians = top.Items,
            RecentActivity = recent
                .Select(h => new RecentActivityItem(
                    h.RequestId, h.Status.ToString(), h.Note ?? string.Empty, h.ChangedAt,
                    h.Category?.ToString() ?? "—"))
                .ToList()
        };
    }
}
