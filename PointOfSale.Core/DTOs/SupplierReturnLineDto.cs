namespace PointOfSale.Core.DTOs
{
    public class SupplierReturnLineDto
    {
        public string ItemName { get; set; }
        public decimal ReturnedQty { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Discount { get; set; }
        public decimal Tax { get; set; }
        public string Reason { get; set; }
        public decimal LineTotal { get; set; }
    }
}
