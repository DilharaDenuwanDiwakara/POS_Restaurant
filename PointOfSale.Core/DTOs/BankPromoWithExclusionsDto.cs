using System.Collections.Generic;

namespace PointOfSale.Core.DTOs
{
    public class BankPromoWithExclusionsDto
    {
        public int BankPromotionId { get; set; }
        public int BankId { get; set; }
        public string PromoName { get; set; }
        public string CardType { get; set; }
        public string CardBrand { get; set; }
        public string RequiredCardType { get; set; }
        public decimal DiscountPercentage { get; set; }
        public List<int> ExcludedVariantIds { get; } = new List<int>();
    }
}
