using System;
using System.Collections.Generic;

namespace PointOfSale.Core.DTOs
{
    public class DiscountDefinitionDto
    {
        public int DiscountId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string DiscountType { get; set; }
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
        public string ApplyScope { get; set; }
        public int? TargetMenuCategoryId { get; set; }
        public List<int> ExcludedProductIds { get; set; } = new List<int>();
        public int? DayOfWeekMask { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public int CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
