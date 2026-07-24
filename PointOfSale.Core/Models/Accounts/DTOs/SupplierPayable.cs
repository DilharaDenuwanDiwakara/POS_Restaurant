using System;

namespace PointOfSale.Core.Models.Accounts
{
    public class SupplierPayable
    {
        public long GoodsReceiveNoteId { get; set; }
        public int SupplierId { get; set; }
        public string InvoiceNumber { get; set; }
        public DateTime InvoiceDate { get; set; }
        public DateTime DueDate { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal AmountDue { get; set; }
        public string Status { get; set; }
    }
}
