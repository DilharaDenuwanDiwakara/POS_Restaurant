using System;

namespace PointOfSale.Core.Models.Inventory
{
    public class StockTransferLine
    {
        // Database Fields (Required for Save)
        public long TransferLineId { get; set; }
        public long TransferId { get; set; }
        public int ProductId { get; set; }
        public long BatchId { get; set; }
        public decimal Quantity { get; set; }

        // UI Display Properties (Not saved to TransferLine table, but useful for Grid/Reports)
        // You can populate these from the SelectedProduct/SelectedBatch in the ViewModel
        public string ProductName { get; set; }
        public string ProductCode { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public decimal UnitCost { get; set; }
        public decimal LineTotal => Quantity * UnitCost;
    }
}
