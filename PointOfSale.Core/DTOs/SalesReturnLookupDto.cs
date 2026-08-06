using System;
using System.Collections.Generic;
using PointOfSale.Core.Models.Sales;

namespace PointOfSale.Core.DTOs
{
    public class SalesReturnLookupDto
    {
        public long SalesId { get; set; }
        public string InvoiceNumber { get; set; }
        public int BranchId { get; set; }
        public int? ShiftId { get; set; }
        public DateTime SalesDate { get; set; }

        public List<SalesReturnItemModel> Lines { get; set; } = new List<SalesReturnItemModel>();
    }
}
