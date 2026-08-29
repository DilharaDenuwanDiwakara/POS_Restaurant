using System;

namespace PointOfSale.Core.Models.Accounts.Entities
{
    public class FundTransfer
    {
        public long FundTransferId { get; set; }
        public string TransferNumber { get; set; }
        public int BranchId { get; set; }
        public DateTime TransferDate { get; set; }
        public int SourceAccountId { get; set; }
        public string SourceAccountName { get; set; }
        public int DestinationAccountId { get; set; }
        public string DestinationAccountName { get; set; }
        public decimal Amount { get; set; }
        public string ReferenceNo { get; set; }
        public string Description { get; set; }
        public int CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
