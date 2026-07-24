namespace PointOfSale.UI.ViewModels.Sales
{
    public class InvoiceLineItemModel : BaseViewModel
    {
        private string _itemName;
        public string ItemName
        {
            get => _itemName;
            set => SetProperty(ref _itemName, value);
        }

        private decimal _qty;
        public decimal Qty
        {
            get => _qty;
            set
            {
                if (SetProperty(ref _qty, value))
                {
                    OnPropertyChanged(nameof(Total));
                }
            }
        }

        private decimal _unitPrice;
        public decimal UnitPrice
        {
            get => _unitPrice;
            set
            {
                if (SetProperty(ref _unitPrice, value))
                {
                    OnPropertyChanged(nameof(Total));
                }
            }
        }

        private decimal _storedTotal;
        public decimal StoredTotal
        {
            get => _storedTotal;
            set
            {
                if (SetProperty(ref _storedTotal, value))
                {
                    OnPropertyChanged(nameof(Total));
                }
            }
        }

        public decimal Total => StoredTotal != 0m ? StoredTotal : Qty * UnitPrice;
    }
}
