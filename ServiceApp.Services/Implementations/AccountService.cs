using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using ServiceApp.Core.Common;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;
using ServiceApp.Core.Interfaces;
using ServiceApp.Core.Models.Accounts;

namespace ServiceApp.Services.Implementations;

/// <summary>
/// Registers customers and technicians: Identity user + role + profile row.
/// If the profile cannot be created the Identity user is deleted again so the
/// email can be reused.
/// </summary>
public class AccountService : IAccountService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUnitOfWork _uow;
    private readonly IPromotionService _promotions;
    private readonly ILogger<AccountService> _logger;

    public AccountService(
        UserManager<ApplicationUser> userManager,
        IUnitOfWork uow,
        IPromotionService promotions,
        ILogger<AccountService> logger)
    {
        _userManager = userManager;
        _uow = uow;
        _promotions = promotions;
        _logger = logger;
    }

    public async Task<Result<ApplicationUser>> RegisterCustomerAsync(CustomerRegistration input)
    {
        var created = await CreateUserAsync(input.FullName, input.Email, input.Phone, input.Password, UserRole.Customer);
        if (!created.IsSuccess)
            return created;

        var user = created.Data!;
        var profileSaved = await SaveProfileAsync(user, () =>
            _uow.CustomerProfiles.AddAsync(new CustomerProfile { UserId = user.Id }));
        if (!profileSaved)
            return Result<ApplicationUser>.Failure("Registration failed. Please try again.", ErrorType.Unexpected);

        // Promotions are best-effort and run after the profile is committed
        // (each manages its own SaveChanges/transaction on the shared DbContext).
        try
        {
            if (!string.IsNullOrWhiteSpace(input.ReferralCode))
                await _promotions.ApplyReferralCodeAsync(user.Id, input.ReferralCode);

            await _promotions.GetOrCreateReferralCodeAsync(user.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Referral setup failed for new customer {UserId}", user.Id);
        }

        _logger.LogInformation("New customer registered: {UserId}", user.Id);
        return Result<ApplicationUser>.Success(user);
    }

    public async Task<Result<ApplicationUser>> RegisterTechnicianAsync(TechnicianRegistration input)
    {
        var created = await CreateUserAsync(input.FullName, input.Email, input.Phone, input.Password, UserRole.Technician);
        if (!created.IsSuccess)
            return created;

        var user = created.Data!;
        var profileSaved = await SaveProfileAsync(user, () =>
            _uow.TechnicianProfiles.AddAsync(new TechnicianProfile
            {
                UserId = user.Id,
                Skill = input.Skill,
                Status = TechnicianStatus.Available
            }));
        if (!profileSaved)
            return Result<ApplicationUser>.Failure("Registration failed. Please try again.", ErrorType.Unexpected);

        _logger.LogInformation("New technician registered: {UserId} Skill: {Skill}", user.Id, input.Skill);
        return Result<ApplicationUser>.Success(user);
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private async Task<Result<ApplicationUser>> CreateUserAsync(
        string fullName, string email, string phone, string password, UserRole role)
    {
        if (await _userManager.FindByEmailAsync(email) != null)
            return Result<ApplicationUser>.Failure(
                "This email is already registered. Please login instead.", ErrorType.Conflict);

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            Phone = phone,
            Role = role,
            EmailConfirmed = true, // e-mail verification is not implemented yet
            IsActive = true
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            return Result<ApplicationUser>.Failure(
                string.Join(" ", result.Errors.Select(e => e.Description)), ErrorType.Validation);

        var roleResult = await _userManager.AddToRoleAsync(user, role.ToString());
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            _logger.LogError("Adding role {Role} failed for {UserId}: {Errors}",
                role, user.Id, string.Join("; ", roleResult.Errors.Select(e => e.Code)));
            return Result<ApplicationUser>.Failure("Registration failed. Please try again.", ErrorType.Unexpected);
        }

        return Result<ApplicationUser>.Success(user);
    }

    private async Task<bool> SaveProfileAsync(ApplicationUser user, Func<Task> addProfile)
    {
        try
        {
            await _uow.BeginTransactionAsync();
            await addProfile();
            await _uow.CommitTransactionAsync();
            return true;
        }
        catch (Exception ex)
        {
            await _uow.RollbackTransactionAsync();
            await _userManager.DeleteAsync(user);
            _logger.LogError(ex, "Failed to create {Role} profile for {UserId}", user.Role, user.Id);
            return false;
        }
    }
}
