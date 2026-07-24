using System;

namespace PointOfSale.Core.Models.Accounts
{
    public class CustomerReceivable
    {
        public long CustomerReceivableId { get; set; }
        public int CustomerId { get; set; }
        public string InvoiceNumber { get; set; }
        public long ReferenceId { get; set; }
        public DateTime TransactionDate { get; set; }
        public DateTime DueDate { get; set; }
        public decimal InitialAmount { get; set; }
        public decimal AmountSettled { get; set; }
        public decimal BalanceAmount { get; set; }
        public string Status { get; set; }

    }
}
