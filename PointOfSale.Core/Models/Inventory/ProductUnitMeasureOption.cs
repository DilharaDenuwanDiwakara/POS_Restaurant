namespace PointOfSale.Core.Models.Inventory
{
    public class ProductUnitMeasureOption
    {
        public int UnitMeasureId { get; set; }
        public string Code { get; set; }
        public string UnitMeasureName { get; set; }
        public bool IsBaseUnit { get; set; }
        public decimal ConversionRate { get; set; } = 1m;
        public bool IsMultiply { get; set; } = true;

        public string DisplayName =>
            !string.IsNullOrWhiteSpace(Code)
                ? Code
                : UnitMeasureName;
    }
}
