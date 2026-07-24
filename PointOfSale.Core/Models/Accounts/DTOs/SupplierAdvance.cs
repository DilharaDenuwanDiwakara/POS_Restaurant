using System;

namespace PointOfSale.Core.Models.Accounts
{
    public class SupplierAdvance
    {
        public int BranchId { get; set; }
        public int AdvanceId { get; set; }
        public int SupplierId { get; set; }
        public DateTime PaymentDate { get; set; }
        public string PaymentMethod { get; set; }
        public string ReferenceNumber { get; set; }
        public decimal Amount { get; set; }
        public int CreatedBy { get; set; }

    }
}
