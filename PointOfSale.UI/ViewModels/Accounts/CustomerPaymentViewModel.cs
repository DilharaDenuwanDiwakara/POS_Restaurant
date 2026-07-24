using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using PointOfSale.Core.Enums;
using PointOfSale.Core.Interfaces.Repositories.Accounts;
using PointOfSale.Core.Interfaces.Repositories.Sales;
using PointOfSale.Core.Interfaces.Repositories.System;
using PointOfSale.Core.Models.Accounts;
using PointOfSale.Core.Models.Sales;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Accounts
{
    public class CustomerPaymentViewModel : BaseViewModel
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ICustomerRepository _customerRepository;
        private readonly ICustomerPaymentRepository _customerPaymentRepository;
        private readonly IUserSessionService _userSessionService;
        private readonly IBankRepository _bankRepository;

        public CustomerPaymentViewModel(IServiceProvider serviceProvider,
                                        ICustomerRepository customerRepository,
                                        ICustomerPaymentRepository customerPaymentRepository,
                                        IUserSessionService userSessionService,
                                        IBankRepository bankRepository)
        {
            _serviceProvider = serviceProvider;
            _customerRepository = customerRepository;
            _customerPaymentRepository = customerPaymentRepository;
            _userSessionService = userSessionService;
            _bankRepository = bankRepository ?? throw new ArgumentNullException(nameof(bankRepository));

            Customers = new ObservableCollection<Customer>();
            ReceivableItems = new ObservableCollection<CustomerReceivableItem>();
            PaymentMethods = new ObservableCollection<AccountsPaymentMethod>((AccountsPaymentMethod[])Enum.GetValues(typeof(AccountsPaymentMethod)));


            LoadReceivableCommand = new AsyncRelayCommand(_ => LoadReceivableAsync(), _ => CanLoadReceivable());
            SavePaymentCommand = new AsyncRelayCommand(_ => SaveAsync(), _ => CanSave());

            Task.Run(LoadCustomerAsync);
            _ = LoadBanksAsync();
        }

        public List<AccountsPaymentMethod> AvailablePaymentMethods { get; } =
            Enum.GetValues(typeof(AccountsPaymentMethod))
                    .Cast<AccountsPaymentMethod>()
                    .ToList();

        public ObservableCollection<string> AvailableBanks { get; } = new ObservableCollection<string>();

        private AccountsPaymentMethod _paymentMethod;
        public AccountsPaymentMethod PaymentMethod
        {
            get => _paymentMethod;
            set
            {
                if (SetProperty(ref _paymentMethod, value))
                {
                    OnPropertyChanged(nameof(IsBankVisible));
                    OnPropertyChanged(nameof(IsAccountNameVisible));
                    OnPropertyChanged(nameof(IsAccountNumberVisible));

                }
            }
        }

        private string _bankName;
        public string BankName
        {
            get => _bankName;
            set => SetProperty(ref _bankName, value);
        }

        private string _accountNumber;
        public string AccountNumber
        {
            get => _accountNumber;
            set => SetProperty(ref _accountNumber, value);
        }
        private string _accountName;
        public string AccountName
        {
            get => _accountName;
            set => SetProperty(ref _accountName, value);
        }

        public Visibility IsBankVisible =>
           SelectedPaymentMethod == AccountsPaymentMethod.BANK_TRANSFER ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsAccountNumberVisible =>
            SelectedPaymentMethod == AccountsPaymentMethod.BANK_TRANSFER ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsAccountNameVisible =>
            (SelectedPaymentMethod == AccountsPaymentMethod.BANK_TRANSFER || PaymentMethod == AccountsPaymentMethod.CHEQUE)
            ? Visibility.Visible : Visibility.Collapsed;

        #region Properties
        public ObservableCollection<Customer> Customers { get; }
        public ObservableCollection<AccountsPaymentMethod> PaymentMethods { get; }
        public ObservableCollection<CustomerReceivableItem> ReceivableItems { get; }
        public CustomerPayment Payment { get; set; } = new CustomerPayment();

        private Customer _selectedCustomer;
        public Customer SelectedCustomer
        {
            get => _selectedCustomer;
            set
            {
                if (SetProperty(ref _selectedCustomer, value))
                {
                    ReceivableItems.Clear();
                    LoadReceivableCommand.RaiseCanExecuteChanged();
                    SavePaymentCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private DateTime _PaymentDate = DateTime.Today;
        public DateTime PaymentDate
        {
            get => _PaymentDate;
            set => SetProperty(ref _PaymentDate, value);
        }

        private AccountsPaymentMethod? _selectedPaymentMethod;
        public AccountsPaymentMethod? SelectedPaymentMethod
        {
            get => _selectedPaymentMethod;
            set
            {
                if (SetProperty(ref _selectedPaymentMethod, value))
                {
                    // A. Update Visibility based on selection
                    OnPropertyChanged(nameof(IsBankVisible));
                    OnPropertyChanged(nameof(IsAccountNameVisible));
                    OnPropertyChanged(nameof(IsAccountNumberVisible));

                    // B. CRITICAL: Tell the Save button to re-check
                    SavePaymentCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private decimal _totalSelectedReceivableAmount;
        public decimal TotalSelectedReceivableAmount
        {
            get => _totalSelectedReceivableAmount;
            set => SetProperty(ref _totalSelectedReceivableAmount, value);
        }

        private int _selectedItemCount;
        public int SelectedItemCount
        {
            get => _selectedItemCount;
            set => SetProperty(ref _selectedItemCount, value);
        }
        #endregion

        #region Commands
        public AsyncRelayCommand LoadReceivableCommand { get; }
        public AsyncRelayCommand SavePaymentCommand { get; }
        #endregion

        #region Helper Methods
        private async Task LoadBanksAsync()
        {
            try
            {
                AvailableBanks.Clear();
                var banks = await _bankRepository.GetAllAsync();
                foreach (var bank in banks.Where(b => b.IsActive))
                    AvailableBanks.Add(bank.BankName);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load banks: {ex.Message}";
            }
        }

        private void ClearForm()
        {
            Payment = new CustomerPayment();
            foreach (var r in ReceivableItems)
            {
                r.IsSelected = false;
                r.PaymentAmount = 0;
            }
        }
        private async Task LoadCustomerAsync()
        {
            try
            {
                var result = await _customerRepository.GetAllAsync();

                foreach (var customers in result)
                {
                    Customers.Add(customers);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load customers: {ex.Message}";
            }
        }

        private void RecalculateTotals()
        {
            SelectedItemCount = ReceivableItems.Count(p => p.IsSelected);

            TotalSelectedReceivableAmount = ReceivableItems
                .Where(p => p.IsSelected)
                .Sum(p => p.PaymentAmount);

            Payment.PaidAmount = TotalSelectedReceivableAmount;

            SavePaymentCommand.RaiseCanExecuteChanged();
        }
        protected override void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            base.OnPropertyChanged(propertyName);

            // This is a common pattern to recalculate a dependent property
            if (propertyName == nameof(ReceivableItems))
            {
                RecalculateTotals();
            }
        }

        // This method is called whenever IsSelected or PaymentAmount changes on a PayableItem
        private void ReceivableItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CustomerReceivableItem.IsSelected) ||
                e.PropertyName == nameof(CustomerReceivableItem.PaymentAmount))
            {
                RecalculateTotals();
            }
        }
        #endregion

        #region Methods
        private bool CanLoadReceivable() => SelectedCustomer != null;
        private async Task LoadReceivableAsync()
        {
            if (SelectedCustomer == null) return;

            try
            {
                ErrorMessage = null;

                var result = await _customerPaymentRepository.GetCustomerReceivableAsync(SelectedCustomer.Id);

                ReceivableItems.Clear();

                foreach (var item in result)
                {
                    var receivableItem = new CustomerReceivableItem
                    {
                        CustomerReceivableId = item.CustomerReceivableId,
                        InvoiceNumber = item.InvoiceNumber,
                        CustomerId = item.CustomerId,
                        ReferenceId = item.ReferenceId,
                        TransactionDate = item.TransactionDate,
                        DueDate = item.DueDate,
                        InitialAmount = item.InitialAmount,
                        AmountSettled = item.AmountSettled,
                        BalanceAmount = item.BalanceAmount,
                        Status = item.Status,
                        IsSelected = false,
                        PaymentAmount = item.BalanceAmount
                    };
                    receivableItem.PropertyChanged += ReceivableItem_PropertyChanged;

                    ReceivableItems.Add(receivableItem);
                }
                RecalculateTotals();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load payables: {ex.Message}";
            }
        }

        private bool CanSave()
        {
            return Payment.PaidAmount > 0
                    && SelectedPaymentMethod.HasValue;
        }
        private async Task SaveAsync()
        {
            try
            {
                Payment.CustomerId = SelectedCustomer.Id;
                Payment.PaymentDate = PaymentDate;
                Payment.PaymentMethod = SelectedPaymentMethod?.ToString();
                Payment.ReferenceNumber = null;
                Payment.PaidAmount = TotalSelectedReceivableAmount;
                Payment.CreatedBy = _userSessionService.UserId;

                if (SelectedPaymentMethod == AccountsPaymentMethod.BANK_TRANSFER)
                {
                    Payment.BankName = BankName;
                    Payment.AccountNumber = AccountNumber;
                }
                else
                {
                    // Clear these for Cash/Cheque so we don't save junk data
                    Payment.BankName = null;
                    Payment.AccountNumber = null;
                }

                // logic: Save Account Name (Payee) for both Bank Transfer AND Cheque
                if (SelectedPaymentMethod == AccountsPaymentMethod.BANK_TRANSFER ||
                    SelectedPaymentMethod == AccountsPaymentMethod.CHEQUE)
                {
                    Payment.AccountName = AccountName;
                }
                else
                {
                    Payment.AccountName = null;
                }

                Payment.PaymentLines = ReceivableItems
                    .Where(r => r.IsSelected && r.PaymentAmount > 0)
                    .Select(r => new CustomerPaymentLine
                    {
                        CustomerReceivableId = r.CustomerReceivableId,
                        AmountApplied = r.PaymentAmount

                    })
                    .ToList();

                long newId = await _customerPaymentRepository.CreateAsync(Payment);
                MessageBox.Show($"Payment saved successfully (ID: {newId})", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                ClearForm();
                await LoadReceivableAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while saving the payment: {ex.Message}");
            }
        }

        #endregion

    }
}
