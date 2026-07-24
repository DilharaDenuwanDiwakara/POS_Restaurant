using PointOfSale.Core.Models.Common;

namespace PointOfSale.Core.Models.Sales
{
    public class Customer : AuditableEntity<int>
    {
        public string CustomerName { get; set; }
        public string ContactNumber { get; set; }
        public bool IsTaxRegistered { get; set; }
        public string TaxRegistrationNumber { get; set; }
        public decimal CreditLimit { get; set; }
        public decimal Balance { get; set; }
        public int LoyaltyPoints { get; set; }
        public bool IsActive { get; set; }
        public bool IsDefault { get; set; }
    }
}
