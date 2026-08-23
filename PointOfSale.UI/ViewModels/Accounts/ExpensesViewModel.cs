using System;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CrystalDecisions.CrystalReports.Engine;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces;
using PointOfSale.Core.Interfaces.Repositories.Accounts;
using PointOfSale.Core.Interfaces.Services;
using PointOfSale.Core.Models.Accounts.Entities;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;
using PointOfSale.UI.Views.Sales;

namespace PointOfSale.UI.ViewModels.Accounts
{
    public class ExpensesViewModel : BaseViewModel
    {
        private readonly IExpensesRepository _expensesRepository;
        private readonly IExpensesCategoryRepository _expensesCategoryRepository;
        private readonly IAccountingRepository _accountingRepository;
        private readonly IUserSessionService _userSessionService;
        private readonly IDialogService _dialogService;

        public ExpensesViewModel(IExpensesRepository expensesRepository,
            IDialogService dialogService,
            IExpensesCategoryRepository expensesCategoryRepository,
            IAccountingRepository accountingRepository,
            IUserSessionService userSessionService)
        {
            _expensesRepository = expensesRepository ?? throw new ArgumentNullException(nameof(expensesRepository));
            _expensesCategoryRepository = expensesCategoryRepository;
            _accountingRepository = accountingRepository ?? throw new ArgumentNullException(nameof(accountingRepository));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

            ExpensesList = new ObservableCollection<Expenses>();
            ExpensesCategories = new ObservableCollection<ExpensesCategory>();
            PaymentAccounts = new ObservableCollection<AccountDto>();

            SaveExpenseCommand = new AsyncRelayCommand(async _ => await SaveExpenseAsync());
            LoadExpensesCommand = new AsyncRelayCommand(async _ => await LoadExpensesAsync());
            SearchExpensesCommand = new AsyncRelayCommand(async _ => await LoadExpensesAsync());
            PrintExpenseCommand = new AsyncRelayCommand(async parameter => await PrintExpenseAsync(parameter));
            NewExpenseCommand = new RelayCommand(_ => CreateNewExpense());

            _ = LoadExpensesAsync();
            _ = LoadExpensesCategoryAsync();
            _ = LoadPaymentAccountsAsync();

            _dialogService = dialogService;

        }

        public ObservableCollection<Expenses> ExpensesList { get; }

        private ObservableCollection<ExpensesCategory> _expensesCategories;
        public ObservableCollection<ExpensesCategory> ExpensesCategories
        {
            get => _expensesCategories;
            set => SetProperty(ref _expensesCategories, value);
        }

        private Expenses _selectedExpense;
        public Expenses SelectedExpense
        {
            get => _selectedExpense;
            set => SetProperty(ref _selectedExpense, value);
        }

        private ExpensesCategory _selectedExeCategory;
        public ExpensesCategory SelectedExeCategory
        {
            get => _selectedExeCategory;
            set
            {
                if (SetProperty(ref _selectedExeCategory, value))
                {
                    ValidateExpensesCategory();
                    // Force button re-evaluation
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public ObservableCollection<AccountDto> PaymentAccounts { get; }

        private AccountDto _selectedPaymentAccount;
        public AccountDto SelectedPaymentAccount
        {
            get => _selectedPaymentAccount;
            set
            {
                if (SetProperty(ref _selectedPaymentAccount, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private DateTime _expensesDate = DateTime.Now;
        public DateTime ExpensesDate
        {
            get => _expensesDate;
            set
            {
                SetProperty(ref _expensesDate, value);
            }
        }

        private DateTime _fromDate = DateTime.Today;
        public DateTime FromDate
        {
            get => _fromDate;
            set
            {
                if (SetProperty(ref _fromDate, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private DateTime _toDate = DateTime.Today;
        public DateTime ToDate
        {
            get => _toDate;
            set
            {
                if (SetProperty(ref _toDate, value))
                {
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

        private string _description;
        public string Description
        {
            get => _description;
            set
            {
                if (SetProperty(ref _description, value))
                {
                    ValidateDescription();
                }
            }
        }

        private bool _isOverlayVisible;
        public bool IsOverlayVisible
        {
            get { return _isOverlayVisible; }
            set
            {
                _isOverlayVisible = value;
                OnPropertyChanged(nameof(IsOverlayVisible));
            }
        }

        #region Command
        public ICommand SaveExpenseCommand { get; }
        public ICommand LoadExpensesCommand { get; }
        public ICommand SearchExpensesCommand { get; }
        public ICommand PrintExpenseCommand { get; }
        public ICommand NewExpenseCommand { get; }
        public ICommand OpenAddExpensesCategoryCommand => new RelayCommand(ExecuteOpenAddCategories);

        #endregion

        private bool CanSaveExpense(object _)
        {
            return !HasErrors &&
                    Amount > 0m &&
                    SelectedExeCategory != null;
        }
        private void CreateNewExpense()
        {
            ExpensesDate = DateTime.Now;
            SelectedExeCategory = null;
            Amount = 0m;
            Description = string.Empty;
            ClearAllErrors();
            SelectedExpense = null;
        }

        private async void ExecuteOpenAddCategories(object parameter)
        {
            try
            {
                IsOverlayVisible = true;

                if (_dialogService == null)
                {
                    MessageBox.Show("Dialog Service is not initialized.");
                    return;
                }

                _dialogService.ShowDialog<ExpensesCategoryViewModel>(out var expenseCategoryVm);

                if (expenseCategoryVm.ExpensesCategoryList != null && expenseCategoryVm.ExpensesCategoryList.Count > 0)
                {
                    // Loop through the accumulated list and add them to the ComboBox source
                    foreach (var expensesCategory in expenseCategoryVm.ExpensesCategoryList)
                    {
                        // Check if it already exists to be safe (optional)
                        if (!ExpensesCategories.Any(b => b.ExpensesCategoryId == expensesCategory.ExpensesCategoryId))
                        {
                            ExpensesCategories.Add(expensesCategory);
                        }
                    }
                    SelectedExeCategory = expenseCategoryVm.ExpensesCategoryList.Last();
                }
                else
                {

                    await LoadExpensesCategoryAsync();
                }
            }
            finally
            {
                IsOverlayVisible = false;
            }
        }

        #region Command Implementation
        private async Task LoadExpensesAsync()
        {
            try
            {
                ExpensesList.Clear();
                var currentBranchId = _userSessionService.BranchId;

                if (ToDate.Date < FromDate.Date)
                {
                    MessageBox.Show("To Date cannot be earlier than From Date.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var items = await _expensesRepository.GetAllAsync(currentBranchId, FromDate, ToDate);
                if (items == null) return;

                foreach (var item in items)
                {
                    ExpensesList.Add(item);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load expenses: {ex.Message}";
            }
        }
        private async Task LoadExpensesCategoryAsync()
        {
            try
            {
                var expensesCategories = await _expensesCategoryRepository.GetAllAsync();
                ExpensesCategories = new ObservableCollection<ExpensesCategory>(expensesCategories);

            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load expenses category: {ex.Message}";
            }
        }
        private async Task LoadPaymentAccountsAsync()
        {
            try
            {
                var accounts = await _accountingRepository.GetAccountsAsync();
                PaymentAccounts.Clear();
                foreach (var account in accounts.Where(a => a.AccountTypeId == 1 && !a.IsHeader && a.IsActive))
                {
                    PaymentAccounts.Add(account);
                }
                SelectedPaymentAccount = PaymentAccounts.FirstOrDefault();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load payment accounts: {ex.Message}";
            }
        }
        private async Task SaveExpenseAsync()
        {
            try
            {
                ValidateAll();

                if (HasErrors || SelectedExeCategory == null)
                {
                    MessageBox.Show("Please correct the errors before saving.");
                    return;
                }

                if (SelectedPaymentAccount == null)
                {
                    MessageBox.Show("Select the account this expense was paid from.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var expense = new Expenses
                {
                    ExpensesDate = ExpensesDate,
                    ExpensesCategoryId = SelectedExeCategory.ExpensesCategoryId,
                    PaymentAccountId = SelectedPaymentAccount.Id,
                    Amount = Amount,
                    Description = Description,
                    CreatedBy = _userSessionService.UserId,
                    LocationId = _userSessionService.BranchId

                };

                var expensesId = await _expensesRepository.CreateAsync(expense);
                MessageBox.Show("Expenses saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                await OpenExpenseVoucherAsync(expensesId);

                await LoadExpensesAsync();
                CreateNewExpense();

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving: {ex.Message}");
            }
        }

        private async Task PrintExpenseAsync(object parameter)
        {
            var expense = parameter as Expenses;
            if (expense == null || expense.ExpensesId <= 0)
            {
                return;
            }

            try
            {
                await OpenExpenseVoucherAsync(expense.ExpensesId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to open Expense Voucher preview: {ex.Message}", "Expense Voucher", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task OpenExpenseVoucherAsync(int expensesId)
        {
            try
            {
                DataTable reportData = await _expensesRepository.GetExpenseVoucherAsync(expensesId);

                if (reportData == null || reportData.Rows.Count == 0)
                {
                    MessageBox.Show(
                        "No data found for this Expense Voucher.",
                        "Expense Voucher",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ReportDocument reportDocument = null;

                    try
                    {
                        reportDocument = new ReportDocument();
                        reportDocument.Load(ResolveExpenseVoucherReportPath());
                        reportDocument.SetDataSource(reportData);

                        var previewWindow = new ZReportViewerWindow(reportDocument, disposeReportOnClose: true)
                        {
                            Title = "Expense Payment Voucher"
                        };

                        var owner = Application.Current.MainWindow;
                        if (owner != null && owner != previewWindow)
                        {
                            previewWindow.Owner = owner;
                            previewWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                        }
                        else
                        {
                            previewWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                        }

                        previewWindow.ShowDialog();
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
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Expense was saved, but the voucher could not be opened: {ex.Message}",
                    "Expense Voucher",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private static string ResolveExpenseVoucherReportPath()
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var candidatePaths = new[]
            {
                Path.Combine(baseDirectory, "Reports", "ExpenseVoucher.rpt"),
                Path.Combine(baseDirectory, "ExpenseVoucher.rpt"),
                Path.GetFullPath(Path.Combine(baseDirectory, @"..\..\Reports\ExpenseVoucher.rpt"))
            };

            foreach (var candidatePath in candidatePaths)
            {
                if (File.Exists(candidatePath))
                {
                    return candidatePath;
                }
            }

            throw new FileNotFoundException(
                "Crystal report file not found. Expected ExpenseVoucher.rpt under the application Reports folder.",
                candidatePaths[0]);
        }
        #endregion

        #region Validation
        private void ValidateAll()
        {
            ValidateAmount();
            ValidateExpensesCategory();
            ValidateDescription();
        }
        private void ValidateAmount()
        {
            ClearErrors(nameof(Amount));
            if (Amount <= 0m)
            {
                AddError(nameof(Amount), "Enter valid amount.");
            }
        }
        private void ValidateExpensesCategory()
        {
            ClearErrors(nameof(SelectedExeCategory));
            if (SelectedExeCategory == null)
                AddError(nameof(ExpensesCategory), "Please select a valid expense category.");
        }
        private void ValidateDescription()
        {
            ClearErrors(nameof(Description));
            if (!string.IsNullOrWhiteSpace(Description) && Description.Length > 500)
                AddError(nameof(Description), "Description cannot exceed 500 characters.");
            else if (!string.IsNullOrWhiteSpace(Description) && !Regex.IsMatch(Description, @"^[a-zA-Z0-9\s\-\(\)]+$"))
                AddError(nameof(Description), "Cannot contain special character");
        }
        #endregion
    }
}
