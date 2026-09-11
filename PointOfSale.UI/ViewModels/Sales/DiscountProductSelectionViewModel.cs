namespace PointOfSale.UI.ViewModels.Sales
{
    public class DiscountProductSelectionViewModel : BaseViewModel
    {
        public int ProductId { get; set; }
        public string ProductCode { get; set; }
        public string ProductName { get; set; }

        private bool _isExcluded;
        public bool IsExcluded
        {
            get => _isExcluded;
            set => SetProperty(ref _isExcluded, value);
        }
    }
}
