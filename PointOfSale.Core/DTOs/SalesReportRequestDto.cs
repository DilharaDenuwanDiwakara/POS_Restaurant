using System;

namespace PointOfSale.Core.DTOs
{
    public class SalesReportRequestDto
    {
        public int UserId { get; set; }
        public int? BranchId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }
}
