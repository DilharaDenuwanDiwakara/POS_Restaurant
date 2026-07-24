namespace PointOfSale.Core.DTOs
{
    public class StockLedgerReportModel
    {
        // Company header — returned inline by the stored procedure
        public string CompanyName { get; set; }
        public string CompanyAddress { get; set; }
        public string CompanyContactNumber { get; set; }
        public string CompanyEmail { get; set; }
        public byte[] CompanyLogo { get; set; }

        // Report scope — embedded in every row by the SP
        public string LocationName { get; set; }
        public string ProductCode { get; set; }
        public string ProductName { get; set; }

        // Ledger transaction line
        public System.DateTime MovementDate { get; set; }
        public string TransactionType { get; set; }
        public string ReferenceNo { get; set; }
        public decimal QuantityIn { get; set; }
        public decimal QuantityOut { get; set; }
        public decimal RunningBalance { get; set; }
        public decimal UnitCost { get; set; }
        public string Notes { get; set; }
    }
}
