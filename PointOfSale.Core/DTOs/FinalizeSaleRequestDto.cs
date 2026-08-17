using System.Collections.Generic;

namespace PointOfSale.Core.DTOs
{
    public class FinalizeSaleRequestDto
    {
        public long SalesId { get; set; }
        public int? CustomerId { get; set; }
        public decimal CashGiven { get; set; }
        public int CreatedBy { get; set; }
        public List<SalePaymentRequestDto> Payments { get; set; } = new List<SalePaymentRequestDto>();
    }

    public class SalePaymentRequestDto
    {
        public int? PaymentTerminalId { get; set; }
        public string PaymentMethod { get; set; }
        public decimal Amount { get; set; }
        public string ReferenceNumber { get; set; }
    }
}
