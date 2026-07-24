namespace PointOfSale.Core.Models.Restaurant
{
    public class MealPeriod
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public global::System.TimeSpan DefaultStartTime { get; set; }
        public global::System.TimeSpan DefaultEndTime { get; set; }
        public bool IsActive { get; set; }
    }
}
