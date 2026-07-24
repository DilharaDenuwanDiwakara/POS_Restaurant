namespace PointOfSale.Core.Models.Sales
{
    public class DiscountValidationResult
    {
        public bool IsValid { get; set; }
        public string Message { get; set; }
        public int DiscountId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string DiscountType { get; set; } // PERCENT / FLAT
        public decimal DiscountValue { get; set; }
        public decimal MinimumBillAmount { get; set; }
        public decimal? MaximumDiscountAmount { get; set; }
        public bool IsSingleUse { get; set; }
    }
}
