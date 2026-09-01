namespace PointOfSale.Core.Models.Accounts.Entities
{
    public class Expenses : ExpenseHeader
    {
        public int ExpensesCategoryId { get; set; }
        public string ExpensesCategoryName { get; set; }
    }
}
