namespace PointOfSale.Core.Models.Sales
{
    public class PaymentDetail
    {
        public int? PaymentTerminalId { get; set; }
        public string PaymentMethod { get; set; }
        public decimal Amount { get; set; }
        public string ReferenceNumber { get; set; }
    }
}
