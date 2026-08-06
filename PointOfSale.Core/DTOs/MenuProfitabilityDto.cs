namespace PointOfSale.Core.DTOs
{
    public class MenuProfitabilityDto
    {
        public string CategoryName { get; set; }
        public string MenuItemName { get; set; }
        public string VariantName { get; set; }
        public decimal TotalBOMCost { get; set; }
        public decimal SellingPrice { get; set; }
        public decimal GrossProfit { get; set; }
        public decimal FoodCostPercentage { get; set; }
        public string BOMStatus { get; set; }

        public bool IsHighFoodCost => FoodCostPercentage > 35;
    }
}
