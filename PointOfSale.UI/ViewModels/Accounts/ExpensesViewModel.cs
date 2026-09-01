using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces;
using PointOfSale.Core.Interfaces.Repositories.Accounts;
using PointOfSale.Core.Interfaces.Repositories.System;
using PointOfSale.Core.Interfaces.Services;
using PointOfSale.Core.Models.Accounts.Entities;
using PointOfSale.Core.Models.System;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;
using PointOfSale.UI.Reports;
using PointOfSale.UI.Views.Sales;

namespace PointOfSale.UI.ViewModels.Accounts
{
    public class ExpensesViewModel : BaseViewModel
    {
        private readonly IExpensesRepository _expensesRepository;
        private readonly IAccountingRepository _accountingRepository;
        private readonly IBranchRepository _branchRepository;
        private readonly IUserSessionService _userSessionService;

        public ExpensesViewModel(
            IExpensesRepository expensesRepository,
            IDialogService dialogService,
            IExpensesCategoryRepository expensesCategoryRepository,
            IAccountingRepository accountingRepository,
            IBranchRepository branchRepository,
            IUserSessionService userSessionService)
        {
            _expensesRepository = expensesRepository ?? throw new ArgumentNullException(nameof(expensesRepository));
            _accountingRepository = accountingRepository ?? throw new ArgumentNullException(nameof(accountingRepository));
            _branchRepository = branchRepository ?? throw new ArgumentNullException(nameof(branchRepository));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));

            ExpensesList = new ObservableCollection<Expenses>();
            Branches = new ObservableCollection<Branch>();
            PaymentAccounts = new ObservableCollection<AccountDto>();
            GLExpenseAccounts = new ObservableCollection<AccountDto>();
            ExpenseLines = new ObservableCollection<ExpenseLineModel>();
            ExpenseLines.CollectionChanged += ExpenseLinesCollectionChanged;

            SaveExpenseCommand = new AsyncRelayCommand(async _ => await SaveExpenseAsync());
            LoadExpensesCommand = new AsyncRelayCommand(async _ => await LoadExpensesAsync());
            SearchExpensesCommand = new AsyncRelayCommand(async _ => await LoadExpensesAsync());
            PrintExpenseCommand = new AsyncRelayCommand(async parameter => await PrintExpenseAsync(parameter));
            NewExpenseCommand = new RelayCommand(_ => CreateNewExpense());
            AddLineCommand = new RelayCommand(_ => AddLine());
            RemoveLineCommand = new RelayCommand(RemoveLine, parameter => parameter is ExpenseLineModel);

            _ = LoadBranchesAsync();
            _ = LoadExpensesAsync();
            _ = LoadPaymentAccountsAsync();
            _ = LoadGLExpenseAccountsAsync();
        }

        public ObservableCollection<Expenses> ExpensesList { get; }
        public ObservableCollection<Branch> Branches { get; }
        public ObservableCollection<AccountDto> PaymentAccounts { get; }
        public ObservableCollection<AccountDto> GLExpenseAccounts { get; }
        public ObservableCollection<ExpenseLineModel> ExpenseLines { get; }

        private Expenses _selectedExpense;
        public Expenses SelectedExpense
        {
            get => _selectedExpense;
            set => SetProperty(ref _selectedExpense, value);
        }

        private Branch _selectedBranch;
        public Branch SelectedBranch
        {
            get => _selectedBranch;
            set
            {
                if (SetProperty(ref _selectedBranch, value))
                {
                    ValidateBranch();
                }
            }
        }

        private AccountDto _selectedPaymentAccount;
        public AccountDto SelectedPaymentAccount
        {
            get => _selectedPaymentAccount;
            set
            {
                if (SetProperty(ref _selectedPaymentAccount, value))
                {
                    ValidatePaymentAccount();
                }
            }
        }

        private DateTime _expensesDate = DateTime.Now;
        public DateTime ExpensesDate
        {
            get => _expensesDate;
            set => SetProperty(ref _expensesDate, value);
        }

        private DateTime _fromDate = DateTime.Today;
        public DateTime FromDate
        {
            get => _fromDate;
            set => SetProperty(ref _fromDate, value);
        }

        private DateTime _toDate = DateTime.Today;
        public DateTime ToDate
        {
            get => _toDate;
            set => SetProperty(ref _toDate, value);
        }

        private decimal _totalAmount;
        public decimal TotalAmount
        {
            get => _totalAmount;
            private set => SetProperty(ref _totalAmount, value);
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

        private int _selectedTabIndex;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetProperty(ref _selectedTabIndex, value);
        }

        private AccountDto _selectedLineAccount;
        public AccountDto SelectedLineAccount
        {
            get => _selectedLineAccount;
            set => SetProperty(ref _selectedLineAccount, value);
        }

        private string _lineDescription;
        public string LineDescription
        {
            get => _lineDescription;
            set => SetProperty(ref _lineDescription, value);
        }

        private decimal _lineAmount;
        public decimal LineAmount
        {
            get => _lineAmount;
            set => SetProperty(ref _lineAmount, value);
        }

        public ICommand SaveExpenseCommand { get; }
        public ICommand LoadExpensesCommand { get; }
        public ICommand SearchExpensesCommand { get; }
        public ICommand PrintExpenseCommand { get; }
        public ICommand NewExpenseCommand { get; }
        public ICommand AddLineCommand { get; }
        public ICommand RemoveLineCommand { get; }

        private void CreateNewExpense()
        {
            ExpensesDate = DateTime.Now;
            Description = string.Empty;
            SelectedPaymentAccount = PaymentAccounts.FirstOrDefault();
            SelectedBranch = Branches.FirstOrDefault(b => b.Id == _userSessionService.BranchId) ?? Branches.FirstOrDefault();
            ExpenseLines.Clear();
            ClearLineEntry();
            ClearAllErrors();
            SelectedExpense = null;
        }

        private void AddLine()
        {
            if (SelectedLineAccount == null)
            {
                MessageBox.Show("Select the GL account for this expense line.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (LineAmount <= 0m)
            {
                MessageBox.Show("Enter a valid line amount.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!string.IsNullOrWhiteSpace(LineDescription) && LineDescription.Length > 500)
            {
                MessageBox.Show("Line description cannot exceed 500 characters.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!string.IsNullOrWhiteSpace(LineDescription) && !Regex.IsMatch(LineDescription, @"^[a-zA-Z0-9\s\-\(\)]+$"))
            {
                MessageBox.Show("Line description cannot contain special character.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ExpenseLines.Add(new ExpenseLineModel
            {
                AccountId = SelectedLineAccount.Id,
                AccountDisplayText = SelectedLineAccount.DisplayText,
                Description = LineDescription,
                Amount = LineAmount
            });

            ClearLineEntry();
            ValidateLines();
        }

        private void RemoveLine(object parameter)
        {
            var line = parameter as ExpenseLineModel;
            if (line == null)
            {
                return;
            }

            ExpenseLines.Remove(line);
            ValidateLines();
        }

        private void ClearLineEntry()
        {
            SelectedLineAccount = null;
            LineDescription = string.Empty;
            LineAmount = 0m;
        }

        private async Task LoadBranchesAsync()
        {
            try
            {
                Branches.Clear();
                var branches = await _branchRepository.GetAllAsync();
                foreach (var branch in branches.Where(b => b.IsActive))
                {
                    Branches.Add(branch);
                }

                SelectedBranch = Branches.FirstOrDefault(b => b.Id == _userSessionService.BranchId) ?? Branches.FirstOrDefault();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load branches: {ex.Message}";
            }
        }

        private async Task LoadExpensesAsync()
        {
            try
            {
                ExpensesList.Clear();

                if (ToDate.Date < FromDate.Date)
                {
                    MessageBox.Show("To Date cannot be earlier than From Date.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var branchId = SelectedBranch?.Id ?? _userSessionService.BranchId;
                var items = await _expensesRepository.GetAllAsync(branchId, FromDate, ToDate);
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

                SelectedPaymentAccount = PaymentAccounts.FirstOrDefault();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load payment accounts: {ex.Message}";
            }
        }

        private async Task LoadGLExpenseAccountsAsync()
        {
            try
            {
                GLExpenseAccounts.Clear();

                var accountTypes = await _accountingRepository.GetAccountTypesAsync();
                var expenseTypeIds = new HashSet<int>(
                    accountTypes
                        .Where(t => string.Equals(t.Name, "Expense", StringComparison.OrdinalIgnoreCase)
                                    || string.Equals(t.Name, "Expenses", StringComparison.OrdinalIgnoreCase))
                        .Select(t => t.AccountTypeId));

                if (!expenseTypeIds.Any())
                {
                    expenseTypeIds.Add(5);
                }

                var accounts = await _accountingRepository.GetAccountsAsync();
                foreach (var account in accounts.Where(a => expenseTypeIds.Contains(a.AccountTypeId) && !a.IsHeader && a.IsActive))
                {
                    GLExpenseAccounts.Add(account);
                }

                SelectedLineAccount = GLExpenseAccounts.FirstOrDefault();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load GL expense accounts: {ex.Message}";
            }
        }

        private async Task SaveExpenseAsync()
        {
            try
            {
                ValidateAll();

                if (HasErrors)
                {
                    MessageBox.Show("Please correct the errors before saving.");
                    return;
                }

                var expense = new ExpenseHeader
                {
                    ExpensesDate = ExpensesDate,
                    BranchId = SelectedBranch.Id,
                    PaymentAccountId = SelectedPaymentAccount.Id,
                    Amount = TotalAmount,
                    Description = Description,
                    CreatedBy = _userSessionService.UserId,
                    Lines = ExpenseLines.Select(line => new ExpenseLine
                    {
                        AccountId = line.AccountId,
                        Amount = line.Amount,
                        Description = line.Description
                    }).ToList()
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
                    MessageBox.Show("No data found for this Expense Voucher.", "Expense Voucher", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ExpenseVoucher reportDocument = null;

                    try
                    {
                        reportData.TableName = "uspGetExpenseVoucher";
                        reportDocument = new ExpenseVoucher();
                        reportDocument.SetDataSource(reportData);
                        TrySetReportParameter(reportDocument, "ExpensesId", expensesId);

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

        private static void TrySetReportParameter(ExpenseVoucher reportDocument, string parameterName, object value)
        {
            try
            {
                reportDocument.SetParameterValue(parameterName, value);
            }
            catch
            {
                // The report may be fully data-source bound and not expose the parameter at runtime.
            }
        }

        private void ExpenseLinesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (ExpenseLineModel line in e.OldItems)
                {
                    line.PropertyChanged -= ExpenseLinePropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (ExpenseLineModel line in e.NewItems)
                {
                    line.PropertyChanged += ExpenseLinePropertyChanged;
                }
            }

            CalculateTotalAmount();
            ValidateLines();
            (RemoveLineCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private void ExpenseLinePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ExpenseLineModel.Amount)
                || e.PropertyName == nameof(ExpenseLineModel.AccountId)
                || e.PropertyName == nameof(ExpenseLineModel.Description))
            {
                CalculateTotalAmount();
                ValidateLines();
            }
        }

        private void CalculateTotalAmount()
        {
            TotalAmount = ExpenseLines.Sum(line => line.Amount);
        }

        private void ValidateAll()
        {
            ValidateBranch();
            ValidatePaymentAccount();
            ValidateDescription();
            ValidateLines();
        }

        private void ValidateBranch()
        {
            ClearErrors(nameof(SelectedBranch));
            if (SelectedBranch == null)
            {
                AddError(nameof(SelectedBranch), "Please select a branch.");
            }
        }

        private void ValidatePaymentAccount()
        {
            ClearErrors(nameof(SelectedPaymentAccount));
            if (SelectedPaymentAccount == null)
            {
                AddError(nameof(SelectedPaymentAccount), "Please select the account to pay from.");
            }
        }

        private void ValidateDescription()
        {
            ClearErrors(nameof(Description));
            if (!string.IsNullOrWhiteSpace(Description) && Description.Length > 500)
            {
                AddError(nameof(Description), "Description cannot exceed 500 characters.");
            }
            else if (!string.IsNullOrWhiteSpace(Description) && !Regex.IsMatch(Description, @"^[a-zA-Z0-9\s\-\(\)]+$"))
            {
                AddError(nameof(Description), "Cannot contain special character");
            }
        }

        private void ValidateLines()
        {
            ClearErrors(nameof(ExpenseLines));

            if (!ExpenseLines.Any())
            {
                AddError(nameof(ExpenseLines), "Add at least one expense line.");
                return;
            }

            if (ExpenseLines.Any(line => line.AccountId <= 0))
            {
                AddError(nameof(ExpenseLines), "Select a GL account for every line.");
            }

            if (ExpenseLines.Any(line => line.Amount <= 0m))
            {
                AddError(nameof(ExpenseLines), "Enter a valid amount for every line.");
            }

            if (ExpenseLines.Any(line => !string.IsNullOrWhiteSpace(line.Description) && line.Description.Length > 500))
            {
                AddError(nameof(ExpenseLines), "Line description cannot exceed 500 characters.");
            }

            if (ExpenseLines.Any(line => !string.IsNullOrWhiteSpace(line.Description)
                                        && !Regex.IsMatch(line.Description, @"^[a-zA-Z0-9\s\-\(\)]+$")))
            {
                AddError(nameof(ExpenseLines), "Line description cannot contain special character.");
            }
        }
    }

    public class ExpenseLineModel : INotifyPropertyChanged
    {
        private int _accountId;
        private decimal _amount;
        private string _description;
        private string _accountDisplayText;

        public event PropertyChangedEventHandler PropertyChanged;

        public int AccountId
        {
            get => _accountId;
            set => SetProperty(ref _accountId, value);
        }

        public string AccountDisplayText
        {
            get => _accountDisplayText;
            set => SetProperty(ref _accountDisplayText, value);
        }

        public decimal Amount
        {
            get => _amount;
            set => SetProperty(ref _amount, value);
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}
