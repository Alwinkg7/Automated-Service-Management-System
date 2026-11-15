using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ServiceApp.Core.Common;

namespace ServiceApp.Api.Infrastructure;

/// <summary>
/// Maps unhandled exceptions to ProblemDetails. Details (SQL, stack traces) are
/// logged, never returned.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private static readonly HashSet<int> TransientSqlErrors = [-2, 53, 233, 1205, 4060, 10053, 10054, 10060, 40197, 40501, 40613];

    private readonly IProblemDetailsService _problems;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(IProblemDetailsService problems, ILogger<GlobalExceptionHandler> logger)
    {
        _problems = problems;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
            return true; // client went away; nothing to write

        var (status, title, detail) = Classify(exception);

        if (status >= 500)
            _logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        else
            _logger.LogWarning(exception, "Request failed with {Status} for {Method} {Path}", status, httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = status;
        return await _problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail }
        });
    }

    private static (int Status, string Title, string Detail) Classify(Exception ex) => ex switch
    {
        DbUpdateConcurrencyException =>
            (409, "Conflict", "The resource was modified by someone else. Reload and try again."),
        DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } } =>
            (409, "Conflict", "A record with the same unique value already exists."),
        SqlException sql when TransientSqlErrors.Contains(sql.Number) =>
            (503, "Service Unavailable", "The database is temporarily unavailable. Retry later."),
        UpstreamServiceException up =>
            (502, "Bad Gateway", $"{up.Provider} request failed."),
        TimeoutException or TaskCanceledException =>
            (504, "Gateway Timeout", "An upstream service timed out."),
        BadHttpRequestException bad =>
            (bad.StatusCode, "Bad Request", "The request could not be read."),
        _ => (500, "Internal Server Error", "An unexpected error occurred.")
    };
}
