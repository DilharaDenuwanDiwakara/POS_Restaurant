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
using PointOfSale.Core.Interfaces.Repositories.Inventory;
using PointOfSale.Core.Models.Inventory;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Inventory
{
    public class CategoryViewModel : BaseViewModel
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly IAccountingRepository _accountingRepository;

        public CategoryViewModel(
            ICategoryRepository categoryRepository,
            IAccountingRepository accountingRepository)
        {
            _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
            _accountingRepository = accountingRepository ?? throw new ArgumentNullException(nameof(accountingRepository));

            // Initialize Commands
            SaveCategoryCommand = new AsyncRelayCommand(async _ => await SaveCategoryAsync(), _ => CanSaveCategory);
            LoadCategoryCommand = new AsyncRelayCommand(async _ => await LoadCategoryAsync());
            EditCategoryCommand = new RelayCommand(_ => SetEditMode(true), _ => SelectedCategory != null);
            DeleteCategoryCommand = new AsyncRelayCommand(async _ => await DeleteCategoryAsync(), _ => SelectedCategory != null);
            ErrorsChanged += (_, __) => RaiseCanExecuteChanged();

            _ = LoadInitialDataAsync();
        }

        #region Properties
        public ObservableCollection<AccountDto> ChartOfAccounts { get; }
            = new ObservableCollection<AccountDto>();

        public ObservableCollection<AccountDto> StockChartOfAccounts { get; }
            = new ObservableCollection<AccountDto>();

        public ObservableCollection<AccountDto> CostOfSalesChartOfAccounts { get; }
            = new ObservableCollection<AccountDto>();

        public ObservableCollection<Category> CategoryList { get; } = new ObservableCollection<Category>();
        public ObservableCollection<CategoryLookupItem> ParentCategoryList { get; }
                = new ObservableCollection<CategoryLookupItem>();

        public ObservableCollection<CategoryTreeNode> CategoryTree { get; } = new ObservableCollection<CategoryTreeNode>();

        // Currently selected brand in DataGrid
        private Category _selectedCategory;
        public Category SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                {
                    SetEditMode(false);
                    RaiseCanExecuteChanged();
                }
            }
        }

        public bool _isEditing;
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

        private int _categoryId;
        public int CategoryId
        {
            get => _categoryId;
            set => SetProperty(ref _categoryId, value);
        }

        private string _code;
        public string Code
        {
            get => _code;
            set
            {
                if (SetProperty(ref _code, value))
                {
                    ValidateCategoryCode();
                    RaiseCanExecuteChanged();
                }
            }
        }

        private string _name;
        public string Name
        {
            get => _name;
            set
            {
                SetProperty(ref _name, value);
                ValidateCategoryName();
                RaiseCanExecuteChanged();
            }
        }

        private int? _selectedParentId;
        public int? SelectedParentId
        {
            get => _selectedParentId;
            set
            {
                if (SetProperty(ref _selectedParentId, value))
                {
                    if (!IsParentCategory)
                    {
                        StockAccountCode = null;
                        CostOfSalesAccountCode = null;
                    }

                    OnPropertyChanged(nameof(IsParentCategory));
                }
            }
        }

        private string _stockAccountCode;
        public string StockAccountCode
        {
            get => _stockAccountCode;
            set => SetProperty(ref _stockAccountCode, value);
        }

        private string _costOfSalesAccountCode;
        public string CostOfSalesAccountCode
        {
            get => _costOfSalesAccountCode;
            set => SetProperty(ref _costOfSalesAccountCode, value);
        }

        public bool IsParentCategory => (SelectedParentId ?? 0) == 0;

        public bool CanSaveCategory => !HasErrors &&
            !string.IsNullOrWhiteSpace(Code) &&
            !string.IsNullOrWhiteSpace(Name);
        #endregion

        #region Commands
        public ICommand SaveCategoryCommand { get; }
        public ICommand LoadCategoryCommand { get; }
        public ICommand NewCategoryCommand { get; }
        public ICommand EditCategoryCommand { get; }
        public ICommand DeleteCategoryCommand { get; }
        #endregion

        #region CRUD Methods
        private async Task LoadInitialDataAsync()
        {
            await LoadChartOfAccountsAsync();
            await LoadCategoryAsync();
        }

        private async Task LoadChartOfAccountsAsync()
        {
            try
            {
                var accounts = await _accountingRepository.GetAccountsAsync();
                var postableAccounts = accounts.Where(a => !a.IsHeader && a.IsActive).ToList();

                ChartOfAccounts.Clear();
                StockChartOfAccounts.Clear();
                CostOfSalesChartOfAccounts.Clear();

                foreach (var account in postableAccounts.Where(a => a.AccountTypeId == 1))
                {
                    ChartOfAccounts.Add(account);
                    StockChartOfAccounts.Add(account);
                }

                foreach (var account in postableAccounts.Where(a => a.AccountTypeId == 5))
                    CostOfSalesChartOfAccounts.Add(account);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load chart of accounts: {ex.Message}";
            }
        }

        private async Task LoadCategoryAsync()
        {
            try
            {
                var categories = await _categoryRepository.GetAllAsync();

                CategoryList.Clear();
                ParentCategoryList.Clear();

                foreach (var category in categories)
                    CategoryList.Add(category);

                ParentCategoryList.Add(new CategoryLookupItem
                {
                    Id = 0,
                    DisplayName = "< NO PARENT >"
                });

                BuildHierarchy(categories, 0, 0);

                BuildTreeForView(categories);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load categories: {ex.Message}";
            }

        }
        private async Task SaveCategoryAsync()
        {
            ValidateAll();

            if (IsEditing && (SelectedParentId ?? 0) == CategoryId)
            {
                MessageBox.Show("A category cannot be its own Parent.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (HasErrors) return;

            int? parentIdToKeep = SelectedParentId;

            try
            {
                int? parentIdToSave = (SelectedParentId ?? 0) == 0 ? (int?)null : SelectedParentId;

                if (IsEditing && SelectedCategory != null)
                {
                    SelectedCategory.Code = Code;
                    SelectedCategory.Name = Name;
                    SelectedCategory.ParentCategoryId = parentIdToSave;
                    SelectedCategory.StockAccountCode = parentIdToSave.HasValue ? null : StockAccountCode;
                    SelectedCategory.CostOfSalesAccountCode = parentIdToSave.HasValue ? null : CostOfSalesAccountCode;

                    await _categoryRepository.UpdateAsync(SelectedCategory);
                    MessageBox.Show("Category updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    var newCategory = new Category
                    {
                        Code = Code,
                        Name = Name,
                        ParentCategoryId = parentIdToSave,
                        StockAccountCode = parentIdToSave.HasValue ? null : StockAccountCode,
                        CostOfSalesAccountCode = parentIdToSave.HasValue ? null : CostOfSalesAccountCode
                    };
                    await _categoryRepository.CreateAsync(newCategory);
                    MessageBox.Show("Category created successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                await LoadCategoryAsync();

                SelectedParentId = parentIdToKeep;

                CreateNewCategory();
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving category: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async Task DeleteCategoryAsync()
        {
            if (SelectedCategory == null) return;

            try
            {
                // Show confirmation dialog
                var result = MessageBox.Show(
                    "Are you sure you want to delete?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    await _categoryRepository.DeleteAsync(SelectedCategory.CategoryId);
                    MessageBox.Show("Category Deleted successfully", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                    await LoadCategoryAsync();
                    CreateNewCategory();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting category: {ex.Message}");
            }
        }
        #endregion

        #region Form Helpers
        private void CreateNewCategory()
        {
            SelectedCategory = null;

            CategoryId = 0;
            Code = string.Empty;
            Name = string.Empty;
            SelectedParentId = 0;
            StockAccountCode = null;
            CostOfSalesAccountCode = null;

            ClearAllErrors();

            RaiseCanExecuteChanged();
        }
        private void SetEditMode(bool isEditing)
        {
            IsEditing = isEditing;

            if (isEditing && SelectedCategory != null)
            {
                CategoryId = SelectedCategory.CategoryId;
                Code = SelectedCategory.Code;
                Name = SelectedCategory.Name;
                SelectedParentId = SelectedCategory.ParentCategoryId ?? 0;
                StockAccountCode = IsParentCategory ? SelectedCategory.StockAccountCode : null;
                CostOfSalesAccountCode = IsParentCategory ? SelectedCategory.CostOfSalesAccountCode : null;
            }
        }
        private void RaiseCanExecuteChanged()
        {
            (SaveCategoryCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (EditCategoryCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (DeleteCategoryCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }

        private void BuildHierarchy(IEnumerable<Category> allCategories, int parentId, int level)
        {
            // Filter: Get children of the current parentId
            var children = allCategories
                .Where(c => (c.ParentCategoryId ?? 0) == parentId)
                .OrderBy(c => c.Name);

            foreach (var cat in children)
            {

                ParentCategoryList.Add(new CategoryLookupItem
                {
                    Id = cat.CategoryId,
                    DisplayName = cat.Name,
                    Level = level
                });

                // RECURSION: Call this function again for the children of THIS category
                BuildHierarchy(allCategories, cat.CategoryId, level + 1);
            }
        }

        private void BuildTreeForView(IEnumerable<Category> allCategories)
        {
            CategoryTree.Clear();

            // Local function to recursively find children
            void AddChildren(CategoryTreeNode node, int parentId)
            {
                var children = allCategories.Where(c => c.ParentCategoryId == parentId).OrderBy(c => c.Name);
                foreach (var child in children)
                {
                    var childNode = new CategoryTreeNode
                    {
                        Id = child.CategoryId,
                        Name = child.Name,
                        Code = child.Code
                    };

                    node.Children.Add(childNode);
                    AddChildren(childNode, child.CategoryId); // Recursion
                }
            }

            // Find Roots (ParentId is NULL or 0)
            var roots = allCategories.Where(c => (c.ParentCategoryId ?? 0) == 0).OrderBy(c => c.Name);

            foreach (var root in roots)
            {
                var rootNode = new CategoryTreeNode
                {
                    Id = root.CategoryId,
                    Name = root.Name,
                    Code = root.Code
                };

                // Find children for this root
                AddChildren(rootNode, root.CategoryId);

                CategoryTree.Add(rootNode);
            }
        }
        #endregion

        #region Validation
        private void ValidateAll()
        {
            ValidateCategoryCode();
            ValidateCategoryName();
        }

        private void ValidateCategoryCode()
        {
            ClearErrors(nameof(Code));

            var code = Code?.Trim();
            if (string.IsNullOrWhiteSpace(code))
                AddError(nameof(Code), "Code is required.");
            else if (!Regex.IsMatch(code, @"^[a-zA-Z0-9]+$"))
                AddError(nameof(Code), "Code can contain letters and numbers only.");
        }
        private void ValidateCategoryName()
        {
            ClearErrors(nameof(Name));
            if (string.IsNullOrWhiteSpace(Name))
                AddError(nameof(Name), "Category name is required.");
            else if (!Regex.IsMatch(Name, @"^[a-zA-Z&denu\s]+$"))
                AddError(nameof(Name), "Cannot contain numbers or special characters.");
        }

        #endregion
    }
}
