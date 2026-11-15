using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ServiceApp.Core.Common;

namespace ServiceApp.Api.Controllers;

/// <summary>
/// Base for all API controllers: caller identity plus the single place where
/// failures become RFC 7807 ProblemDetails. Every error body also carries the
/// legacy <c>success: false</c> and <c>error</c> fields used by existing clients
/// (added in <c>ProblemDetailsOptions.CustomizeProblemDetails</c>).
/// </summary>
[ApiController]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Authenticated user's id (JWT <c>sub</c>, mapped to NameIdentifier).</summary>
    protected string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    /// <summary>ProblemDetails response with an explicit status code.</summary>
    protected ObjectResult Fail(int statusCode, string? message)
    {
        var problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext, statusCode, detail: message);
        var result = new ObjectResult(problem) { StatusCode = statusCode };
        result.ContentTypes.Add("application/problem+json");
        return result;
    }

    /// <summary>ProblemDetails response for a failed <see cref="Result"/>.</summary>
    protected ObjectResult Fail(Result result) => Fail(ToStatusCode(result.ErrorType), result.ErrorMessage);

    /// <summary>ProblemDetails response for a failed <see cref="Result{T}"/>.</summary>
    protected ObjectResult Fail<T>(Result<T> result) => Fail(ToStatusCode(result.ErrorType), result.ErrorMessage);

    /// <summary>Central <see cref="ErrorType"/> to HTTP status mapping.</summary>
    public static int ToStatusCode(ErrorType errorType) => errorType switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.UnsupportedMedia => StatusCodes.Status415UnsupportedMediaType,
        ErrorType.BusinessRule => StatusCodes.Status422UnprocessableEntity,
        ErrorType.RateLimited => StatusCodes.Status429TooManyRequests,
        ErrorType.Dependency => StatusCodes.Status502BadGateway,
        ErrorType.Unavailable => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status500InternalServerError
    };
}
