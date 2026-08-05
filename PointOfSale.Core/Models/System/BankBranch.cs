namespace PointOfSale.Core.Models.System
{
    public class BankBranch
    {
        public int Id { get; set; }
        public int BankId { get; set; }
        public string BranchCode { get; set; }
        public string BranchName { get; set; }
        public bool IsActive { get; set; }
    }
}
