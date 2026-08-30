using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Enums;
using PointOfSale.Core.Interfaces;
using PointOfSale.Core.Interfaces.Purchasing;
using PointOfSale.Core.Interfaces.Repositories.Accounts;
using PointOfSale.Core.Models.Accounts;
using PointOfSale.Core.Models.Accounts.DTOs;
using PointOfSale.Core.Models.Accounts.Entities;
using PointOfSale.Core.Models.Purchasing;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Accounts
{
    public class SupplierSettlementViewModel : BaseViewModel
    {
        private readonly ISupplierRepository _supplierRepository;
        private readonly ISupplierPaymentRepository _supplierPaymentRepository;
        private readonly ISupplierCreditRepository _supplierCreditRepository;
        private readonly IUserSessionService _userSessionService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IAccountingRepository _accountingRepository;
        public SupplierSettlementViewModel(ISupplierRepository supplierRepository,
                                            ISupplierPaymentRepository supplierPaymentRepository,
                                            ISupplierCreditRepository supplierCreditRepository,
                                            IUserSessionService userSessionService,
                                            IServiceProvider serviceProvider,
                                            IAccountingRepository accountingRepository)
        {
            _supplierRepository = supplierRepository;
            _supplierPaymentRepository = supplierPaymentRepository;
            _supplierCreditRepository = supplierCreditRepository;
            _userSessionService = userSessionService;
            _serviceProvider = serviceProvider;
            _accountingRepository = accountingRepository ?? throw new ArgumentNullException(nameof(accountingRepository));

            Suppliers = new ObservableCollection<Supplier>();
            PayableItems = new ObservableCollection<SupplierPayableItem>();
            CreditItems = new ObservableCollection<SupplierCreditItem>();
            PaymentAccounts = new ObservableCollection<AccountDto>();
            PaymentMethods = new ObservableCollection<AccountsPaymentMethod>((AccountsPaymentMethod[])Enum.GetValues(typeof(AccountsPaymentMethod)));

            LoadPayablesCommand = new AsyncRelayCommand(_ => LoadPayablesAsync(), _ => CanLoadPayables());
            LoadCreditsCommand = new AsyncRelayCommand(_ => LoadCreditsAsync(), _ => CanLoadCredits());
            SavePaymentCommand = new AsyncRelayCommand(_ => SavePaymentAsync());
            ClearSelectionCommand = new RelayCommand(_ => ClearSelection());

            _ = LoadSuppliersAsync();
            _ = LoadPaymentAccountsAsync();
        }

        public ObservableCollection<Supplier> Suppliers { get; }
        public ObservableCollection<AccountsPaymentMethod> PaymentMethods { get; }
        public ObservableCollection<AccountDto> PaymentAccounts { get; }
        public ObservableCollection<SupplierPayableItem> PayableItems { get; }
        public ObservableCollection<SupplierCreditItem> CreditItems { get; }

        #region Properties
        private Supplier _selectedSupplier;
        public Supplier SelectedSupplier
        {
            get => _selectedSupplier;
            set
            {
                if (SetProperty(ref _selectedSupplier, value))
                {
                    PayableItems.Clear();
                    LoadPayablesCommand.RaiseCanExecuteChanged();
                    LoadCreditsCommand.RaiseCanExecuteChanged();
                }
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
            set => SetProperty(ref _selectedPaymentMethod, value);
        }

        private int _selectedPaymentAccountId;
        public int SelectedPaymentAccountId
        {
            get => _selectedPaymentAccountId;
            set => SetProperty(ref _selectedPaymentAccountId, value);
        }

        private decimal _totalSelectedPayableAmount;
        public decimal TotalSelectedPayableAmount
        {
            get => _totalSelectedPayableAmount;
            set => SetProperty(ref _totalSelectedPayableAmount, value);
        }

        private decimal _totalSelectedCreditAmount;
        public decimal TotalSelectedCreditAmount
        {
            get => _totalSelectedCreditAmount;
            set => SetProperty(ref _totalSelectedCreditAmount, value);
        }

        private decimal _netPaymentAmount;
        public decimal NetPaymentAmount
        {
            get => _netPaymentAmount;
            set => SetProperty(ref _netPaymentAmount, value);
        }

        private int _selectedPayableCount;
        public int SelectedPayableCount
        {
            get => _selectedPayableCount;
            set => SetProperty(ref _selectedPayableCount, value);
        }

        private int _selectedCreditCount;
        public int SelectedCreditCount
        {
            get => _selectedCreditCount;
            set => SetProperty(ref _selectedCreditCount, value);
        }
        #endregion

        #region Commands
        public AsyncRelayCommand LoadPayablesCommand { get; }
        public AsyncRelayCommand LoadCreditsCommand { get; }
        public RelayCommand ClearSelectionCommand { get; }
        public AsyncRelayCommand SavePaymentCommand { get; }
        #endregion

        #region Helper Methods
        private async Task LoadSuppliersAsync()
        {
            try
            {
                var result = await _supplierRepository.GetAllAsync();

                foreach (var supplier in result)
                {
                    Suppliers.Add(supplier);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load suppliers: {ex.Message}";
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
        private void RecalculateTotals()
        {
            SelectedPayableCount = PayableItems.Count(p => p.IsSelected);
            TotalSelectedPayableAmount = PayableItems
                .Where(p => p.IsSelected)
                .Sum(p => p.PaymentAmount);

            // Calculate Credits
            SelectedCreditCount = CreditItems.Count(c => c.IsSelected);
            TotalSelectedCreditAmount = CreditItems
                .Where(c => c.IsSelected)
                .Sum(c => c.ApplyAmount);

            // Calculate Net Payment (Cash Required)
            // FIX: Clamp to 0. If Credits > Payables, we just need 0 cash.
            decimal rawNet = TotalSelectedPayableAmount - TotalSelectedCreditAmount;
            NetPaymentAmount = rawNet < 0 ? 0 : rawNet;
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
        private void CreditItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SupplierCreditItem.IsSelected) ||
                e.PropertyName == nameof(SupplierCreditItem.ApplyAmount))
            {
                RecalculateTotals();
            }
        }
        private void ClearSelection()
        {
            foreach (var item in PayableItems)
            {
                item.IsSelected = false;
                item.PaymentAmount = item.AmountDue;
            }

            foreach (var credit in CreditItems)
            {
                credit.IsSelected = false;
                credit.ApplyAmount = 0m;
            }

            RecalculateTotals();
        }
        #endregion

        #region Command Implementation
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

                await LoadCreditsAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load payables: {ex.Message}";
            }
        }

        private bool CanLoadCredits() => SelectedSupplier != null;
        private async Task LoadCreditsAsync()
        {
            if (SelectedSupplier == null) return;

            try
            {
                ErrorMessage = null;

                var result = await _supplierCreditRepository.GetAvailableCreditsAsync(SelectedSupplier.SupplierId);

                CreditItems.Clear();

                foreach (var credit in result)
                {
                    var creditItem = new SupplierCreditItem
                    {
                        SourceId = credit.SourceId,
                        SourceType = credit.SourceType,
                        ReferenceNumber = credit.ReferenceNumber,
                        CreditDate = credit.CreditDate,
                        CreditAmount = credit.CreditAmount,
                        RemainingAmount = credit.RemainingAmount,
                        ApplyAmount = 0m,
                        IsSelected = false
                    };

                    creditItem.PropertyChanged += CreditItem_PropertyChanged;
                    CreditItems.Add(creditItem);
                }

                RecalculateTotals();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load credits: {ex.Message}";
            }
        }

        private async Task SavePaymentAsync()
        {
            // 1. VALIDATION
            if (SelectedSupplier == null) return;

            if (NetPaymentAmount > 0 && SelectedPaymentMethod == null)
            {
                MessageBox.Show("Please select a Payment Method for the cash balance.", "Validation Error");
                return;
            }
            if (NetPaymentAmount > 0 && SelectedPaymentAccountId <= 0)
            {
                MessageBox.Show("Please select the account to pay from.", "Validation Error");
                return;
            }
            if (TotalSelectedPayableAmount <= 0)
            {
                MessageBox.Show("Please select at least one bill to pay.", "Validation Error");
                return;
            }

            try
            {
                var billsToPay = PayableItems.Where(p => p.IsSelected && p.PaymentAmount > 0).ToList();
                var creditsToUse = CreditItems.Where(c => c.IsSelected && c.ApplyAmount > 0).ToList();

                // 1. Prepare Lists for Bulk Insert
                var settlementList = new List<SupplierSettlement>();
                var paymentLineList = new List<SupplierPaymentLine>();

                // --- LOGIC START (In Memory Only) ---

                foreach (var bill in billsToPay)
                {
                    bill.RemainingToProcess = bill.PaymentAmount;
                }

                // Phase 1: Calculate Settlements
                foreach (var credit in creditsToUse)
                {
                    decimal creditBalance = credit.ApplyAmount;

                    foreach (var bill in billsToPay)
                    {
                        // If this credit note is empty, move to next credit note
                        if (creditBalance <= 0) break;

                        // If this bill is fully paid, move to next bill
                        if (bill.RemainingToProcess <= 0) continue;

                        // Take ONLY what is needed. 
                        // If Bill needs 120 and Credit has 200, we take 120.
                        decimal amountToSettle = Math.Min(creditBalance, bill.RemainingToProcess);

                        settlementList.Add(new SupplierSettlement
                        {
                            PayableId = bill.GoodsReceiveNoteId,
                            CreditId = credit.SourceId,
                            SettlementAmount = amountToSettle
                        });

                        creditBalance -= amountToSettle;
                        bill.RemainingToProcess -= amountToSettle;
                    }
                }

                // Phase 2: Calculate Cash Lines
                // Since we clamped NetPaymentAmount to 0 in RecalculateTotals, 
                // this block simply won't run if credits covered everything.
                if (NetPaymentAmount > 0)
                {
                    foreach (var bill in billsToPay)
                    {
                        if (bill.RemainingToProcess > 0)
                        {
                            paymentLineList.Add(new SupplierPaymentLine
                            {
                                SupplierPayableId = bill.GoodsReceiveNoteId,
                                AmountApplied = bill.RemainingToProcess
                            });
                        }
                    }
                }

                // --- LOGIC END ---

                await _supplierPaymentRepository.ProcessBulkPaymentAsync(
                    SelectedSupplier.SupplierId,
                    SelectedPaymentMethod?.ToString(),
                    PaymentDate,
                    NetPaymentAmount,
                    SelectedPaymentAccountId,
                    _userSessionService.UserId,
                    settlementList,
                    paymentLineList
                );

                MessageBox.Show("Payment saved successfully!", "Success");
                ClearSelection();
                await LoadPayablesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving payment: {ex.Message}");
            }
        }
        #endregion
    }
}
