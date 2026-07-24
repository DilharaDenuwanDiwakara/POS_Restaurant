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

        public string DisplayName => string.IsNullOrEmpty(VariantName)
                                    ? MenuItemName
                                    : $"{MenuItemName} - {VariantName}";

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
