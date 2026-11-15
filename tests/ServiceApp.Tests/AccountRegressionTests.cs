using Microsoft.EntityFrameworkCore;
using ServiceApp.Core.Common;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;
using ServiceApp.Core.Models.Accounts;
using ServiceApp.Services.Implementations;

namespace ServiceApp.Tests;

/// <summary>BUG-01 (registration crashed on a never-injected promotions service) and BUG-17.</summary>
public class AccountRegressionTests
{
    private static AccountService CreateService(TestDb db) =>
        new(db.Users, db.Uow, new PromotionService(db.Uow, TestDb.Log<PromotionService>()), TestDb.Log<AccountService>());

    [Fact]
    public async Task Bug01_Customer_registration_creates_user_profile_role_and_referral_code()
    {
        using var db = new TestDb();

        var result = await CreateService(db).RegisterCustomerAsync(
            new CustomerRegistration("Asha Rao", "asha@example.com", "9876543210", "Secret123", ReferralCode: null));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        var user = result.Data!;
        Assert.Equal(UserRole.Customer, user.Role);
        Assert.True(await db.Users.IsInRoleAsync(user, "Customer"));
        Assert.True(await db.Context.CustomerProfiles.AnyAsync(p => p.UserId == user.Id));
        Assert.True(await db.Context.ReferralCodes.AnyAsync(r => r.OwnerId == user.Id));
    }

    [Fact]
    public async Task Bug01_Registration_with_referral_code_succeeds()
    {
        using var db = new TestDb();
        var svc = CreateService(db);
        var referrer = (await svc.RegisterCustomerAsync(
            new CustomerRegistration("Ref", "ref@example.com", "9876543210", "Secret123", null))).Data!;
        var code = (await db.Context.ReferralCodes.SingleAsync(r => r.OwnerId == referrer.Id)).Code;

        var result = await svc.RegisterCustomerAsync(
            new CustomerRegistration("New", "new@example.com", "9876543211", "Secret123", code));

        Assert.True(result.IsSuccess, result.ErrorMessage);
    }

    [Fact]
    public async Task Duplicate_email_is_a_conflict()
    {
        using var db = new TestDb();
        var svc = CreateService(db);
        await svc.RegisterCustomerAsync(new CustomerRegistration("A", "dup@example.com", "9876543210", "Secret123", null));

        var again = await svc.RegisterCustomerAsync(new CustomerRegistration("B", "dup@example.com", "9876543210", "Secret123", null));

        Assert.Equal(ErrorType.Conflict, again.ErrorType);
    }

    [Fact]
    public async Task Bug17_Admin_cannot_deactivate_self_or_the_last_admin()
    {
        using var db = new TestDb();
        var admin = new ApplicationUser { UserName = "a@x.com", Email = "a@x.com", FullName = "Admin", Role = UserRole.Admin };
        await db.Users.CreateAsync(admin, "Secret123");
        await db.Users.AddToRoleAsync(admin, "Admin");
        var svc = CreateService(db);

        Assert.Equal(ErrorType.Conflict, (await svc.SetActiveAsync(admin.Id, false, admin.Id)).ErrorType);
        Assert.Equal(ErrorType.Conflict, (await svc.SetActiveAsync(admin.Id, false, "another-admin")).ErrorType);
    }
}
