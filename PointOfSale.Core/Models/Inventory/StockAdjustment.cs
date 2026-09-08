using System;
using System.Collections.Generic;

namespace PointOfSale.Core.Models.Inventory
{
    public class StockAdjustment
    {
        // Header Data
        public long StockAdjustmentId { get; set; }
        public string AdjustmentNumber { get; set; }
        public DateTime AdjustDate { get; set; }
        public int LocationId { get; set; }
        public string LocationName { get; set; }
        public string Note { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; }
        public int BranchId { get; set; }

        // Detail/Grid Data (Used for GetAll output)
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public decimal Quantity { get; set; }
        public string Reason { get; set; }
        public string ActionType { get; set; }

        // Used for Saving Multiple Items
        public List<StockAdjustmentLine> Lines { get; set; } = new List<StockAdjustmentLine>();
    }

    public class StockAdjustmentLine
    {
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public string Reason { get; set; }
    }
}
