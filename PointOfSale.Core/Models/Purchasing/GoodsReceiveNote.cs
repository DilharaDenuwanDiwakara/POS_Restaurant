using System;
using System.Collections.Generic;

namespace PointOfSale.Core.Models.Purchasing
{
    public class GoodsReceiveNote
    {
        public long GoodsReceiveNoteId { get; set; }
        public long PurchaseOrderId { get; set; }
        public int BranchId { get; set; }
        public int SupplierId { get; set; }
        public string InvoiceNumber { get; set; }
        public string Notes { get; set; } = null;
        public decimal SubTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string ReceivedBy { get; set; }
        public DateTime GoodsReceiveNoteDate { get; set; } = DateTime.Now;
        public int CreditDays { get; set; }
        public DateTime DueDate { get; set; } = DateTime.Today;
        public string Status { get; set; }
        public int CreatedBy { get; set; }
        public string Username { get; set; }
        public DateTime CreatedDate { get; set; }
        public string GoodsReceiveNoteNumber { get; set; }
        public string SupplierName { get; set; }
        public string PONumber { get; set; }
        public string CreatedByName { get; set; }
        public bool IsRejected =>
            !string.IsNullOrWhiteSpace(Status) &&
            Status.Trim().IndexOf("REJECT", StringComparison.OrdinalIgnoreCase) >= 0;

        public bool IsDraft =>
            string.Equals(Status?.Trim(), "DRAFT", StringComparison.OrdinalIgnoreCase);

        public string PONumberDisplay =>
            string.IsNullOrWhiteSpace(PONumber) ? "N/A (Direct)" : PONumber;

        public string ReceivedByAuditDisplay =>
            !string.IsNullOrWhiteSpace(CreatedByName)
                ? CreatedByName
                : !string.IsNullOrWhiteSpace(ReceivedBy)
                    ? ReceivedBy
                    : !string.IsNullOrWhiteSpace(Username)
                        ? Username
                        : "N/A";

        public List<GoodsReceiveNoteLine> Lines { get; set; } = new List<GoodsReceiveNoteLine>();
    }
}
