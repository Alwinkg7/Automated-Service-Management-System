// =================================================================
//  PromotionEntities.cs — ServiceApp.Core/Entities
//
//  Three entities power the promotions system:
//
//  PromoCode    → admin creates discount codes
//  PromoUsage   → tracks which customer used which code
//  LoyaltyLedger→ every points earn/spend transaction
//
//  PROMO CODE TYPES:
//  "Flat"       → fixed ₹ discount (e.g. ₹100 off)
//  "Percentage" → % discount (e.g. 10% off, max ₹500)
//  "FirstOnly"  → flat discount but only valid on first booking
//
//  LOYALTY POINTS:
//  Earn 1 point per ₹10 spent (configurable)
//  100 points = ₹10 discount on next booking
//  Points expire after 12 months from earning date
// =================================================================

namespace ServiceApp.Core.Entities
{
    // ----------------------------------------------------------
    //  PromoCode — admin creates these
    // ----------------------------------------------------------
    public class PromoCode
    {
        public int PromoCodeId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        // "Flat", "Percentage", "FirstOnly"
        public string DiscountType { get; set; } = "Flat";

        // For Flat/FirstOnly: ₹ amount off
        // For Percentage: percentage (e.g. 10 = 10%)
        public decimal DiscountValue { get; set; }

        // For Percentage: max discount cap (e.g. max ₹500)
        public decimal? MaxDiscount { get; set; }

        // Minimum bill amount to apply code
        public decimal MinimumAmount { get; set; }

        // How many times this code can be used in total
        // Null = unlimited
        public int? MaxUses { get; set; }

        // How many times it has been used
        public int UsedCount { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; set; }

        // Navigation
        public ICollection<PromoUsage> Usages { get; set; }
            = new List<PromoUsage>();

        // Helper — check if code is still valid
        public bool IsValid() =>
            IsActive
            && (ExpiresAt == null || ExpiresAt > DateTime.UtcNow)
            && (MaxUses == null || UsedCount < MaxUses);

        // Calculate actual discount for a given bill amount
        public decimal CalculateDiscount(decimal billAmount)
        {
            if (billAmount < MinimumAmount) return 0;

            decimal discount = DiscountType == "Percentage"
                ? Math.Round(billAmount * DiscountValue / 100, 2)
                : DiscountValue;

            // Cap at MaxDiscount if percentage type
            if (DiscountType == "Percentage" && MaxDiscount.HasValue)
                discount = Math.Min(discount, MaxDiscount.Value);

            // Never discount more than the bill
            return Math.Min(discount, billAmount);
        }
    }

    // ----------------------------------------------------------
    //  PromoUsage — one row per code redemption
    // ----------------------------------------------------------
    public class PromoUsage
    {
        public int PromoUsageId { get; set; }
        public int PromoCodeId { get; set; }
        public string CustomerId { get; set; } = string.Empty;
        public int RequestId { get; set; }
        public decimal DiscountApplied { get; set; }
        public DateTime UsedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public PromoCode PromoCode { get; set; } = null!;
        public ApplicationUser Customer { get; set; } = null!;
    }

    // ----------------------------------------------------------
    //  LoyaltyLedger — every points transaction
    //  Each row is either an EARN or a SPEND
    // ----------------------------------------------------------
    public class LoyaltyLedger
    {
        public int LedgerId { get; set; }
        public string CustomerId { get; set; } = string.Empty;

        // Positive = earned, Negative = spent
        public int Points { get; set; }

        // "Earned", "Redeemed", "Expired", "Referral"
        public string TransactionType { get; set; } = string.Empty;

        public int? RequestId { get; set; }
        public string? Note { get; set; }
        public DateTime TransactedAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAt { get; set; }

        // Navigation
        public ApplicationUser Customer { get; set; } = null!;
    }

    // ----------------------------------------------------------
    //  ReferralCode — customer gets a unique referral code
    //  When friend uses it → both get loyalty points
    // ----------------------------------------------------------
    public class ReferralCode
    {
        public int ReferralCodeId { get; set; }
        public string OwnerId { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public int TimesUsed { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;

        // Navigation
        public ApplicationUser Owner { get; set; } = null!;
    }
}