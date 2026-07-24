namespace PointOfSale.Core.Models.Accounts
{
    public class CustomerPaymentLine
    {
        public long CustomerReceivableId { get; set; }
        public decimal AmountApplied { get; set; }
    }
}
