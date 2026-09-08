namespace PointOfSale.Core.DTOs
{
    public class StockAdjustmentLineDto
    {
        public long Id { get; set; }
        public long StockAdjustmentId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal Total { get; set; }
        public string Reason { get; set; }
    }
}
