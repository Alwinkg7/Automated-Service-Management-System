using Microsoft.EntityFrameworkCore;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;
using ServiceApp.Core.Interfaces;
using ServiceApp.Core.Models.Queries;
using ServiceApp.Core.Models.Reports;
using ServiceApp.Data.Context;

namespace ServiceApp.Data.Queries;

/// <summary>Admin technician list/detail with earnings and workload computed in SQL subqueries.</summary>
public class TechnicianQueries : ITechnicianQueries
{
    private readonly ApplicationDbContext _db;

    public TechnicianQueries(ApplicationDbContext db) => _db = db;

    public async Task<PagedResult<TechnicianSummary>> SearchAsync(TechnicianSearchCriteria c, CancellationToken ct = default)
    {
        var query = _db.TechnicianProfiles.AsNoTracking();

        if (c.Skill != null) query = query.Where(t => t.Skill == c.Skill);
        if (c.Status != null) query = query.Where(t => t.Status == c.Status);
        if (c.IsActive != null) query = query.Where(t => t.User.IsActive == c.IsActive);
        if (!string.IsNullOrWhiteSpace(c.Search))
        {
            var term = c.Search.Trim();
            query = query.Where(t =>
                t.User.FullName.Contains(term)
                || (t.User.Email != null && t.User.Email.Contains(term))
                || (t.User.Phone != null && t.User.Phone.Contains(term)));
        }

        var total = await query.CountAsync(ct);
        var ordered = Project(query.OrderByDescending(t => t.TotalJobsCompleted).ThenByDescending(t => t.Rating));

        var page = Math.Max(1, c.Page);
        var size = c.PageSize <= 0 ? 0 : Math.Min(c.PageSize, RequestSearchCriteria.MaxPageSize);
        var items = size == 0
            ? await ordered.ToListAsync(ct)
            : await ordered.Skip((page - 1) * size).Take(size).ToListAsync(ct);

        return new PagedResult<TechnicianSummary>(items, total, page, size == 0 ? total : size);
    }

    public Task<TechnicianSummary?> GetSummaryAsync(int technicianProfileId, CancellationToken ct = default) =>
        Project(_db.TechnicianProfiles.AsNoTracking().Where(t => t.TechnicianProfileId == technicianProfileId))
            .FirstOrDefaultAsync(ct);

    public async Task<TechnicianStatusCounts> CountByStatusAsync(CancellationToken ct = default)
    {
        var counts = await _db.TechnicianProfiles.AsNoTracking()
            .GroupBy(t => t.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int Of(TechnicianStatus s) => counts.FirstOrDefault(x => x.Key == s)?.Count ?? 0;
        return new TechnicianStatusCounts(counts.Sum(x => x.Count),
            Of(TechnicianStatus.Available), Of(TechnicianStatus.Busy), Of(TechnicianStatus.Offline));
    }

    public async Task<IReadOnlyList<LabelledAmount>> GetMonthlyEarningsAsync(
        int technicianProfileId, int months, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-(months - 1));

        var rows = await _db.Bills.AsNoTracking()
            .Where(b => b.TechnicianProfileId == technicianProfileId
                        && b.PaymentStatus == PaymentStatus.Paid
                        && b.PaidAt >= start)
            .GroupBy(b => new { b.PaidAt!.Value.Year, b.PaidAt.Value.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(b => b.TotalAmount) })
            .ToListAsync(ct);

        return Enumerable.Range(0, months)
            .Select(i => start.AddMonths(i))
            .Select(m => new LabelledAmount(
                m.ToString("MMM yyyy"),
                rows.FirstOrDefault(r => r.Year == m.Year && r.Month == m.Month)?.Total ?? 0m))
            .ToList();
    }

    private IQueryable<TechnicianSummary> Project(IQueryable<TechnicianProfile> query) =>
        query.Select(t => new TechnicianSummary
        {
            TechnicianProfileId = t.TechnicianProfileId,
            UserId = t.UserId,
            FullName = t.User.FullName,
            Email = t.User.Email ?? string.Empty,
            Phone = t.User.Phone ?? string.Empty,
            Skill = t.Skill,
            Status = t.Status,
            Rating = t.Rating,
            TotalJobsCompleted = t.TotalJobsCompleted,
            IsActive = t.User.IsActive,
            Bio = t.Bio,
            YearsOfExperience = t.YearsOfExperience,
            ServiceAreaPinCode = t.ServiceAreaPinCode,
            ServiceZoneId = t.ServiceZoneId,
            TotalEarned = _db.Bills
                .Where(b => b.TechnicianProfileId == t.TechnicianProfileId && b.PaymentStatus == PaymentStatus.Paid)
                .Sum(b => (decimal?)b.TotalAmount) ?? 0m,
            ActiveJobCount = _db.ServiceRequests
                .Count(r => r.AssignedTechnicianProfileId == t.TechnicianProfileId
                            && (r.Status == RequestStatus.Assigned || r.Status == RequestStatus.InProgress))
        });
}
