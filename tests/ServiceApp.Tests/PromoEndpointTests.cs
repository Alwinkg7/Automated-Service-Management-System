using Moq;
using ServiceApp.Api.Auth;
using ServiceApp.Api.Contracts;
using ServiceApp.Api.Controllers;
using ServiceApp.Core.Common;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Interfaces;
using ServiceApp.Services.Implementations;

namespace ServiceApp.Tests;

/// <summary>Promo codes on bills: preview/apply endpoints, order amount, admin catalogue rules.</summary>
public class PromoEndpointTests
{
    private static async Task SeedPromoAsync(TestDb db, string code = "SAVE50", decimal value = 50m)
    {
        db.Context.PromoCodes.Add(new PromoCode
        {
            Code = code, DiscountType = "Flat", DiscountValue = value, MinimumAmount = 100m, IsActive = true
        });
        await db.Context.SaveChangesAsync();
    }

    private static BillDiscountService Discounts(TestDb db) =>
        new(db.Uow, new PromotionService(db.Uow, TestDb.Log<PromotionService>()), TestDb.Log<BillDiscountService>());

    [Fact]
    public async Task Apply_promo_endpoint_discounts_once_and_preview_does_not_record()
    {
        using var db = new TestDb();
        var s = await db.SeedBilledJobAsync(700m);
        await SeedPromoAsync(db);
        var controller = new CustomerPromotionsApiController(
            new PromotionService(db.Uow, TestDb.Log<PromotionService>()), Discounts(db)).As(s.CustomerId, "Customer");

        var preview = await controller.PreviewPromo(s.BillId, new BillPromoRequest("save50"));
        Assert.Equal(200, preview.Status());
        Assert.Equal(0m, (await db.Uow.Bills.GetByIdAsync(s.BillId))!.DiscountAmount);

        var applied = await controller.ApplyPromo(s.BillId, new BillPromoRequest("SAVE50"));
        var again = await controller.ApplyPromo(s.BillId, new BillPromoRequest("SAVE50"));

        Assert.Equal(200, applied.Status());
        Assert.Equal(409, again.Status());
        var bill = (await db.Uow.Bills.GetByIdAsync(s.BillId))!;
        Assert.Equal(650m, bill.PayableAmount);
        Assert.Single(db.Context.PromoUsages);
    }

    [Fact]
    public async Task Other_customer_cannot_discount_the_bill()
    {
        using var db = new TestDb();
        var s = await db.SeedBilledJobAsync(700m);
        await SeedPromoAsync(db);

        var result = await Discounts(db).ApplyPromoAsync(s.BillId, "someone-else", "SAVE50");

        Assert.Equal(ErrorType.Forbidden, result.ErrorType);
    }

    [Fact]
    public async Task Razorpay_order_is_created_for_the_discounted_amount_and_checkout_is_idempotent()
    {
        using var db = new TestDb();
        var s = await db.SeedBilledJobAsync(700m);
        await SeedPromoAsync(db);
        decimal? charged = null;
        var razorpay = new Mock<IRazorpayService>();
        razorpay.Setup(r => r.CreateOrderAsync(It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>()))
            .Callback<decimal, string, string, int?>((amount, _, _, _) => charged = amount)
            .ReturnsAsync(new RazorpayOrderResult { OrderId = "order_1", Amount = 65000, Currency = "INR" });
        var payments = new PaymentService(db.Uow, TestDb.Log<PaymentService>(), razorpay.Object,
            Mock.Of<INotificationService>(), Mock.Of<IEmailService>(),
            new PromotionService(db.Uow, TestDb.Log<PromotionService>()));
        var discounts = Discounts(db);

        var first = await discounts.PrepareCheckoutAsync(s.BillId, s.CustomerId, "SAVE50", redeemMaxPoints: true);
        var second = await discounts.PrepareCheckoutAsync(s.BillId, s.CustomerId, "SAVE50", redeemMaxPoints: true);
        var order = await payments.CreateRazorpayOrderAsync(s.BillId, s.CustomerId);

        Assert.True(first.IsSuccess, first.ErrorMessage);
        Assert.True(second.IsSuccess, second.ErrorMessage);
        Assert.Equal(650m, second.Data!.Payable);
        Assert.True(order.IsSuccess, order.ErrorMessage);
        Assert.Equal(650m, charged);
        Assert.Equal("order_1", (await db.Uow.Bills.GetByIdAsync(s.BillId))!.RazorpayOrderId);
    }

    [Fact]
    public async Task Admin_promo_catalogue_rejects_duplicates_and_deleting_used_codes()
    {
        using var db = new TestDb();
        var codes = new PromoCodeService(db.Uow, TestDb.Log<PromoCodeService>());
        var admin = new AdminCatalogApiController(
            new ZoneService(db.Uow, TestDb.Log<ZoneService>()), codes, Mock.Of<ITechnicianQueries>()).As("admin-1", "Admin");
        var body = new PromoCodeRequest("WELCOME", "Welcome", "Flat", 25m, null, 0m, null, null);

        Assert.Equal(201, (await admin.CreatePromoCode(body)).Status());
        Assert.Equal(409, (await admin.CreatePromoCode(body)).Status());
        Assert.Equal(400, (await admin.CreatePromoCode(body with { Code = "PCT", DiscountType = "Percentage", DiscountValue = 150m })).Status());

        var code = db.Context.PromoCodes.Single();
        code.UsedCount = 1;
        await db.Context.SaveChangesAsync();
        Assert.Equal(409, (await admin.DeletePromoCode(code.PromoCodeId)).Status());
    }

    [Fact]
    public async Task Zones_require_six_digit_unique_pin_codes()
    {
        using var db = new TestDb();
        var zones = new ZoneService(db.Uow, TestDb.Log<ZoneService>());

        Assert.True((await zones.CreateAsync(new ZoneInput("Kochi", "Kochi", "Kerala", "682001, 682002"))).IsSuccess);
        Assert.Equal(ErrorType.Conflict, (await zones.CreateAsync(new ZoneInput("Kochi East", "Kochi", "Kerala", "682002"))).ErrorType);
        Assert.Equal(ErrorType.Validation, (await zones.CreateAsync(new ZoneInput("Bad", "Kochi", "Kerala", "12345"))).ErrorType);
    }

    [Theory]
    [InlineData("0|abc", true)]
    [InlineData("not-a-token", true)]
    [InlineData(null, true)]
    public void Refresh_cleanup_treats_old_and_malformed_tokens_as_expired(string? value, bool expired) =>
        Assert.Equal(expired, RefreshTokenCleanupJob.IsExpired(value, DateTime.UtcNow.Ticks));

    [Fact]
    public void Refresh_cleanup_keeps_unexpired_tokens() =>
        Assert.False(RefreshTokenCleanupJob.IsExpired($"{DateTime.UtcNow.AddDays(5).Ticks}|hash", DateTime.UtcNow.Ticks));
}
