using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using CrystalDecisions.CrystalReports.Engine;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Enums;
using PointOfSale.Core.Interfaces;
using PointOfSale.Core.Interfaces.Purchasing;
using PointOfSale.Core.Interfaces.Repositories.Accounts;
using PointOfSale.Core.Interfaces.Repositories.System;
using PointOfSale.Core.Models.Accounts;
using PointOfSale.Core.Models.Purchasing;
using PointOfSale.Core.Models.System;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;
using PointOfSale.UI.Views.Accounts;

namespace PointOfSale.UI.ViewModels.Accounts
{
    public class SupplierPaymentViewModel : BaseViewModel
    {
        private readonly ISupplierRepository _supplierRepository;
        private readonly ISupplierPaymentRepository _supplierPaymentRepository;
        private readonly IUserSessionService _userSessionService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IBankRepository _bankRepository;
        private readonly IBankBranchRepository _bankBranchRepository;
        private readonly IAccountingRepository _accountingRepository;

        public SupplierPaymentViewModel(ISupplierRepository supplierRepository,
                                        ISupplierPaymentRepository supplierPaymentRepository,
                                        IUserSessionService userSessionService,
                                        IServiceProvider serviceProvider,
                                        IBankRepository bankRepository,
                                        IBankBranchRepository bankBranchRepository,
                                        IAccountingRepository accountingRepository)
        {
            _supplierRepository = supplierRepository;
            _supplierPaymentRepository = supplierPaymentRepository;
            _userSessionService = userSessionService;
            _serviceProvider = serviceProvider;
            _bankRepository = bankRepository ?? throw new ArgumentNullException(nameof(bankRepository));
            _bankBranchRepository = bankBranchRepository ?? throw new ArgumentNullException(nameof(bankBranchRepository));
            _accountingRepository = accountingRepository ?? throw new ArgumentNullException(nameof(accountingRepository));

            Suppliers = new ObservableCollection<Supplier>();
            PayableItems = new ObservableCollection<SupplierPayableItem>();
            PaymentAccounts = new ObservableCollection<AccountDto>();
            PaymentMethods = new ObservableCollection<AccountsPaymentMethod>((AccountsPaymentMethod[])Enum.GetValues(typeof(AccountsPaymentMethod)));

            LoadPayablesCommand = new AsyncRelayCommand(_ => LoadPayablesAsync(), _ => CanLoadPayables());
            SavePaymentCommand = new AsyncRelayCommand(_ => SavePaymentAsync(), _ => CanSavePayment());

            _ = LoadSuppliersAsync();
            _ = LoadBanksAsync();
            _ = LoadPaymentAccountsAsync();
        }
        public List<AccountsPaymentMethod> AvailablePaymentMethods { get; } =
            Enum.GetValues(typeof(AccountsPaymentMethod))
                    .Cast<AccountsPaymentMethod>()
                    .ToList();
        public ObservableCollection<Supplier> Suppliers { get; }
        public ObservableCollection<AccountsPaymentMethod> PaymentMethods { get; }
        public ObservableCollection<SupplierPayableItem> PayableItems { get; }
        public ObservableCollection<AccountDto> PaymentAccounts { get; }
        public SupplierPayment Payment { get; set; } = new SupplierPayment();

        public ObservableCollection<Bank> AvailableBanks { get; } = new ObservableCollection<Bank>();
        public ObservableCollection<BankBranch> AvailableBranches { get; } = new ObservableCollection<BankBranch>();
        private bool _isLoadingSupplier;

        #region Properties

        private int? _selectedBankId;
        public int? SelectedBankId
        {
            get => _selectedBankId;
            set
            {
                if (SetProperty(ref _selectedBankId, value))
                {
                    SelectedBranchId = null;
                    _ = LoadBranchesForSelectedBankAsync();
                    OnPropertyChanged(nameof(IsBranchEnabled));

                    var selectedBank = AvailableBanks.FirstOrDefault(b => b.Id == value);
                    BankName = selectedBank?.BankName;
                }
            }
        }

        private int? _selectedBranchId;
        public int? SelectedBranchId
        {
            get => _selectedBranchId;
            set => SetProperty(ref _selectedBranchId, value);
        }

        public bool IsBranchEnabled => SelectedBankId.HasValue;

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
            (SelectedPaymentMethod == AccountsPaymentMethod.BANK_TRANSFER || SelectedPaymentMethod == AccountsPaymentMethod.CHEQUE)
            ? Visibility.Visible : Visibility.Collapsed;


        private Supplier _selectedSupplier;
        public Supplier SelectedSupplier
        {
            get => _selectedSupplier;
            set
            {
                if (SetProperty(ref _selectedSupplier, value))
                {
                    if (_selectedSupplier != null)
                    {
                        AccountNumber = _selectedSupplier.AccountNumber;
                        AccountName = _selectedSupplier.AccountName;

                        _ = UpdateSupplierBankSelectionAsync(_selectedSupplier);

                        // Auto-select payment method if the supplier has a default one
                        if (_selectedSupplier.DefaultPaymentMethod != null)
                        {
                            // Supplier.DefaultPaymentMethod is SupplierPaymentMethod; the Accounts
                            // module uses its own AccountsPaymentMethod, so convert by name.
                            SelectedPaymentMethod = (AccountsPaymentMethod)Enum.Parse(
                                typeof(AccountsPaymentMethod),
                                _selectedSupplier.DefaultPaymentMethod.Value.ToString());
                        }
                    }
                    else
                    {
                        // Clear fields if supplier is deselected
                        BankName = string.Empty;
                        SelectedBankId = null;
                        SelectedBranchId = null;
                        AccountNumber = string.Empty;
                        AccountName = string.Empty;
                        SelectedPaymentMethod = null;
                    }
                    PayableItems.Clear();
                    LoadPayablesCommand.RaiseCanExecuteChanged();
                    SavePaymentCommand.RaiseCanExecuteChanged();

                    if (LoadPayablesCommand.CanExecute(null))
                    {
                        LoadPayablesCommand.Execute(null);
                    }
                }
            }
        }

        private async Task UpdateSupplierBankSelectionAsync(Supplier supplier)
        {
            SelectedBankId = supplier.BankId;
            if (SelectedBankId.HasValue)
            {
                await LoadBranchesForSelectedBankAsync();
                SelectedBranchId = supplier.BankBranchId;
            }
        }

        private DateTime _paymentDate = DateTime.Today;
        public DateTime PaymentDate
        {
            get => _paymentDate;
            set => SetProperty(ref _paymentDate, value);
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

        private int _selectedPaymentAccountId;
        public int SelectedPaymentAccountId
        {
            get => _selectedPaymentAccountId;
            set
            {
                if (SetProperty(ref _selectedPaymentAccountId, value))
                {
                    SavePaymentCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private decimal _totalSelectedPayableAmount;
        public decimal TotalSelectedPayableAmount
        {
            get => _totalSelectedPayableAmount;
            set => SetProperty(ref _totalSelectedPayableAmount, value);
        }

        private int _selectedItemCount;
        public int SelectedItemCount
        {
            get => _selectedItemCount;
            set => SetProperty(ref _selectedItemCount, value);
        }
        #endregion

        #region Commands
        public AsyncRelayCommand LoadPayablesCommand { get; }
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
                    AvailableBanks.Add(bank);

                if (!string.IsNullOrWhiteSpace(BankName))
                {
                    var matchedBank = AvailableBanks.FirstOrDefault(b => string.Equals(b.BankName, BankName, StringComparison.OrdinalIgnoreCase));
                    if (matchedBank != null)
                    {
                        SelectedBankId = matchedBank.Id;
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load banks: {ex.Message}";
            }
        }

        private async Task LoadBranchesForSelectedBankAsync()
        {
            try
            {
                AvailableBranches.Clear();
                if (SelectedBankId.HasValue)
                {
                    var branches = await _bankBranchRepository.GetByBankIdAsync(SelectedBankId.Value);
                    foreach (var branch in branches.Where(b => b.IsActive))
                    {
                        AvailableBranches.Add(branch);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load branches: {ex.Message}";
            }
        }

        private async Task LoadPaymentAccountsAsync()
        {
            try
            {
                PaymentAccounts.Clear();
                var accounts = await _accountingRepository.GetPaymentAccountsAsync();
                foreach (var account in accounts)
                {
                    PaymentAccounts.Add(account);
                }

                SelectedPaymentAccountId = PaymentAccounts.FirstOrDefault()?.Id ?? 0;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load payment accounts: {ex.Message}";
            }
        }

        private void ClearForm()
        {
            Payment = new SupplierPayment();
            SelectedPaymentAccountId = PaymentAccounts.FirstOrDefault()?.Id ?? 0;
            foreach (var r in PayableItems)
            {
                r.IsSelected = false;
                r.PaymentAmount = 0;
            }
        }
        private async Task LoadSuppliersAsync()
        {
            try
            {
                var result = await _supplierRepository.GetAllAsync();

                foreach (var supplier in result)
                    Suppliers.Add(supplier);

            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load suppliers: {ex.Message}";
            }
        }
        private void RecalculateTotals()
        {
            SelectedItemCount = PayableItems.Count(p => p.IsSelected);

            TotalSelectedPayableAmount = PayableItems
                .Where(p => p.IsSelected)
                .Sum(p => p.PaymentAmount);

            Payment.PaidAmount = TotalSelectedPayableAmount;

            SavePaymentCommand.RaiseCanExecuteChanged();
        }

        protected override void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            base.OnPropertyChanged(propertyName);

            // This is a common pattern to recalculate a dependent property
            if (propertyName == nameof(PayableItems))
            {
                RecalculateTotals();
            }
        }

        // This method is called whenever IsSelected or PaymentAmount changes on a PayableItem
        private void PayableItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SupplierPayableItem.IsSelected) ||
                e.PropertyName == nameof(SupplierPayableItem.PaymentAmount))
            {
                RecalculateTotals();
            }
        }
        #endregion

        #region Methods
        private bool CanLoadPayables() => SelectedSupplier != null;
        private async Task LoadPayablesAsync()
        {
            if (SelectedSupplier == null) return;

            try
            {
                ErrorMessage = null;

                var result = await _supplierPaymentRepository.GetSupplierPayableAsync(SelectedSupplier.SupplierId);

                PayableItems.Clear();

                foreach (var item in result)
                {
                    var payableItem = new SupplierPayableItem
                    {
                        // Map properties from DTO to PayableItem
                        GoodsReceiveNoteId = item.GoodsReceiveNoteId,
                        InvoiceNumber = item.InvoiceNumber,
                        InvoiceDate = item.InvoiceDate,
                        DueDate = item.DueDate,
                        TotalAmount = item.TotalAmount,
                        PaidAmount = item.PaidAmount,
                        AmountDue = item.AmountDue,
                        Status = item.Status,
                        IsSelected = false,
                        PaymentAmount = item.AmountDue
                    };

                    // Attach the property changed handler to each new item
                    payableItem.PropertyChanged += PayableItem_PropertyChanged;
                    PayableItems.Add(payableItem);
                }
                RecalculateTotals();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load payables: {ex.Message}");
            }
        }

        private bool CanSavePayment()
        {
            // Must have selected a supplier, a payment method, and at least one item must have a payment amount entered
            return SelectedSupplier != null &&
                   SelectedPaymentMethod.HasValue &&
                   SelectedPaymentAccountId > 0 &&
                   PayableItems.Any(p => p.IsSelected && p.PaymentAmount > 0);
        }
        private async Task SavePaymentAsync()
        {
            if (!CanSavePayment()) return;

            try
            {
                Payment.BranchId = _userSessionService.BranchId;
                Payment.SupplierId = SelectedSupplier.SupplierId;
                Payment.PaymentDate = PaymentDate;
                Payment.PaymentMethod = SelectedPaymentMethod?.ToString();
                Payment.ReferenceNumber = null;
                Payment.PaymentAccountId = SelectedPaymentAccountId;
                Payment.PaidAmount = TotalSelectedPayableAmount;
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

                Payment.PaymentLines = PayableItems
                    .Where(r => r.IsSelected && r.PaymentAmount > 0)
                    .Select(r => new SupplierPaymentLine
                    {
                        SupplierPayableId = r.GoodsReceiveNoteId,
                        AmountApplied = r.PaymentAmount
                    })
                    .ToList();

                long newId = await _supplierPaymentRepository.CreateAsync(Payment);

                var printResult = MessageBox.Show(
                    "Payment successful. Do you want to print the payment voucher?",
                    "Payment Saved",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (printResult == MessageBoxResult.Yes)
                {
                    await OpenSupplierPaymentVoucherAsync(newId);
                }

                ClearForm();
                await LoadPayablesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while saving the payment: {ex.Message}");
            }
        }

        private async Task OpenSupplierPaymentVoucherAsync(long supplierPaymentId)
        {
            try
            {
                var reportDataSet = await _supplierPaymentRepository.GetSupplierPaymentVoucherDataSetAsync(supplierPaymentId);

                if (!HasSupplierPaymentVoucherData(reportDataSet))
                {
                    MessageBox.Show("No data found for this Supplier Payment Voucher.", "Report", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var reportPath = ResolveSupplierPaymentVoucherReportPath();
                var reportDocument = new ReportDocument();

                try
                {
                    reportDocument.Load(reportPath);
                    reportDocument.SetDataSource(reportDataSet);

                    var viewerWindow = new SupplierPaymentVoucherViewerWindow(reportDocument)
                    {
                        Owner = Application.Current?.MainWindow
                    };

                    viewerWindow.ShowDialog();
                    reportDocument = null;
                }
                finally
                {
                    if (reportDocument != null)
                    {
                        reportDocument.Close();
                        reportDocument.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Payment was saved, but the voucher could not be opened: {ex.Message}",
                    "Supplier Payment Voucher",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private static bool HasSupplierPaymentVoucherData(DataSet dataSet)
        {
            return dataSet != null &&
                   dataSet.Tables.Count > 0 &&
                   dataSet.Tables[0].Rows.Count > 0;
        }

        private static string ResolveSupplierPaymentVoucherReportPath()
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var candidatePaths = new[]
            {
                Path.Combine(baseDirectory, "Reports", "SupplierPaymentVoucher.rpt"),
                Path.Combine(baseDirectory, "SupplierPaymentVoucher.rpt"),
                Path.GetFullPath(Path.Combine(baseDirectory, @"..\..\Reports\SupplierPaymentVoucher.rpt"))
            };

            foreach (var candidatePath in candidatePaths)
            {
                if (File.Exists(candidatePath))
                {
                    return candidatePath;
                }
            }

            throw new FileNotFoundException(
                "Crystal report file not found. Expected SupplierPaymentVoucher.rpt under the application Reports folder.",
                candidatePaths[0]);
        }
        #endregion
    }
}
