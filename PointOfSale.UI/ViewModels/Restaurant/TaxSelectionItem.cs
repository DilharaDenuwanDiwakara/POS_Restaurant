namespace PointOfSale.UI.ViewModels.Restaurant
{
    public class TaxSelectionItem : BaseViewModel
    {
        public int TaxId { get; set; }
        public string TaxCode { get; set; }
        public string TaxName { get; set; }
        public decimal Rate { get; set; }

        private bool _isSelected = true;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }
}
