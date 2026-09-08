using System;

namespace PointOfSale.Core.Models.Purchasing
{
    public class GoodsReceiveNoteLineModel
    {
        public long GoodsPurchaseNoteLineId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public decimal QuantityOrdered { get; set; }
        public decimal QuantityReceived { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineDiscount { get; set; }
        public decimal TaxAmount { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public decimal TotalAmount => Math.Round((QuantityReceived * UnitPrice) - LineDiscount + TaxAmount, 2);
    }
}
