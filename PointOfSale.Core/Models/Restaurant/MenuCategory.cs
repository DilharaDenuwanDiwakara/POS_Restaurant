namespace PointOfSale.Core.Models.Restaurant
{
    public class MenuCategory
    {
        public int Id { get; set; }
        public int? ParentId { get; set; }
        public string ParentName { get; set; }
        public string Name { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
        public string IncomeAccountCode { get; set; }
    }
}
