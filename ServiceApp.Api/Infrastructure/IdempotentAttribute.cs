using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ServiceApp.Api.Infrastructure;

/// <summary>
/// Honours an optional <c>Idempotency-Key</c> header: a successful (2xx) response
/// is stored for 24 hours per user + route + key, and a repeated request with the
/// same key replays it instead of executing the action again. Concurrent requests
/// with the same key are serialised, so a double click cannot create two records.
/// Error responses are not stored, so the client can retry after fixing input.
/// </summary>
/// <remarks>
/// Storage is the in-process <see cref="IMemoryCache"/>: keys survive only for the
/// lifetime of one API instance. Move to IDistributedCache (Redis) when scaling out.
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public sealed class IdempotentAttribute : Attribute, IAsyncActionFilter
{
    public const string HeaderName = "Idempotency-Key";
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new();

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        var key = http.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrWhiteSpace(key))
        {
            await next();
            return;
        }

        if (key.Length > 100)
        {
            context.Result = new BadRequestObjectResult(new { success = false, error = $"{HeaderName} must be at most 100 characters." });
            return;
        }

        var user = http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
        var cacheKey = $"idem:{user}:{http.Request.Method}:{http.Request.Path}:{key}";
        var cache = http.RequestServices.GetRequiredService<IMemoryCache>();

        var gate = Locks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(http.RequestAborted);
        try
        {
            if (cache.TryGetValue(cacheKey, out StoredResponse? stored) && stored != null)
            {
                http.Response.Headers["Idempotent-Replayed"] = "true";
                context.Result = new ContentResult
                {
                    StatusCode = stored.StatusCode,
                    Content = stored.Body,
                    ContentType = "application/json"
                };
                return;
            }

            var executed = await next();
            if (executed.Exception == null
                && executed.Result is ObjectResult { StatusCode: null or >= 200 and < 300 } ok)
            {
                var json = http.RequestServices.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;
                var body = JsonSerializer.Serialize(ok.Value, json);
                cache.Set(cacheKey, new StoredResponse(ok.StatusCode ?? 200, body),
                    new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl, Size = 1 });
            }
        }
        finally
        {
            gate.Release();
            // Drop the lock once nobody is waiting; a stored response now guards replays.
            if (gate.CurrentCount == 1)
                Locks.TryRemove(new KeyValuePair<string, SemaphoreSlim>(cacheKey, gate));
        }
    }

    private sealed record StoredResponse(int StatusCode, string Body);
}
