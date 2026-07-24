namespace PointOfSale.Core.Models.Sales
{
    public class PaymentTerminal
    {
        public int Id { get; set; }
        public int BranchId { get; set; }
        public int? LinkedAccountId { get; set; }
        public int? ProviderBankId { get; set; }
        public string TerminalName { get; set; }
        public string TerminalId { get; set; }
        public bool IsActive { get; set; }
        public int CreatedBy { get; set; }
        public int? UpdatedBy { get; set; }

        // Denormalised display fields — populated by the repository mapper
        public string BranchName { get; set; }
        public string LinkedAccountDisplay { get; set; }
        public string ProviderBankName { get; set; }
    }
}
