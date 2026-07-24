using System;

namespace PointOfSale.Core.DTOs
{
    public class ServedOrderDto
    {
        public long OpenAccountId { get; set; }
        public long OrderId { get; set; }
        public string TableName { get; set; }
        public string OrderNumber { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime OrderDate { get; set; }
    }
}
