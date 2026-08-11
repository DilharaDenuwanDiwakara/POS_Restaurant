namespace PointOfSale.Core.Models.Inventory
{
    public class WastageDetailModel
    {
        public string ItemName { get; set; }
        public string Reason { get; set; }
        public string UOM { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalCost { get; set; }
    }
}
