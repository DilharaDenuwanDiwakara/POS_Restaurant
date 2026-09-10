using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PointOfSale.Core.DTOs
{
    // 1. Updated StockTransferFlatDto
    public class StockTransferFlatDto
    {
        // StockTransfer fields
        public long Id { get; set; }
        public int BranchId { get; set; }
        public string TransferNumber { get; set; }
        public int FromLocationId { get; set; }
        public string FromLocationName { get; set; }
        public int ToLocationId { get; set; }
        public string ToLocationName { get; set; }
        public DateTime TransferDate { get; set; }
        public string Note { get; set; }
        public string Status { get; set; }
        public int CreatedBy { get; set; }
        public string Username { get; set; }
        public DateTime CreatedDate { get; set; }

        // StockTransferLine fields
        public long LineId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string ProductCode { get; set; }
        public long BatchId { get; set; }
        public decimal Qty { get; set; }
        public int UnitMeasureId { get; set; }
        public string UnitMeasureName { get; set; }
        public string UnitMeasureCode { get; set; }
    }
}
