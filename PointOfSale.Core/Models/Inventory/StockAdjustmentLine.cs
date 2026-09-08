namespace PointOfSale.Core.Models.Inventory
{
    public class StockAdjustmentLine
    {
        public long StockAdjustmentLineId { get; set; }
        public long StockAdjustmentId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string ProductCode { get; set; }
        public decimal CurrentStock { get; set; }
        public decimal AdjustmentQuantity { get; set; }
        public decimal Quantity { get; set; }
        public string Action { get; set; }
        public string Reason { get; set; }
    }
}
