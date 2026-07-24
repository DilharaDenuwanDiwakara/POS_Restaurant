namespace PointOfSale.Core.DTOs
{
    public class AvailableQuantityDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string Barcode { get; set; }
        public decimal Quantity { get; set; }
        public int LocationId { get; set; }
    }
}
