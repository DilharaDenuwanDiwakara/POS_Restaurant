using System;

namespace PointOfSale.Core.DTOs
{
    public sealed class AccountInquiryLineDto
    {
        public DateTime TransactionDate { get; set; }
        public string DocumentNo { get; set; } = string.Empty;
        public string SourceType { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal RunningBalance { get; set; }
    }
}
