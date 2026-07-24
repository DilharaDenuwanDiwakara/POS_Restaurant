using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces;
using PointOfSale.Core.Interfaces.Repositories.Accounts;
using PointOfSale.Core.Models.Accounts.Entities;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Accounts
{
    public class ExpensesCategoryViewModel : BaseViewModel
    {
        private readonly IExpensesCategoryRepository _expensesCategoryRepository;
        private readonly IAccountingRepository _accountingRepository;

        public ExpensesCategoryViewModel(IExpensesCategoryRepository expensesCategoryRepository, IAccountingRepository accountingRepository)
        {
            _expensesCategoryRepository = expensesCategoryRepository ?? throw new ArgumentNullException(nameof(expensesCategoryRepository));
            _accountingRepository = accountingRepository ?? throw new ArgumentNullException(nameof(accountingRepository));

            // Initialize commands
            SaveExpensesCategoryCommand = new AsyncRelayCommand(async param => await SaveExpensesCategoryAsync(param), _ => CanSaveExpensesCategory);
            EditExpensesCategoryCommand = new RelayCommand(_ => SetEditMode(true), _ => SelectedExpensesCategory != null);
            DeleteExpensesCategoryCommand = new AsyncRelayCommand(async _ => await DeleteExpensesCategoryAsync(), _ => SelectedExpensesCategory != null);
            LoadExpensesCategoryCommand = new AsyncRelayCommand(async _ => await LoadExpensesCategoryAsync());

            _ = LoadExpensesCategoryAsync();
            _ = LoadGLExpenseAccountsAsync();
        }

        #region Properties
        public ObservableCollection<ExpensesCategory> ExpensesCategoryList { get; } = new ObservableCollection<ExpensesCategory>();

        public ObservableCollection<AccountDto> GLExpenseAccounts { get; } = new ObservableCollection<AccountDto>();

        private AccountDto _selectedGLExpenseAccount;
        public AccountDto SelectedGLExpenseAccount
        {
            get => _selectedGLExpenseAccount;
            set
            {
                if (SetProperty(ref _selectedGLExpenseAccount, value))
                {
                    RaiseCanExecuteChanged();
                }
            }
        }

        public List<ExpensesCategory> AddedCategories { get; private set; } = new List<ExpensesCategory>();

        public ExpensesCategory ResultData { get; private set; }

        private ExpensesCategory _selectedExpensesCategory;
        public ExpensesCategory SelectedExpensesCategory
        {
            get => _selectedExpensesCategory;
            set
            {
                if (SetProperty(ref _selectedExpensesCategory, value))
                {
                    SetEditMode(false);
                    RaiseCanExecuteChanged();
                }
            }
        }

        private bool _isEditing;
        public bool IsEditing
        {
            get => _isEditing;
            set
            {
                SetProperty(ref _isEditing, value);
                OnPropertyChanged(nameof(SaveButtonText));
            }
        }
        public string SaveButtonText => IsEditing ? "Update" : "Save";

        private int _expensesCategoryId;
        public int ExpensesCategoryId
        {
            get => _expensesCategoryId;
            set => SetProperty(ref _expensesCategoryId, value);
        }

        private string _expensesCategoryName;
        public string ExpensesCategoryName
        {
            get => _expensesCategoryName;
            set
            {
                SetProperty(ref _expensesCategoryName, value);
                ValidateExpensesCategoryName();
                RaiseCanExecuteChanged();
            }
        }
        #endregion

        #region Commands
        public ICommand SaveExpensesCategoryCommand { get; }
        public ICommand EditExpensesCategoryCommand { get; }
        public ICommand DeleteExpensesCategoryCommand { get; }
        public ICommand LoadExpensesCategoryCommand { get; }
        #endregion

        #region CRUD Methods
        private async Task LoadExpensesCategoryAsync()
        {
            try
            {
                ExpensesCategoryList.Clear();
                var categories = await _expensesCategoryRepository.GetAllAsync();

                foreach (var c in categories)
                    ExpensesCategoryList.Add(c);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load expense categories: {ex.Message}";
            }
        }

        private async Task LoadGLExpenseAccountsAsync()
        {
            try
            {
                GLExpenseAccounts.Clear();
                var accounts = await _accountingRepository.GetAccountsAsync();

                foreach (var account in accounts.Where(a => a.AccountTypeId == 5 && !a.IsHeader && a.IsActive))
                    GLExpenseAccounts.Add(account);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load GL expense accounts: {ex.Message}";
            }
        }

        private async Task SaveExpensesCategoryAsync(object parameter)
        {
            var window = parameter as Window;

            ValidateAll();

            if (HasErrors) return;

            if (SelectedGLExpenseAccount == null)
            {
                MessageBox.Show("Select the GL account this category should map to.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (IsEditing && SelectedExpensesCategory != null)
                {
                    SelectedExpensesCategory.ExpensesCategoryName = ExpensesCategoryName;
                    SelectedExpensesCategory.AccountId = SelectedGLExpenseAccount.Id;

                    await _expensesCategoryRepository.UpdateAsync(SelectedExpensesCategory);

                    MessageBox.Show("Expenses category updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    ResultData = SelectedExpensesCategory;
                }
                else
                {
                    var newCategory = new ExpensesCategory
                    {
                        ExpensesCategoryName = ExpensesCategoryName,
                        AccountId = SelectedGLExpenseAccount.Id
                    };
                    int newId = await _expensesCategoryRepository.CreateAsync(newCategory);
                    newCategory.ExpensesCategoryId = newId;

                    MessageBox.Show("Expenses category created successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                    AddedCategories.Add(newCategory);
                }

                await LoadExpensesCategoryAsync();
                CreateNewExpensesCategory();

            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving expenses category: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task DeleteExpensesCategoryAsync()
        {
            if (SelectedExpensesCategory == null) return;

            try
            {
                var result = MessageBox.Show(
                    "Are you sure you want to delete?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    await _expensesCategoryRepository.DeleteAsync(SelectedExpensesCategory.ExpensesCategoryId);
                    ErrorMessage = "Expenses category deleted successfully.";
                    await LoadExpensesCategoryAsync();
                    CreateNewExpensesCategory();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error deleting expenses category: {ex.Message}";
            }
        }
        #endregion

        #region Helper Methods
        private void CreateNewExpensesCategory()
        {
            SelectedExpensesCategory = null;

            ExpensesCategoryId = 0;
            ExpensesCategoryName = string.Empty;
            SelectedGLExpenseAccount = null;

            ClearErrors(nameof(ExpensesCategoryName));

            ErrorMessage = null;

            RaiseCanExecuteChanged();
        }

        private void SetEditMode(bool isEditing)
        {
            IsEditing = isEditing;

            if (isEditing && SelectedExpensesCategory != null)
            {
                ExpensesCategoryId = SelectedExpensesCategory.ExpensesCategoryId;
                ExpensesCategoryName = SelectedExpensesCategory.ExpensesCategoryName;
                SelectedGLExpenseAccount = GLExpenseAccounts.FirstOrDefault(a => a.Id == SelectedExpensesCategory.AccountId);
            }
        }

        private void RaiseCanExecuteChanged()
        {
            (SaveExpensesCategoryCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (EditExpensesCategoryCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (DeleteExpensesCategoryCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }

        public bool CanSaveExpensesCategory =>
            !HasErrors &&
            !string.IsNullOrWhiteSpace(ExpensesCategoryName) &&
            SelectedGLExpenseAccount != null;
        #endregion

        #region Validation
        private void ValidateAll()
        {
            ValidateExpensesCategoryName();
        }

        private void ValidateExpensesCategoryName()
        {
            ClearErrors(nameof(ExpensesCategoryName));
            if (string.IsNullOrWhiteSpace(ExpensesCategoryName))
                AddError(nameof(ExpensesCategoryName), "Category name is required.");
            else if (!Regex.IsMatch(ExpensesCategoryName, @"^[a-zA-Z0-9\s]+$"))
                AddError(nameof(ExpensesCategoryName), "Cannot contain special character");
        }
        #endregion
    }
}
