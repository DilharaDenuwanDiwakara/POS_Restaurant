namespace PointOfSale.Core.DTOs
{
    public class MenuItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }
        public decimal Price { get; set; } // Base Price
        public string CategoryName { get; set; }
        public int StationId { get; set; }
        public string TargetStation { get; set; }
        public string AppliedTaxes { get; set; }
        public int CategoryId { get; set; }
        public bool IsActive { get; set; }
        public bool IsAvailable { get; set; }
    }
}
