namespace PointOfSale.Core.Models.Restaurant
{
    public class Station
    {
        public int Id { get; set; }
        public int BranchId { get; set; }
        public string Name { get; set; } = string.Empty;

        public string PrinterIP { get; set; }
        public string PrinterName { get; set; }
    }
}
