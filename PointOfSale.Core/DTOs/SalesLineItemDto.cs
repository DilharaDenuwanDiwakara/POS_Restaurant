namespace PointOfSale.Core.DTOs
{
    public class SalesLineItemDto
    {
        public string ItemName { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }

        public decimal Discount { get; set; }
        public decimal TaxAmount { get; set; }

    }
}
