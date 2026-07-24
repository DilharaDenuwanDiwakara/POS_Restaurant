namespace PointOfSale.Core.Models.Restaurant
{
    public class Table
    {
        public int Id { get; set; }
        public int BranchId { get; set; }
        public string Name { get; set; }
        public int Capacity { get; set; }
        public string CurrentStatus { get; set; }
    }
}
