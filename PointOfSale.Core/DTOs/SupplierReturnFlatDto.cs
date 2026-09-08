using System;
namespace PointOfSale.Core.DTOs
{
    public class SupplierReturnFlatDto
    {
        public int SupplierReturnId { get; set; }
        public string ReturnNumber { get; set; }
        public DateTime ReturnDate { get; set; }
        public string SupplierName { get; set; }
        public string ReturnedBy { get; set; }
        public decimal NetAmount { get; set; }

        public string ProductName { get; set; }
        public decimal QtyReturn { get; set; }
        public decimal RefundAmount { get; set; }
        public string ReturnReason { get; set; }
        public bool IsWastage { get; set; }

        public string GRNNo { get; set; }
        public string InvoiceNo { get; set; }
        public DateTime? InvoiceDate { get; set; }
    }
}
