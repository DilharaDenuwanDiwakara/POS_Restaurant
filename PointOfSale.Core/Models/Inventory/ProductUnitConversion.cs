using System;

namespace PointOfSale.Core.Models.Inventory
{
    public class ProductUnitConversion
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int TargetUnitMeasureId { get; set; }
        public string TargetUnitMeasureCode { get; set; }
        public string TargetUnitMeasureName { get; set; }
        public decimal ConversionRate { get; set; }
        public bool IsActive { get; set; } = true;
        public int? CreatedBy { get; set; }
        public DateTime? CreatedAt { get; set; }
        public int? UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
