using System;

namespace PointOfSale.Core.DTOs
{
    public class SalesListDto
    {
        public long SalesId { get; set; }
        public string InvoiceNumber { get; set; }
        public DateTime SalesDate { get; set; }
        public string CustomerName { get; set; }
        public string SalesPerson { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal Discount { get; set; }
        public decimal NetAmount { get; set; }
        public decimal Cash { get; set; }
        public decimal CreditAmount { get; set; }
        public string PaymentStatus { get; set; }
    }
}
