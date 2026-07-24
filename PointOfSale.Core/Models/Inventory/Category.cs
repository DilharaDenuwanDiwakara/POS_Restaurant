namespace PointOfSale.Core.Models.Inventory
{
    public class Category
    {
        public int CategoryId { get; set; }
        public int? ParentCategoryId { get; set; }
        public string ParentCategoryName { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string StockAccountCode { get; set; }
        public string CostOfSalesAccountCode { get; set; }
    }
}
