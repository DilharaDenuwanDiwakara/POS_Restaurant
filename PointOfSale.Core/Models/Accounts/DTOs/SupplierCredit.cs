using System;

namespace PointOfSale.Core.Models.Accounts.DTOs
{
    public class SupplierCredit
    {
        public string SourceType { get; set; }
        public long SourceId { get; set; }
        public string ReferenceNumber { get; set; }
        public DateTime CreditDate { get; set; }
        public decimal CreditAmount { get; set; }
        public decimal AppliedAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        public decimal SelectedAmount { get; set; }
    }
}
