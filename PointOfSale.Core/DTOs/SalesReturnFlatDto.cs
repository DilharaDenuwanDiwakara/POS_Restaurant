using System;

namespace PointOfSale.Core.DTOs
{
    public class SalesReturnFlatDto
    {
        public string ReturnNo { get; set; }
        public string InvoiceNo { get; set; }
        public decimal TotalRefundAmount { get; set; }
        public DateTime CreateDate { get; set; }

        public string ProductName { get; set; }
        public decimal QtyReturn { get; set; }
        public decimal RefundAmount { get; set; }
        public string ReturnReason { get; set; }
        public bool IsWastage { get; set; }
    }
}
