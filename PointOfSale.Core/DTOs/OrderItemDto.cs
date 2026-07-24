namespace PointOfSale.Core.DTOs
{
    public class OrderItemDto
    {
        public long OrderItemId { get; set; }
        public int VariantId { get; set; }
        public string ProductName { get; set; }
        public string VariantName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal DiscountAmount { get; set; }
        public string Note { get; set; }
        public string OfferName { get; set; }
        public bool IsFreeItem { get; set; }
    }
}
