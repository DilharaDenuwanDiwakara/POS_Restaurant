using System;

namespace PointOfSale.Core.Models.Accounts
{
    public class CustomerAdvanceList
    {
        public long AdvanceId { get; set; }
        public string CustomerName { get; set; }
        public string TransactionNumber { get; set; }

        public DateTime PaymentDate { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal UnappliedAmount { get; set; }
    }
}
