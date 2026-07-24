using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using PointOfSale.Core.Interfaces.Repositories.Sales;
using PointOfSale.Core.Models.Sales;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Sales
{
    public class CashInOutViewModel : BaseViewModel
    {
        private readonly ICashInOutRepository _cashInOutRepository;
        private readonly IUserSessionService _userSessionService;

        public CashInOutViewModel(ICashInOutRepository cashInOutRepository, IUserSessionService userSessionService)
        {
            _cashInOutRepository = cashInOutRepository ?? throw new ArgumentNullException(nameof(cashInOutRepository));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));

            // Initialize Collection to prevent NullReferenceException
            Transactions = new ObservableCollection<CashInOut>();

            SaveCashCommand = new AsyncRelayCommand(async _ => await SaveAsync());
            LoadCashCommand = new AsyncRelayCommand(async _ => await LoadAsync());
            ClearCommand = new RelayCommand(_ => ResetForm());

            // Initialize by loading existing transactions.
            _ = LoadAsync();
        }

        #region Collections / Models
        public List<string> TransactionTypes { get; } = new List<string>()
        {
            "Cash-In", "Cash-Out"
        };
        public ObservableCollection<CashInOut> Transactions { get; }

        private CashInOut _selectedTransaction;
        public CashInOut SelectedTransaction
        {
            get => _selectedTransaction;
            set
            {
                if (SetProperty(ref _selectedTransaction, value))
                {
                    // populate editing fields when selection changes
                    if (_selectedTransaction != null)
                    {
                        TransactionType = _selectedTransaction.TransactionType;
                        Amount = _selectedTransaction.Amount;
                        Reason = _selectedTransaction.Reason;
                    }
                    OnPropertyChanged(nameof(SaveButtonText));

                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }
        #endregion

        #region Form fields / validation
        private string _transactionType;
        public string TransactionType
        {
            get => _transactionType;
            set
            {
                if (SetProperty(ref _transactionType, value))
                {
                    ValidateTransactionType(); //

                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private decimal _amount;
        public decimal Amount
        {
            get => _amount;
            set
            {
                if (SetProperty(ref _amount, value))
                {
                    ValidateAmount();

                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private string _reason;
        public string Reason
        {
            get => _reason;
            set => SetProperty(ref _reason, value);

        }
        public string SaveButtonText => SelectedTransaction == null ? "Save" : "Update";
        #endregion

        #region Commands
        public ICommand SaveCashCommand { get; }
        public ICommand LoadCashCommand { get; }
        public ICommand ClearCommand { get; }
        #endregion

        #region CRUD
        private async Task LoadAsync()
        {
            try
            {
                int currentLocationId = _userSessionService.BranchId;

                var items = await _cashInOutRepository.GetAllAsync(currentLocationId);

                Transactions.Clear();
                if (items != null)
                {
                    foreach (var item in items.OrderByDescending(x => x.TransactionDate)) // Assuming you have a Date field
                    {
                        Transactions.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }
        private async Task SaveAsync()
        {
            try
            {
                ClearAllErrors();
                ValidateTransactionType();
                ValidateAmount();

                if (HasErrors) return;

                var currentShiftId = _userSessionService.CurrentShiftId;
                if (!currentShiftId.HasValue)
                {
                    MessageBox.Show("No open shift was found for the current session.", "Cash Transaction", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var model = new CashInOut
                {
                    TransactionType = TransactionType,
                    Amount = Amount,
                    Reason = string.IsNullOrWhiteSpace(Reason) ? null : Reason,
                    CreatedBy = _userSessionService.UserId,
                    LocationId = _userSessionService.BranchId,
                    ShiftId = currentShiftId.Value
                };

                await _cashInOutRepository.CreateAsync(model);
                MessageBox.Show("Transaction saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                await LoadAsync();
                ResetForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Transaction saving error.{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        public void ResetForm()
        {
            // Clear properties directly (avoiding validation triggers if desired, or let them reset)
            _transactionType = null;
            OnPropertyChanged(nameof(TransactionType));

            _amount = 0m;
            OnPropertyChanged(nameof(Amount));

            Reason = string.Empty;

            SelectedTransaction = null;
            OnPropertyChanged(nameof(SaveButtonText));

            ClearAllErrors();
        }
        #endregion

        #region Validation helpers
        private void ValidateTransactionType()
        {
            ClearErrors(nameof(TransactionType));
            if (string.IsNullOrWhiteSpace(TransactionType))
            {
                AddError(nameof(TransactionType), "Transaction type is required.");
            }
        }
        private void ValidateAmount()
        {
            ClearErrors(nameof(Amount));
            if (Amount <= 0m)
            {
                AddError(nameof(Amount), "Amount must be greater than zero.");
            }
        }
        private bool CanSave()
        {
            return !HasErrors
                && !string.IsNullOrWhiteSpace(TransactionType)
                && Amount > 0m;
        }
        #endregion
    }
}
