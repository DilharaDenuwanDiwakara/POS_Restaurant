using System;

namespace PointOfSale.Core.Models.Inventory
{
    public class StockAdjustment
    {
        public int BranchId { get; set; }
        public long StockAdjustmentId { get; set; }
        public DateTime AdjustDate { get; set; }
        public int UserId { get; set; }
        public int LocationId { get; set; }
        public decimal Quantity { get; set; }
        public string Reason { get; set; }
        public string UserName { get; set; }
        public string LocationName { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; }
    }
}
