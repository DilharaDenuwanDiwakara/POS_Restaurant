using System;
using System.Collections.Generic;

namespace PointOfSale.Core.Models.Accounts.Entities
{
    public class ExpenseHeader
    {
        public int ExpensesId { get; set; }
        public string VoucherNumber { get; set; }
        public DateTime ExpensesDate { get; set; }
        public int BranchId { get; set; }
        public int PaymentAccountId { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; }
        public int CreatedBy { get; set; }
        public IList<ExpenseLine> Lines { get; set; } = new List<ExpenseLine>();
    }
}
