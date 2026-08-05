using System;
using System.Collections.Generic;

namespace PointOfSale.Core.Models.Purchasing
{
    public class GoodPurchaseNote
    {
        public long GoodsPurchaseNoteId { get; set; }
        public int BranchId { get; set; }
        public int SupplierId { get; set; }
        public string PONumber { get; set; }
        public decimal SubTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string Note { get; set; }
        public string Notes
        {
            get => Note;
            set => Note = value;
        }
        public string OrderBy { get; set; }
        public string RequestedBy
        {
            get => OrderBy;
            set => OrderBy = value;
        }
        public DateTime OrderDate { get; set; } = DateTime.Now;
        public DateTime? ExpectedDeliveryDate { get; set; }
        public string Status { get; set; }
        public int CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public int? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public int? CancelledBy { get; set; }
        public DateTime? CancelledAt { get; set; }

        public string SupplierName { get; set; }
        public string Username { get; set; }
        public bool IsDraft =>
            string.Equals(Status?.Trim(), "DRAFT", StringComparison.OrdinalIgnoreCase);
        public bool IsRejected =>
            string.Equals(Status?.Trim(), "REJECTED", StringComparison.OrdinalIgnoreCase);
        public List<GoodsPurchaseNoteLine> Lines { get; set; } = new List<GoodsPurchaseNoteLine>();
    }
}
