using System.Collections.Generic;

namespace PointOfSale.Core.DTOs
{
    public class HoldSaleRequestDto
    {
        public int BranchId { get; set; }
        public int? CustomerId { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal Discount { get; set; }
        public bool IsTaxInvoice { get; set; }
        public string TaxInvoiceNumber { get; set; }
        public int CreatedBy { get; set; }
        public long? OrderId { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal ServiceChargeAmount { get; set; }
        public int? ShiftId { get; set; }
        public List<SaleLineRequestDto> Lines { get; set; } = new List<SaleLineRequestDto>();
    }

    public class SaleLineRequestDto
    {
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
    }
}
