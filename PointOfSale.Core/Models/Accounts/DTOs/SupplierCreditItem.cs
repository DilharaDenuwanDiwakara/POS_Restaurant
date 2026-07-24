using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PointOfSale.Core.Models.Accounts.DTOs
{
    public class SupplierCreditItem : SupplierCredit, INotifyPropertyChanged
    {
        private bool _isSelected;
        private decimal _applyAmount;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();

                    // Auto-fill apply amount when selected
                    if (value && ApplyAmount == 0)
                    {
                        ApplyAmount = RemainingAmount;
                    }
                    else if (!value)
                    {
                        ApplyAmount = 0;
                    }
                }
            }
        }

        public decimal ApplyAmount
        {
            get => _applyAmount;
            set
            {
                // Ensure apply amount doesn't exceed remaining amount
                var validatedValue = Math.Max(0, Math.Min(value, RemainingAmount));
                if (_applyAmount != validatedValue)
                {
                    _applyAmount = validatedValue;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
