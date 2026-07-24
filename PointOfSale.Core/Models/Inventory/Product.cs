using System.Collections.Generic;

namespace PointOfSale.Core.Models.Inventory
{
    public class Product
    {
        public int ProductId { get; set; }
        public int CategoryId { get; set; }
        public string ProductCode { get; set; }
        public string Barcode { get; set; } = null;
        public string ProductName { get; set; }
        public int UnitMeasureId { get; set; }
        public string UnitMeasureName { get; set; }
        public string UnitMeasureCode { get; set; }
        public int? ItemTypeId { get; set; }
        public string ItemTypeName { get; set; }
        public decimal StandardCost { get; set; }
        public decimal AvailableQuantity { get; set; }
        public decimal ReorderPoint { get; set; }
        public decimal MaxStockQuantity { get; set; }
        public decimal AdditionalStockQuantity { get; set; }
        public decimal WastagePercentage { get; set; }
        public bool IsPurchasable { get; set; }
        public bool IsActive { get; set; }
        public bool IsTaxApplicable { get; set; }
        public bool TrackExpiry { get; set; }
        public int? CreatedBy { get; set; }
        public int? UpdatedBy { get; set; }

        public string CategoryName { get; set; }
        public List<ProductUnitConversion> UnitConversions { get; set; } = new List<ProductUnitConversion>();
    }
}
