namespace PointOfSale.Core.DTOs
{
    public class ProductRawDto
    {
        public int CategoryId { get; set; }
        public int? BrandId { get; set; }
        public string ProductCode { get; set; }
        public string Barcode { get; set; }
        public string ProductName { get; set; }
        public string GenericName { get; set; }
        public string UnitMeasureCode { get; set; }
        public decimal StandardCost { get; set; }
        public decimal SellingPrice { get; set; }
        public int ReorderPoint { get; set; }
        public int MaxStockQuantity { get; set; }
        public int ExpiryReminderDays { get; set; }
        public bool IsService { get; set; }
        public bool IsTaxApplicable { get; set; }
        public bool IsActive { get; set; }
    }
}
