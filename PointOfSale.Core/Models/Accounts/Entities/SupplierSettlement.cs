using System;

namespace PointOfSale.Core.Models.Accounts.Entities
{
    public class SupplierSettlement
    {
        public int SupplierSettlementId { get; set; }
        public int SupplierId { get; set; }
        public long PayableId { get; set; }
        public long CreditId { get; set; }
        public int PaymentId { get; set; }
        public decimal SettlementAmount { get; set; }
        public DateTime SettlementDate { get; set; }
        public int CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
