using System;
using System.Collections.Generic;

namespace PointOfSale.Core.Models.Accounts
{
    public class CustomerPayment
    {
        public int CustomerId { get; set; }
        public DateTime PaymentDate { get; set; }
        public string PaymentMethod { get; set; }
        public string ReferenceNumber { get; set; } = null;
        public decimal PaidAmount { get; set; }
        public int CreatedBy { get; set; }
        public string BankName { get; set; }
        public string AccountNumber { get; set; }
        public string AccountName { get; set; }

        public List<CustomerPaymentLine> PaymentLines { get; set; } = new List<CustomerPaymentLine>();
    }
}
