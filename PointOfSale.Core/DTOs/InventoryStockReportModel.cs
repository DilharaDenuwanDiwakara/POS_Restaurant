namespace PointOfSale.Core.DTOs
{
    public class InventoryStockReportModel
    {
        public string ProductCode { get; set; }
        public string ProductName { get; set; }
        public string CategoryName { get; set; }
        public decimal CurrentStock { get; set; }
        public string UnitMeasure { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalValue { get; set; }
    }
}
