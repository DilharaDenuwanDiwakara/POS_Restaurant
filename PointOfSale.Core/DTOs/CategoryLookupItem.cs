namespace PointOfSale.Core.DTOs
{
    public class CategoryLookupItem
    {
        public int Id { get; set; }
        public string DisplayName { get; set; }
        public int Level { get; set; }
        public bool IsRoot => Level == 0;
    }
}
