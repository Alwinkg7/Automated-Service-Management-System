using ServiceApp.Core.Entities;

namespace ServiceApp.Web.Areas.Customer.Models
{
    public class PromotionsViewModel
    {
        // Loyalty
        public int PointsBalance { get; set; }
        public decimal PointsValue { get; set; } // balance / 100 * 10
        public string ReferralCode { get; set; } = string.Empty;
        public List<LoyaltyLedger> PointsHistory { get; set; } = new();

        // Promo code check
        public string? PromoMessage { get; set; }
        public decimal PromoDiscount { get; set; }
        public bool PromoValid { get; set; }
    }
}