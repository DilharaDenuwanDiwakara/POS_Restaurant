using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PointOfSale.Core.Models.Sales
{
    public class SalesReturnItemModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private long _salesLineId;
        private string _productName;
        private decimal _soldQty;
        private decimal _unitPrice;
        private decimal _returnQty;
        private int _returnReasonId;
        private bool _isWastage;

        public long SalesLineId
        {
            get => _salesLineId;
            set => SetProperty(ref _salesLineId, value);
        }

        public string ProductName
        {
            get => _productName;
            set => SetProperty(ref _productName, value);
        }

        public decimal SoldQty
        {
            get => _soldQty;
            set => SetProperty(ref _soldQty, value);
        }

        public decimal UnitPrice
        {
            get => _unitPrice;
            set
            {
                if (SetProperty(ref _unitPrice, value))
                {
                    OnPropertyChanged(nameof(RefundAmount));
                }
            }
        }

        public decimal ReturnQty
        {
            get => _returnQty;
            set
            {
                // Clamp to a valid range so the cashier can never return more than was sold.
                var clamped = value < 0 ? 0 : (value > SoldQty ? SoldQty : value);

                if (SetProperty(ref _returnQty, clamped))
                {
                    OnPropertyChanged(nameof(RefundAmount));
                }
            }
        }

        public int ReturnReasonId
        {
            get => _returnReasonId;
            set => SetProperty(ref _returnReasonId, value);
        }

        public bool IsWastage
        {
            get => _isWastage;
            set => SetProperty(ref _isWastage, value);
        }

        public decimal RefundAmount => ReturnQty * UnitPrice;

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value))
            {
                return false;
            }
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
