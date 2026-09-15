using System.Collections.Generic;

namespace PointOfSale.Core.DTOs
{
    public class RecalledSaleDto
    {
        public long SalesId { get; set; }
        public string InvoiceNumber { get; set; }
        public int? CustomerId { get; set; }
        public int? SalesPersonId { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal Discount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal ServiceChargeAmount { get; set; }
        public long? OrderId { get; set; }
        public int? ShiftId { get; set; }
        public List<RecalledSaleLineDto> Lines { get; set; } = new List<RecalledSaleLineDto>();
    }

    public class RecalledSaleLineDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
    }
}
