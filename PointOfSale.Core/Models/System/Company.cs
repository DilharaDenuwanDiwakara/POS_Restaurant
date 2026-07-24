using PointOfSale.Core.Models.Common;

namespace PointOfSale.Core.Models.System
{
    public class Company : AuditableEntity<int>
    {
        public string TradingName { get; set; }
        public string LegalName { get; set; }
        public string BusinessRegistrationNumber { get; set; }
        public string TaxRegistrationNumber { get; set; }
        public string AddressLine1 { get; set; }
        public string AddressLine2 { get; set; }
        public string ContactNumber { get; set; }
        public string Email { get; set; }
        public string Website { get; set; }
        public string BaseCurrency { get; set; }
        public string ReceiptFooterText { get; set; }
        public string DocumentTerms { get; set; }
        public byte[] CompanyLogo { get; set; }
    }
}
