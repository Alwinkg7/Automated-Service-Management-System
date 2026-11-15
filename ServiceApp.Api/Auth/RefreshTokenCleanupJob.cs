using Microsoft.EntityFrameworkCore;
using ServiceApp.Data.Context;

namespace ServiceApp.Api.Auth;

/// <summary>
/// Hangfire recurring job: deletes expired refresh-token rows from
/// AspNetUserTokens. Rotation and logout already delete used tokens; this
/// removes tokens that were issued but never used before they expired.
/// </summary>
public class RefreshTokenCleanupJob
{
    private const int BatchSize = 500;

    private readonly ApplicationDbContext _db;
    private readonly ILogger<RefreshTokenCleanupJob> _logger;

    public RefreshTokenCleanupJob(ApplicationDbContext db, ILogger<RefreshTokenCleanupJob> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Removes expired tokens in batches and returns the number deleted.</summary>
    public async Task<int> RunAsync()
    {
        var nowTicks = DateTime.UtcNow.Ticks;
        var deleted = 0;
        var skip = 0;

        while (true)
        {
            // Stored value is "{expiresUtcTicks}|{sha256}"; expiry is parsed client-side per batch.
            var batch = await _db.UserTokens
                .Where(t => t.LoginProvider == TokenService.RefreshProvider)
                .OrderBy(t => t.UserId).ThenBy(t => t.Name)
                .Skip(skip)
                .Take(BatchSize)
                .ToListAsync();
            if (batch.Count == 0) break;

            var expired = batch.Where(t => IsExpired(t.Value, nowTicks)).ToList();
            if (expired.Count > 0)
            {
                _db.UserTokens.RemoveRange(expired);
                await _db.SaveChangesAsync();
                deleted += expired.Count;
            }

            _db.ChangeTracker.Clear();
            skip += batch.Count - expired.Count;
            if (batch.Count < BatchSize) break;
        }

        _logger.LogInformation("Refresh-token cleanup removed {Count} expired tokens", deleted);
        return deleted;
    }

    /// <summary>Malformed values are treated as expired.</summary>
    public static bool IsExpired(string? value, long nowTicks)
    {
        var separator = value?.IndexOf('|') ?? -1;
        return separator <= 0 || !long.TryParse(value![..separator], out var ticks) || ticks <= nowTicks;
    }
}
