namespace PointOfSale.Core.Models.Inventory
{
    public class WastageLine
    {
        public long Id { get; set; }
        public long WastageId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } // For UI Display
        public long BatchId { get; set; }
        public int? WastageReasonId { get; set; }
        public string ReasonName { get; set; } // For UI Display
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalCost => Quantity * UnitCost;
    }
}
