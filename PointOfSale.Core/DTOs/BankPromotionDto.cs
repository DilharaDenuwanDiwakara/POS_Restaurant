using System.Collections.Generic;
using PointOfSale.Core.Models.Restaurant;
using PointOfSale.Core.Models.Sales;

namespace PointOfSale.Core.DTOs
{
    public class BankPromotionDto : BankPromotion
    {
        public string BankName { get; set; }
        public string Status => IsActive ? "Active" : "Inactive";
        public List<Variant> ExcludedVariants { get; set; } = new List<Variant>();
    }
}
