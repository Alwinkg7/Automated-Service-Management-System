using ServiceApp.Core.Common;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Models.Accounts;

namespace ServiceApp.Core.Interfaces;

/// <summary>
/// Host-agnostic account registration. Creates the Identity user, role
/// membership and the role profile row. Signing in (cookie or JWT) is the
/// caller's responsibility.
/// </summary>
public interface IAccountService
{
    /// <summary>Registers a customer. Fails with <see cref="ErrorType.Conflict"/> when the email is taken.</summary>
    Task<Result<ApplicationUser>> RegisterCustomerAsync(CustomerRegistration input);

    /// <summary>Registers a technician. Fails with <see cref="ErrorType.Conflict"/> when the email is taken.</summary>
    Task<Result<ApplicationUser>> RegisterTechnicianAsync(TechnicianRegistration input);

    /// <summary>
    /// Activates or deactivates a user. Admins cannot deactivate themselves or the last
    /// active admin (<see cref="ErrorType.Conflict"/>). Rotates the security stamp on
    /// deactivation so existing cookies are invalidated.
    /// </summary>
    Task<Result<ApplicationUser>> SetActiveAsync(string userId, bool isActive, string actingAdminUserId);
}
