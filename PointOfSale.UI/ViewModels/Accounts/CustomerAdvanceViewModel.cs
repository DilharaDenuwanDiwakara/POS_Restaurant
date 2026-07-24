using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using PointOfSale.Core.Enums;
using PointOfSale.Core.Interfaces.Repositories.Accounts;
using PointOfSale.Core.Interfaces.Repositories.Sales;
using PointOfSale.Core.Models.Accounts;
using PointOfSale.Core.Models.Sales;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Accounts
{
    public class CustomerAdvanceViewModel : BaseViewModel
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly ICustomerAdvanceRepository _customerAdvanceRepository;
        private readonly IUserSessionService _userSessionService;
        public CustomerAdvanceViewModel(
            ICustomerRepository customerRepository,
            ICustomerAdvanceRepository customerAdvanceRepository,
            IUserSessionService userSessionService)
        {
            _customerRepository = customerRepository;
            _customerAdvanceRepository = customerAdvanceRepository;
            _userSessionService = userSessionService;

            Customers = new ObservableCollection<Customer>();
            PaymentMethods = new ObservableCollection<AccountsPaymentMethod>(
                (AccountsPaymentMethod[])Enum.GetValues(typeof(AccountsPaymentMethod)));
            UnappliedAdvances = new ObservableCollection<CustomerAdvanceList>();

            SaveAdvanceCommand = new AsyncRelayCommand(SaveAsync, CanSave);
            ClearCommand = new RelayCommand(ResetForm, CanClear);
            LoadAdvancesCommand = new AsyncRelayCommand(_ => LoadUnappliedAdvancesAsync());
            CancelAdvanceCommand = new AsyncRelayCommand(CancelAsync, CanUpdateOrCancel);

            _ = InitializeAsync();
        }

        #region Collections
        public ObservableCollection<Customer> Customers { get; }
        public ObservableCollection<AccountsPaymentMethod> PaymentMethods { get; }
        public ObservableCollection<CustomerAdvanceList> UnappliedAdvances { get; }
        #endregion

        #region Properties

        private CustomerAdvanceList _selectedAdvance;
        public CustomerAdvanceList SelectedAdvance
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

        private Customer _selecetedCustomer;
        public Customer SelectedCustomer
        {
            get => _selecetedCustomer;
            set
            {
                if (SetProperty(ref _selecetedCustomer, value))
                {
                    ValidateCustomer();
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
            await LoadCustomersAsync();
        }
        #endregion

        #region Command Implementation
        private bool CanSave(object parameter)
        {
            if (IsBusy)
                return false;

            if (SelectedCustomer == null)
                return false;

            if (string.IsNullOrWhiteSpace(PaymentAmount))
                return false;

            if (!decimal.TryParse(PaymentAmount, out var amount) || amount <= 0)
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
            SelectedCustomer = null;
            SelectedPaymentMethod = PaymentMethods.FirstOrDefault();
            PaymentDate = DateTime.Today;
            ReferenceNumber = string.Empty;
            PaymentAmount = string.Empty;
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

                var advancePayment = new CustomerAdvance
                {
                    CustomerId = SelectedCustomer.Id,
                    PaymentDate = PaymentDate,
                    PaymentMethod = SelectedPaymentMethod.ToString(),
                    ReferenceNumber = ReferenceNumber?.Trim(),
                    Amount = decimal.Parse(PaymentAmount),
                    CreatedBy = _userSessionService.UserId
                };

                await _customerAdvanceRepository.CreateAsync(advancePayment);
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
                int? filteredCustomerId = SelectedCustomer?.Id;
                var result = await _customerAdvanceRepository.GetUnappliedAdvanceAsync(filteredCustomerId);

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

            // Add a confirmation dialog here before proceeding!
            if (MessageBox.Show($"Are you sure you want to cancel advance {SelectedAdvance.TransactionNumber}? This will reverse the payment in the GL.", "Confirm Cancellation", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                IsBusy = true;
                await _customerAdvanceRepository.CancelAdvanceAsync(
                    SelectedAdvance.AdvanceId,
                    _userSessionService.UserId);

                ShowSuccessMessage($"Advance {SelectedAdvance.TransactionNumber} has been successfully cancelled and reversed.");

                await LoadUnappliedAdvancesAsync(); // Refresh the list
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
        private void ResetForm()
        {
            SelectedCustomer = null;
            SelectedPaymentMethod = PaymentMethods.FirstOrDefault();
            PaymentDate = DateTime.Today;
            ReferenceNumber = string.Empty;
            PaymentAmount = string.Empty;
            UnappliedAdvances.Clear();

            ClearAllErrors();

            RefreshCommands();

        }
        private async Task LoadCustomersAsync()
        {
            try
            {
                var result = await _customerRepository.GetAllAsync();
                Customers.Clear();

                foreach (var customer in result)
                    Customers.Add(customer);
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Failed to load customers: {ex.Message}");
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
            ValidateCustomer();
            ValidatePaymentDate();
            ValidatePaymentAmount();
            return !HasErrors;
        }

        private void ValidateCustomer()
        {
            ClearErrors(nameof(SelectedCustomer));
            if (SelectedCustomer == null)
                AddError(nameof(SelectedCustomer), "Please select a customer.");
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

        private bool IsValidDecimal(string value)
        {
            if (string.IsNullOrEmpty(value))
                return true;

            return Regex.IsMatch(value, @"^[0-9]*\.?[0-9]*$");
        }

        #endregion
    }
}
