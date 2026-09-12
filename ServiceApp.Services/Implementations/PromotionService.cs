// =================================================================
//  PromotionService.cs — ServiceApp.Services/Implementations
//
//  LOYALTY POINT RULES:
//  Earn: 1 point per ₹10 spent (floor)
//        e.g. ₹850 → 85 points
//  Redeem: 100 points = ₹10 discount
//        e.g. 250 points → max ₹25 discount
//  Expiry: points expire 12 months from earning date
//
//  REFERRAL RULES:
//  Referrer gets 200 points when a friend completes first booking
//  New customer gets 100 points on signup with a valid referral code
// =================================================================

using Microsoft.Extensions.Logging;
using ServiceApp.Core.Common;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Interfaces;

namespace ServiceApp.Services.Implementations
{
    public class PromotionService : IPromotionService
    {
        private readonly IUnitOfWork _uow;
        private readonly ILogger<PromotionService> _logger;

        // Points configuration
        private const int PointsPerRupee = 1;   // 1 pt per ₹10
        private const int RupeesPerPoint = 10;  // earn threshold
        private const int PointsPerDiscount = 100; // 100 pts = ₹10
        private const decimal DiscountPerHundred = 10m;
        private const int ReferrerBonusPoints = 200;
        private const int RefereeSignupPoints = 100;
        private const int PointsExpiryMonths = 12;

        public PromotionService(
            IUnitOfWork uow,
            ILogger<PromotionService> logger)
        {
            _uow = uow;
            _logger = logger;
        }

        // =============================================================
        //  VALIDATE PROMO CODE
        // =============================================================
        public async Task<Result<decimal>> ValidatePromoCodeAsync(
            string code,
            string customerId,
            decimal billAmount)
        {
            if (string.IsNullOrWhiteSpace(code))
                return Result<decimal>.Failure("Enter a promo code.");

            var promo = await _uow.PromoCodes
                .GetByCodeAsync(code.ToUpper().Trim());

            if (promo == null)
                return Result<decimal>.Failure(
                    $"Promo code '{code}' not found.");

            if (!promo.IsValid())
                return Result<decimal>.Failure(
                    "This promo code has expired or is no longer active.");

            // Check if customer already used it
            var alreadyUsed = await _uow.PromoCodes
                .HasCustomerUsedCodeAsync(customerId, promo.PromoCodeId);
            if (alreadyUsed)
                return Result<decimal>.Failure(
                    "You have already used this promo code.");

            // FirstOnly check
            if (promo.DiscountType == "FirstOnly")
            {
                var isFirst = await _uow.PromoCodes
                    .IsFirstBookingAsync(customerId);
                if (!isFirst)
                    return Result<decimal>.Failure(
                        "This code is valid for first bookings only.");
            }

            // Minimum amount check
            if (billAmount < promo.MinimumAmount)
                return Result<decimal>.Failure(
                    $"Minimum order of ₹{promo.MinimumAmount:N2} " +
                    "required to use this code.");

            var discount = promo.CalculateDiscount(billAmount);
            return Result<decimal>.Success(discount);
        }

        // =============================================================
        //  APPLY PROMO CODE
        //  Called after payment is confirmed — inserts usage row.
        // =============================================================
        public async Task<Result<decimal>> ApplyPromoCodeAsync(
            string code,
            string customerId,
            int requestId,
            decimal billAmount)
        {
            // Validate first
            var validation = await ValidatePromoCodeAsync(
                code, customerId, billAmount);
            if (!validation.IsSuccess)
                return validation;

            var promo = await _uow.PromoCodes
                .GetByCodeAsync(code.ToUpper().Trim());
            var discount = promo!.CalculateDiscount(billAmount);

            await _uow.BeginTransactionAsync();
            try
            {
                // Insert usage record
                var usage = new PromoUsage
                {
                    PromoCodeId = promo.PromoCodeId,
                    CustomerId = customerId,
                    RequestId = requestId,
                    DiscountApplied = discount,
                    UsedAt = DateTime.UtcNow
                };
                await _context_PromoUsages_AddAsync(usage);

                // Increment used count
                promo.UsedCount++;
                _uow.PromoCodes.Update(promo);

                await _uow.CommitTransactionAsync();

                _logger.LogInformation(
                    "Promo code {Code} applied by customer {CustomerId}. " +
                    "Discount: ₹{Discount}",
                    code, customerId, discount);

                return Result<decimal>.Success(discount);
            }
            catch (Exception ex)
            {
                await _uow.RollbackTransactionAsync();
                _logger.LogError(ex,
                    "Failed to apply promo code {Code}", code);
                return Result<decimal>.Failure(
                    "Failed to apply promo code.");
            }
        }

        // =============================================================
        //  GET POINTS BALANCE
        // =============================================================
        public async Task<int> GetPointsBalanceAsync(string customerId) =>
            await _uow.Loyalty.GetPointsBalanceAsync(customerId);

        // =============================================================
        //  AWARD POINTS after payment
        //  1 point per ₹10 spent, expires in 12 months
        // =============================================================
        public async Task AwardPointsAsync(
            string customerId,
            int requestId,
            decimal amountPaid)
        {
            var pointsEarned = (int)Math.Floor(
                amountPaid / RupeesPerPoint) * PointsPerRupee;

            if (pointsEarned <= 0) return;

            var entry = new LoyaltyLedger
            {
                CustomerId = customerId,
                Points = pointsEarned,
                TransactionType = "Earned",
                RequestId = requestId,
                Note = $"Earned {pointsEarned} points " +
                                  $"for ₹{amountPaid:N2} payment " +
                                  $"on request #{requestId}.",
                TransactedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow
                    .AddMonths(PointsExpiryMonths)
            };

            await _uow.Loyalty.AddAsync(entry);
            await _uow.SaveChangesAsync();

            _logger.LogInformation(
                "Awarded {Points} loyalty points to customer {CustomerId}",
                pointsEarned, customerId);
        }

        // =============================================================
        //  REDEEM POINTS as a discount
        //  100 points = ₹10 discount
        // =============================================================
        public async Task<Result<decimal>> RedeemPointsAsync(
            string customerId,
            int requestId,
            int pointsToRedeem)
        {
            if (pointsToRedeem <= 0)
                return Result<decimal>.Failure(
                    "Enter a valid number of points to redeem.");

            if (pointsToRedeem % PointsPerDiscount != 0)
                return Result<decimal>.Failure(
                    $"Points must be redeemed in multiples of " +
                    $"{PointsPerDiscount}.");

            var balance = await GetPointsBalanceAsync(customerId);
            if (pointsToRedeem > balance)
                return Result<decimal>.Failure(
                    $"You only have {balance} points available.");

            var discount = (pointsToRedeem / PointsPerDiscount)
                           * DiscountPerHundred;

            var entry = new LoyaltyLedger
            {
                CustomerId = customerId,
                Points = -pointsToRedeem, // negative = spent
                TransactionType = "Redeemed",
                RequestId = requestId,
                Note = $"Redeemed {pointsToRedeem} points " +
                                  $"for ₹{discount:N2} discount " +
                                  $"on request #{requestId}.",
                TransactedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddYears(100)
            };

            await _uow.Loyalty.AddAsync(entry);
            await _uow.SaveChangesAsync();

            _logger.LogInformation(
                "Customer {CustomerId} redeemed {Points} points " +
                "for ₹{Discount} discount",
                customerId, pointsToRedeem, discount);

            return Result<decimal>.Success(discount);
        }

        // =============================================================
        //  POINTS HISTORY
        // =============================================================
        public async Task<IEnumerable<LoyaltyLedger>>
            GetPointsHistoryAsync(string customerId) =>
            await _uow.Loyalty.GetHistoryAsync(customerId);

        // =============================================================
        //  GET OR CREATE REFERRAL CODE
        // =============================================================
        public async Task<string> GetOrCreateReferralCodeAsync(
            string customerId)
        {
            // Return existing code if already has one
            var existing = await _uow.Referrals
                .GetByOwnerAsync(customerId);
            if (existing != null)
                return existing.Code;

            // Generate a new unique code
            var code = await _uow.Referrals
                .GenerateUniqueCodeAsync(customerId);
            var referral = new ReferralCode
            {
                OwnerId = customerId,
                Code = code,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await _uow.Referrals.AddAsync(referral);
            await _uow.SaveChangesAsync();

            return code;
        }

        // =============================================================
        //  APPLY REFERRAL CODE on signup
        // =============================================================
        public async Task ApplyReferralCodeAsync(
            string newCustomerId,
            string referralCode)
        {
            if (string.IsNullOrWhiteSpace(referralCode)) return;

            var referral = await _uow.Referrals
                .GetByCodeAsync(referralCode.ToUpper().Trim());

            if (referral == null || !referral.IsActive) return;
            if (referral.OwnerId == newCustomerId) return; // can't refer yourself

            await _uow.BeginTransactionAsync();
            try
            {
                var now = DateTime.UtcNow;

                // Award points to new customer (signup bonus)
                await _uow.Loyalty.AddAsync(new LoyaltyLedger
                {
                    CustomerId = newCustomerId,
                    Points = RefereeSignupPoints,
                    TransactionType = "Referral",
                    Note = $"Welcome bonus — " +
                                      $"signed up with referral code {referralCode}",
                    TransactedAt = now,
                    ExpiresAt = now.AddMonths(PointsExpiryMonths)
                });

                // Award points to referrer
                await _uow.Loyalty.AddAsync(new LoyaltyLedger
                {
                    CustomerId = referral.OwnerId,
                    Points = ReferrerBonusPoints,
                    TransactionType = "Referral",
                    Note = $"Referral bonus — " +
                                      $"a friend signed up using your code.",
                    TransactedAt = now,
                    ExpiresAt = now.AddMonths(PointsExpiryMonths)
                });

                // Increment usage count
                referral.TimesUsed++;
                _uow.Referrals.Update(referral);

                await _uow.CommitTransactionAsync();

                _logger.LogInformation(
                    "Referral code {Code} applied. Referee: {NewId}, " +
                    "Referrer: {OwnerId}",
                    referralCode, newCustomerId, referral.OwnerId);
            }
            catch (Exception ex)
            {
                await _uow.RollbackTransactionAsync();
                _logger.LogWarning(ex,
                    "Failed to apply referral code {Code}", referralCode);
            }
        }

        // Internal helper — EF context access for PromoUsage
        private async Task _context_PromoUsages_AddAsync(PromoUsage usage)
        {
            // PromoUsage has no repository — use the Bills repo's context
            // via a direct DbContext access through UoW SaveChangesAsync
            await _uow.PromoCodes.AddUsageAsync(usage);
        }
    }
}