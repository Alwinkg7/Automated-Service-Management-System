namespace ServiceApp.Core.Common;

/// <summary>
/// Classifies a failed <see cref="Result"/> so hosts can map it to the correct
/// HTTP status code (or MVC behaviour) without parsing error messages.
/// </summary>
public enum ErrorType
{
    None,
    Validation,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
    UnsupportedMedia,
    BusinessRule,
    RateLimited,
    Dependency,
    Unavailable,
    Unexpected
}
