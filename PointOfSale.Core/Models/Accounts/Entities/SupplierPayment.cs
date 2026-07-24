using System;
using System.Collections.Generic;

namespace PointOfSale.Core.Models.Accounts
{
    public class SupplierPayment
    {
        public int BranchId { get; set; }
        public int SupplierId { get; set; }
        public DateTime PaymentDate { get; set; }
        public string PaymentMethod { get; set; }
        public string BankName { get; set; }
        public string AccountNumber { get; set; }
        public string AccountName { get; set; }
        public string ReferenceNumber { get; set; }
        public decimal PaidAmount { get; set; }
        public int CreatedBy { get; set; }

        public List<SupplierPaymentLine> PaymentLines { get; set; } = new List<SupplierPaymentLine>();
    }
}
