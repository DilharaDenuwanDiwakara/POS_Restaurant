using System;

namespace PointOfSale.Core.Models.Accounts
{
    public class CustomerAdvance
    {
        public int AdvanceId { get; set; }
        public int CustomerId { get; set; }
        public DateTime PaymentDate { get; set; }
        public string PaymentMethod { get; set; }
        public string ReferenceNumber { get; set; }
        public decimal Amount { get; set; }
        public int CreatedBy { get; set; }
    }
}
