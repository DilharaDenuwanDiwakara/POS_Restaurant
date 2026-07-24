namespace PointOfSale.Core.Models.Restaurant
{
    public class Room
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int MaxCapacity { get; set; }
        public string Type { get; set; }
        public bool IsActive { get; set; }
        public string Status { get; set; }

        public int FloorNumber { get; set; }
        public string ImageUrl { get; set; }
    }
}
