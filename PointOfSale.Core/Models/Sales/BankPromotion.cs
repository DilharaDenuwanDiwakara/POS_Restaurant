using System;

namespace PointOfSale.Core.Models.Sales
{
    public class BankPromotion
    {
        public int Id { get; set; }
        public int BankId { get; set; }
        public string PromoName { get; set; }
        public decimal DiscountPercentage { get; set; }
        public DateTime ValidFrom { get; set; }
        public DateTime ValidTo { get; set; }
        private string _requiredCardType = "ALL";
        public string RequiredCardType { get => _requiredCardType; set => _requiredCardType = value?.Trim().ToUpperInvariant(); }
        public bool IsActive { get; set; } = true;
    }
}
