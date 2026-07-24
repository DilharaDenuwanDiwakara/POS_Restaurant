namespace PointOfSale.Core.Models.Inventory
{
    public class WastageReason
    {
        public int Id { get; set; }
        public string Reason { get; set; }
        public bool IsActive { get; set; }
    }
}
