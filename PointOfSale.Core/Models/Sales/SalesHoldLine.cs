namespace PointOfSale.Core.Models.Sales
{
    public class SalesHoldLine
    {
        public int ProductId { get; set; }
        public long BatchId { get; set; }
        public string ProductCode { get; set; }
        public string ProductName { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Discount { get; set; }
    }
}
