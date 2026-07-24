using System;

namespace PointOfSale.Core.Models.Sales
{
    public class PromotionRule
    {
        public int PromotionRuleId { get; set; }
        public string RuleName { get; set; }
        public string PromotionType { get; set; } // BOGO_SAME_FREE, BOGO_SAME_PERCENT, BUY_A_GET_B_FREE
        public int BuyProductId { get; set; }
        public string BuyProductName { get; set; }
        public int? GetProductId { get; set; }
        public string GetProductName { get; set; }
        public int BuyQuantity { get; set; }
        public int GetQuantity { get; set; }
        public decimal DiscountPercent { get; set; }
        public int? DayOfWeekMask { get; set; } // Sun=1, Mon=2, Tue=4, Wed=8, Thu=16, Fri=32, Sat=64
        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }
        public int? BranchId { get; set; }
        public bool IsActive { get; set; }
        public int CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
