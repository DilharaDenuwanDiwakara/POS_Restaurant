using System;

namespace PointOfSale.Core.DTOs
{
    public class SalesReturnReportDto
    {
        public string ReportTitle { get; set; }
        public string ReportFromDate { get; set; }
        public string ReportToDate { get; set; }
        public string CompanyName { get; set; }
        public string CompanyAddress { get; set; }
        public string CompanyContact { get; set; }
        public DateTime ReturnDate { get; set; }
        public string ReturnNumber { get; set; }
        public string InvoiceNumber { get; set; }
        public string ProductName { get; set; }
        public decimal ReturnQuantity { get; set; }
        public string Reason { get; set; }
        public bool IsWastage { get; set; }
        public decimal LineRefundAmount { get; set; }
        public decimal HeaderTotalRefund { get; set; }
    }
}
