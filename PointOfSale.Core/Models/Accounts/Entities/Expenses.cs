using System;

namespace PointOfSale.Core.Models.Accounts.Entities
{
    public class Expenses
    {
        public int ExpensesId { get; set; }
        public string VoucherNumber { get; set; }
        public DateTime ExpensesDate { get; set; }
        public int ExpensesCategoryId { get; set; }
        public string ExpensesCategoryName { get; set; }
        public int PaymentAccountId { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; }
        public int CreatedBy { get; set; }
        public int LocationId { get; set; }
    }
}
