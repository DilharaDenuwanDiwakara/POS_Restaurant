using System;
using System.Windows.Input;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Sales
{
    public class CloseShiftDialogViewModel : BaseViewModel
    {
        public CloseShiftDialogViewModel()
        {
            ConfirmCommand = new RelayCommand(_ => Confirm(), _ => CanConfirm());
            CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(false));
        }

        private decimal _physicalCash;
        public decimal PhysicalCash
        {
            get => _physicalCash;
            set
            {
                if (SetProperty(ref _physicalCash, value))
                {
                    ClearErrors(nameof(PhysicalCash));
                    (ConfirmCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }

        public event Action<bool?> CloseRequested;

        private void Confirm()
        {
            ClearAllErrors();

            if (PhysicalCash < 0)
            {
                AddError(nameof(PhysicalCash), "Physical cash cannot be negative.");
                return;
            }

            CloseRequested?.Invoke(true);
        }

        private bool CanConfirm()
        {
            return PhysicalCash >= 0;
        }
    }
}
