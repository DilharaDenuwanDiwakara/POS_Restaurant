using System;
using System.Text;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Sales;

namespace PointOfSale.Core.Services
{
    public class BankPromotionCheckoutService
    {
        private readonly ISalesRepository _sales;

        public BankPromotionCheckoutService(ISalesRepository sales)
        {
            _sales = sales ?? throw new ArgumentNullException(nameof(sales));
        }

        // Callers calculating a bill must retain the promotion's variant exclusions.
        // Only a BIN prefix of at most eight digits reaches the repository; the card number is never persisted.
        public Task<BankPromoWithExclusionsDto> GetApplicablePromotionAsync(string cardNumberOrPrefix)
        {
            var prefix = GetPrefix(cardNumberOrPrefix);
            return prefix == null
                ? Task.FromResult<BankPromoWithExclusionsDto>(null)
                : _sales.GetBankPromoWithExclusionsAsync(prefix);
        }

        public async Task<decimal> GetDiscountPercentageAsync(string cardNumberOrPrefix)
        {
            var promotion = await GetApplicablePromotionAsync(cardNumberOrPrefix);
            return promotion?.DiscountPercentage ?? 0m;
        }

        // Legacy full-card callers use the first six digits; explicit 6-8 digit BINs stay intact.
        private static string GetPrefix(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var prefix = new StringBuilder(8);
            var digits = 0;
            foreach (var character in value)
            {
                if (character == ' ' || character == '-') continue;
                if (character < '0' || character > '9' || ++digits > 19) return null;
                if (prefix.Length < 8) prefix.Append(character);
            }
            return digits < 6 ? null : digits <= 8 ? prefix.ToString() : prefix.ToString(0, 6);
        }
    }
}
