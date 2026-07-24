namespace PointOfSale.Core.Models.Purchasing
{
    public class OriginalGrnReturnLine
    {
        public long GoodsReceiveNoteLineId { get; set; }
        public decimal OriginalQuantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineDiscount { get; set; }
        public decimal TaxAmount { get; set; }
    }
}
