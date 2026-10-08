namespace PointOfSale.Core.DTOs
{
    public class OpeningStockItemDto
    {
        public int ProductId { get; set; }
        public string ProductCode { get; set; }
        public string ProductName { get; set; }
        public decimal UnitCost { get; set; }
        public decimal OpeningQuantity { get; set; }
        public string Uom { get; set; }
    }
}
