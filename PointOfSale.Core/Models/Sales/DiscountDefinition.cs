using System;

namespace PointOfSale.Core.Models.Sales
{
    public class DiscountDefinition
    {
        public int DiscountId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string DiscountType { get; set; } // PERCENT / FLAT
        public decimal DiscountValue { get; set; }
        public decimal MinimumBillAmount { get; set; }
        public decimal? MaximumDiscountAmount { get; set; }
        public bool IsSingleUse { get; set; }
        public int? MaxRedemptionCount { get; set; }
        public int CurrentRedemptionCount { get; set; }
        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }
        public int? BranchId { get; set; }
        public bool IsActive { get; set; }
        public bool IsAutoApply { get; set; }
        public string ApplyScope { get; set; } // BILL / CATEGORY
        public int? TargetMenuCategoryId { get; set; }
        public int? DayOfWeekMask { get; set; } // Sun=1, Mon=2, Tue=4, Wed=8, Thu=16, Fri=32, Sat=64
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public int CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}



