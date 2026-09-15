using System;
using System.Collections.Generic;

namespace PointOfSale.Core.Models.Sales
{
    public class Sale
    {
        public long SalesId { get; set; }
        public string InvoiceNumber { get; set; }
        public int? CustomerId { get; set; }
        public DateTime SalesDate { get; set; }

        public decimal TotalAmount { get; set; }
        public decimal Discount { get; set; }
        public decimal TaxRate { get; set; }
        public decimal CashGiven { get; set; }
        public bool IsTaxInvoice { get; set; }
        public string TaxInvoiceNumber { get; set; }
        public int CreatedBy { get; set; }
        public int BranchId { get; set; }

        public long? OrderId { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal ServiceChargeAmount { get; set; }
        public int? ShiftId { get; set; }
        public int? SalesPersonId { get; set; }

        public List<SalesLine> Lines { get; set; } = new List<SalesLine>();
        public List<PaymentDetail> Payments { get; set; } = new List<PaymentDetail>();
    }
}
