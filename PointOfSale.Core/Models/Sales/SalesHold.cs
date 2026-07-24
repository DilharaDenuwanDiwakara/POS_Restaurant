using System;
using System.Collections.Generic;

namespace PointOfSale.Core.Models.Sales
{
    public class SalesHold
    {

        public long HoldId { get; set; }
        public int LocationId { get; set; }
        public DateTime HoldDate { get; set; }
        public string ReferenceNote { get; set; }
        public decimal TotalAmount { get; set; }
        public int CreatedBy { get; set; }
        public List<SalesHoldLine> Lines { get; set; } = new List<SalesHoldLine>();
    }
}
