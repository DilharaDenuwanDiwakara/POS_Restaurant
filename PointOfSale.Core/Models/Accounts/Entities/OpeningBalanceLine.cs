using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PointOfSale.Core.Models.Accounts.Entities
{
    public class OpeningBalanceLine : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private decimal _debitAmount;
        private decimal _creditAmount;

        public int AccountId { get; set; }
        public string AccountCode { get; set; }
        public string AccountName { get; set; }

        public decimal DebitAmount
        {
            get => _debitAmount;
            set
            {
                if (_debitAmount != value)
                {
                    _debitAmount = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasConflict));
                }
            }
        }

        public decimal CreditAmount
        {
            get => _creditAmount;
            set
            {
                if (_creditAmount != value)
                {
                    _creditAmount = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasConflict));
                }
            }
        }

        public bool HasConflict => DebitAmount > 0m && CreditAmount > 0m;
    }
}
