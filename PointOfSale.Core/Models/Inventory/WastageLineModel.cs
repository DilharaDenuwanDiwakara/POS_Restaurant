namespace PointOfSale.Core.Models.Inventory
{
    public class WastageLineModel
    {
        public long Id { get; set; }
        public string ProductName { get; set; }
        public string Reason { get; set; }
        public string UOM { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalCost { get; set; }
    }
}
