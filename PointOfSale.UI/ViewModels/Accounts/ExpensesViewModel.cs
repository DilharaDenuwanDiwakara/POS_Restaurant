using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces;
using PointOfSale.Core.Interfaces.Repositories.Accounts;
using PointOfSale.Core.Interfaces.Services;
using PointOfSale.Core.Models.Accounts.Entities;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;

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

                var items = await _expensesRepository.GetAllAsync(currentBranchId);
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

                await _expensesRepository.CreateAsync(expense);
                MessageBox.Show("Expenses saved succesfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                await LoadExpensesAsync();
                CreateNewExpense();

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving: {ex.Message}");
            }
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
            else if (!string.IsNullOrWhiteSpace(Description) && !Regex.IsMatch(Description, @"^[a-zA-Z0-9\s]+$"))
                AddError(nameof(Description), "Cannot contain special character");
        }
        #endregion
    }
}
