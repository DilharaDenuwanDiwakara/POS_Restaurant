using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Enums;
using PointOfSale.Core.Interfaces;
using PointOfSale.Core.Interfaces.Purchasing;
using PointOfSale.Core.Interfaces.Repositories.Accounts;
using PointOfSale.Core.Models.Accounts;
using PointOfSale.Core.Models.Purchasing;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Accounts
{
    public class SupplierAdvanceViewModel : BaseViewModel
    {
        private readonly ISupplierRepository _supplierRepository;
        private readonly ISupplierAdvanceRepository _supplierAdvanceRepository;
        private readonly IUserSessionService _userSessionService;
        private readonly IAccountingRepository _accountingRepository;

        public SupplierAdvanceViewModel(ISupplierRepository supplierRepository,
                                        ISupplierAdvanceRepository supplierAdvanceRepository,
                                        IUserSessionService userSessionService,
                                        IAccountingRepository accountingRepository)
        {
            _supplierRepository = supplierRepository;
            _supplierAdvanceRepository = supplierAdvanceRepository;
            _userSessionService = userSessionService;
            _accountingRepository = accountingRepository ?? throw new ArgumentNullException(nameof(accountingRepository));

            Suppliers = new ObservableCollection<Supplier>();
            PaymentAccounts = new ObservableCollection<AccountDto>();
            PaymentMethods = new ObservableCollection<AccountsPaymentMethod>(
                (AccountsPaymentMethod[])Enum.GetValues(typeof(AccountsPaymentMethod)));
            UnappliedAdvances = new ObservableCollection<SupplierAdvanceList>();

            SaveAdvanceCommand = new AsyncRelayCommand(SaveAsync, CanSave);
            ClearCommand = new RelayCommand(ResetForm, CanClear);
            LoadAdvancesCommand = new AsyncRelayCommand(_ => LoadUnappliedAdvancesAsync());
            CancelAdvanceCommand = new AsyncRelayCommand(CancelAsync, CanUpdateOrCancel);

            _ = InitializeAsync();
        }

        #region Collections
        public ObservableCollection<Supplier> Suppliers { get; }
        public ObservableCollection<AccountDto> PaymentAccounts { get; }
        public ObservableCollection<AccountsPaymentMethod> PaymentMethods { get; }
        public ObservableCollection<SupplierAdvanceList> UnappliedAdvances { get; }
        #endregion

        #region Properties
        private SupplierAdvanceList _selectedAdvance;
        public SupplierAdvanceList SelectedAdvance
        {
            get => _selectedAdvance;
            set
            {
                if (SetProperty(ref _selectedAdvance, value))
                {
                    ((AsyncRelayCommand)CancelAdvanceCommand).RaiseCanExecuteChanged();
                }
            }
        }

        private Supplier _selectedSupplier;
        public Supplier SelectedSupplier
        {
            get => _selectedSupplier;
            set
            {
                if (SetProperty(ref _selectedSupplier, value))
                {
                    ValidateSupplier();
                    _ = LoadUnappliedAdvancesAsync();
                    RefreshCommands();
                }
            }
        }

        private AccountsPaymentMethod _selectedPaymentMethod;
        public AccountsPaymentMethod SelectedPaymentMethod
        {
            get => _selectedPaymentMethod;
            set
            {
                if (SetProperty(ref _selectedPaymentMethod, value))
                {
                    RefreshCommands();
                }
            }
        }

        private int _selectedPaymentAccountId;
        public int SelectedPaymentAccountId
        {
            get => _selectedPaymentAccountId;
            set
            {
                if (SetProperty(ref _selectedPaymentAccountId, value))
                {
                    RefreshCommands();
                }
            }
        }

        private string _paymentAmount;
        public string PaymentAmount
        {
            get => _paymentAmount;
            set
            {
                if (IsValidDecimal(value) || string.IsNullOrEmpty(value))
                {
                    if (SetProperty(ref _paymentAmount, value))
                    {
                        ValidatePaymentAmount();
                        RefreshCommands();
                    }
                }
            }
        }

        private DateTime _paymentDate = DateTime.Today;
        public DateTime PaymentDate
        {
            get => _paymentDate;
            set
            {
                if (SetProperty(ref _paymentDate, value))
                    ValidatePaymentDate();
            }
        }

        private string _referenceNumber;
        public string ReferenceNumber
        {
            get => _referenceNumber;
            set => SetProperty(ref _referenceNumber, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                    RefreshCommands();
            }
        }

        public bool IsNotBusy => !IsBusy;
        #endregion

        #region Commands
        public AsyncRelayCommand SaveAdvanceCommand { get; }
        public AsyncRelayCommand LoadAdvancesCommand { get; }
        public AsyncRelayCommand CancelAdvanceCommand { get; }
        public RelayCommand ClearCommand { get; }
        #endregion

        #region Initialization
        private async Task InitializeAsync()
        {
            await LoadSuppliersAsync();
            await LoadPaymentAccountsAsync();
        }
        #endregion

        #region Command Implementation
        private bool CanSave(object parameter)
        {
            if (IsBusy)
                return false;

            if (SelectedSupplier == null)
                return false;

            if (string.IsNullOrWhiteSpace(PaymentAmount))
                return false;

            if (!decimal.TryParse(PaymentAmount, out var amount) || amount <= 0)
                return false;

            if (SelectedPaymentAccountId <= 0)
                return false;

            return !HasErrors;
        }
        private bool CanClear(object parameter) => !IsBusy;
        private bool CanUpdateOrCancel(object parameter)
        {
            return !IsBusy && SelectedAdvance != null;
        }
        private void RefreshCommands()
        {
            ((AsyncRelayCommand)SaveAdvanceCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ClearCommand).RaiseCanExecuteChanged();
        }
        private void ResetForm(object obj = null)
        {
            SelectedSupplier = null;
            SelectedPaymentMethod = PaymentMethods.FirstOrDefault();
            SelectedPaymentAccountId = PaymentAccounts.FirstOrDefault()?.Id ?? 0;
            PaymentDate = DateTime.Today;
            ReferenceNumber = string.Empty;
            PaymentAmount = string.Empty;
            UnappliedAdvances.Clear();
            ClearAllErrors();
            RefreshCommands();
        }
        private async Task SaveAsync(object parameter)
        {
            if (!ValidateAllFields())
                return;

            try
            {
                IsBusy = true;
                ErrorMessage = null;

                var advancePayment = new SupplierAdvance
                {
                    SupplierId = SelectedSupplier.SupplierId,
                    PaymentDate = PaymentDate,
                    PaymentMethod = SelectedPaymentMethod.ToString(),
                    ReferenceNumber = ReferenceNumber?.Trim(),
                    PaymentAccountId = SelectedPaymentAccountId,
                    Amount = decimal.Parse(PaymentAmount),
                    CreatedBy = _userSessionService.UserId
                };

                await _supplierAdvanceRepository.CreateAsync(advancePayment);
                ShowSuccessMessage("Advance payment saved successfully!");
                ResetForm();
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error saving advance payment: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }
        private async Task LoadUnappliedAdvancesAsync()
        {
            try
            {
                IsBusy = true;
                int? filteredSupplierId = SelectedSupplier?.SupplierId;
                var result = await _supplierAdvanceRepository.GetUnappliedAdvanceAsync(filteredSupplierId);

                UnappliedAdvances.Clear();
                foreach (var advance in result)
                    UnappliedAdvances.Add(advance);
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Failed to load unapplied advances: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }
        private async Task CancelAsync(object parameter)
        {
            if (SelectedAdvance == null) return;

            if (MessageBox.Show(
                $"Are you sure you want to cancel advance {SelectedAdvance.TransactionNumber}? This will reverse the payment in the GL.",
                "Confirm Cancellation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                IsBusy = true;
                await _supplierAdvanceRepository.CancelAdvanceAsync(
                    SelectedAdvance.AdvanceId,
                    _userSessionService.UserId);

                ShowSuccessMessage($"Advance {SelectedAdvance.TransactionNumber} has been successfully cancelled and reversed.");

                await LoadUnappliedAdvancesAsync();
                ResetForm();
                SelectedAdvance = null;
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error cancelling advance: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }
        #endregion

        #region Helper Methods
        private async Task LoadSuppliersAsync()
        {
            try
            {
                var result = await _supplierRepository.GetAllAsync();
                Suppliers.Clear();

                foreach (var supplier in result)
                    Suppliers.Add(supplier);
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Failed to load suppliers: {ex.Message}");
            }
        }
        private async Task LoadPaymentAccountsAsync()
        {
            try
            {
                var accounts = await _accountingRepository.GetPaymentAccountsAsync();
                PaymentAccounts.Clear();
                foreach (var account in accounts)
                {
                    PaymentAccounts.Add(account);
                }

                SelectedPaymentAccountId = PaymentAccounts.FirstOrDefault()?.Id ?? 0;
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Failed to load payment accounts: {ex.Message}");
            }
        }
        private void ShowSuccessMessage(string message)
            => MessageBox.Show(message, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        private void ShowErrorMessage(string message)
            => MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        #endregion

        #region Validation
        private bool ValidateAllFields()
        {
            ValidateSupplier();
            ValidatePaymentDate();
            ValidatePaymentAmount();
            ValidatePaymentAccount();
            return !HasErrors;
        }

        private void ValidateSupplier()
        {
            ClearErrors(nameof(SelectedSupplier));
            if (SelectedSupplier == null)
                AddError(nameof(SelectedSupplier), "Please select a supplier.");
        }

        private void ValidatePaymentDate()
        {
            ClearErrors(nameof(PaymentDate));

            if (PaymentDate.Date > DateTime.Today)
                AddError(nameof(PaymentDate), "Payment date cannot be in the future.");
        }

        private void ValidatePaymentAmount()
        {
            ClearErrors(nameof(PaymentAmount));
            if (string.IsNullOrWhiteSpace(PaymentAmount))
            {
                AddError(nameof(PaymentAmount), "Please enter a payment amount.");
                return;
            }

            if (!decimal.TryParse(PaymentAmount, out var amount))
            {
                AddError(nameof(PaymentAmount), "Please enter a valid number.");
                return;
            }

            if (amount <= 0)
                AddError(nameof(PaymentAmount), "Amount must be greater than zero.");
        }

        private void ValidatePaymentAccount()
        {
            ClearErrors(nameof(SelectedPaymentAccountId));
            if (SelectedPaymentAccountId <= 0)
                AddError(nameof(SelectedPaymentAccountId), "Please select the account to pay from.");
        }

        private bool IsValidDecimal(string value)
        {
            if (string.IsNullOrEmpty(value))
                return true;

            return Regex.IsMatch(value, @"^[0-9]*\.?[0-9]*$");
        }
        #endregion
    }
}
