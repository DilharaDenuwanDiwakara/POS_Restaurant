using System;

namespace PointOfSale.Core.DTOs
{
    public class BatchProductionHistoryDto
    {
        public long StockTransactionId { get; set; }
        public int ProductId { get; set; }
        public string ItemName { get; set; }
        public decimal YieldQuantity { get; set; }
        public string UnitMeasureName { get; set; }
        public DateTime ProcessedAt { get; set; }
    }
}
