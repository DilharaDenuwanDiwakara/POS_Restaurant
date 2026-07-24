namespace PointOfSale.Core.DTOs
{
    public class OpenStockItemDto
    {
        public string ProductCode { get; set; }
        public decimal Quantity { get; set; }
        public decimal? UnitCost { get; set; }
        public decimal SellingPrice { get; set; }
    }
}
