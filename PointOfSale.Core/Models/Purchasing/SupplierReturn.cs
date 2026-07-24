using System;
using System.Collections.Generic;

namespace PointOfSale.Core.Models.Purchasing
{
    public class SupplierReturn
    {
        public int BranchId { get; set; }
        public int LocationId { get; set; }
        public int SupplierReturnId { get; set; }
        public string ReturnNumber { get; set; }
        public string SupplierName { get; set; }
        public int SupplierId { get; set; }
        public string ReturnedBy { get; set; }
        public string Note { get; set; } = null;
        public DateTime ReturnDate { get; set; }
        public decimal SubTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal NetAmount { get; set; }
        public string Status { get; set; }
        public string ApproverRemark { get; set; }
        public int? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public int? RejectedBy { get; set; }
        public DateTime? RejectedAt { get; set; }
        public int CreatedBy { get; set; }

        // Navigation property for the lines
        public List<SupplierReturnLine> Lines { get; set; } = new List<SupplierReturnLine>();
    }
}
