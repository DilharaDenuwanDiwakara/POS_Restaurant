using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PointOfSale.Core.DTOs
{
    public class MenuVariantDto : INotifyPropertyChanged
    {
        public int VariantId { get; set; }
        public int MenuCategoryId { get; set; }
        public string ItemCode { get; set; }
        public string Barcode { get; set; }
        public string MenuItemName { get; set; }
        public string VariantName { get; set; }
        public string ImageUrl { get; set; }
        public decimal DefaultPrice { get; set; }
        public decimal? DiscountAmount { get; set; }
        public List<int> TaxIds { get; set; } = new List<int>();

        private string _offerSummary;
        public string OfferSummary
        {
            get => _offerSummary;
            set
            {
                if (_offerSummary == value)
                    return;

                _offerSummary = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasOffer));
            }
        }

        public bool HasOffer => !string.IsNullOrWhiteSpace(OfferSummary);

        public string DisplayName => FormatDisplayName(MenuItemName, VariantName);

        public static string FormatDisplayName(string itemName, string variantName)
        {
            var normalizedItemName = RemoveStandardVariantSuffix(itemName);

            if (string.IsNullOrWhiteSpace(variantName) ||
                string.Equals(variantName.Trim(), "STANDARD", StringComparison.OrdinalIgnoreCase))
            {
                return normalizedItemName;
            }

            return $"{normalizedItemName} - {variantName.Trim()}";
        }

        public static string RemoveStandardVariantSuffix(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName))
                return itemName;

            const string standardSuffix = " - STANDARD";
            var trimmed = itemName.Trim();

            return trimmed.EndsWith(standardSuffix, StringComparison.OrdinalIgnoreCase)
                ? trimmed.Substring(0, trimmed.Length - standardSuffix.Length).TrimEnd()
                : trimmed;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
