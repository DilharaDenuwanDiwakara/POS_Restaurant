namespace PointOfSale.Core.Models.Accounts.Entities
{
    public class ExpenseLine
    {
        public int ExpenseLineId { get; set; }
        public int ExpenseHeaderId { get; set; }
        public int AccountId { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; }
    }
}
