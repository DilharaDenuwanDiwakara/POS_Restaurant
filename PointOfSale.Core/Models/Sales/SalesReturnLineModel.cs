namespace PointOfSale.Core.Models.Sales
{
    public class SalesReturnLineModel
    {
        public string ProductName { get; set; }
        public decimal QtyReturn { get; set; }
        public decimal RefundAmount { get; set; }
        public string ReturnReason { get; set; }
        public bool IsWastage { get; set; }
    }
}
