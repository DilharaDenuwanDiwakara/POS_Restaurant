using System;

namespace PointOfSale.Core.Models.Sales
{
    public class CashInOut
    {
        public long TransactionId { get; set; }
        public string TransactionType { get; set; }
        public decimal Amount { get; set; }
        public string Reason { get; set; }

        public DateTime TransactionDate { get; set; }
        public int CreatedBy { get; set; }
        public int LocationId { get; set; }
        public long ShiftId { get; set; }

    }
}
