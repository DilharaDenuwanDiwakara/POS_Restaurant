using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PointOfSale.Core.Models.Accounts
{
    public class CustomerReceivableItem : CustomerReceivable, INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler PropertyChanged;

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value); // Will be handled in ViewModel
        }

        private decimal _paymentAmount;
        public decimal PaymentAmount
        {
            get => _paymentAmount;
            set
            {
                if (SetProperty(ref _paymentAmount, value))
                {
                    ValidatePaymentAmount();
                }
            }
        }

        private void ValidatePaymentAmount()
        {
            if (PaymentAmount < 0)
            {
                PaymentAmount = 0;
                OnPropertyChanged(nameof(PaymentAmount));
            }
            else if (PaymentAmount > BalanceAmount)
            {
                PaymentAmount = BalanceAmount;
                OnPropertyChanged(nameof(PaymentAmount));
            }
        }

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

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
