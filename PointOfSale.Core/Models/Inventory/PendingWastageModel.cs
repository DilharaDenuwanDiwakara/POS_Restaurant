using System;

namespace PointOfSale.Core.Models.Inventory
{
    public class PendingWastageModel
    {
        public long WastageId { get; set; }
        public string WastageNumber { get; set; }
        public string LocationName { get; set; }
        public DateTime WastageDate { get; set; }
        public int TotalItems { get; set; }
        public string RequestedBy { get; set; }
        public string Note { get; set; }
        public string Status { get; set; }
        public decimal TotalCost { get; set; }
    }
}
