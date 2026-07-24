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
using PointOfSale.Core.Interfaces.Repositories.Restaurant;
using PointOfSale.Core.Models.Restaurant;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Restaurant
{
    public class MenuCategoryViewModel : BaseViewModel
    {
        private readonly IMenuCategoryRepository _menuCategoryRepository;
        private readonly IAccountingRepository _accountingRepository;
        private readonly IUserSessionService _userSessionService;

        public MenuCategoryViewModel(
            IMenuCategoryRepository menuCategoryRepository,
            IAccountingRepository accountingRepository,
            IUserSessionService userSessionService)
        {
            _menuCategoryRepository = menuCategoryRepository;
            _accountingRepository = accountingRepository;
            _userSessionService = userSessionService;

            SaveCategoryCommand = new AsyncRelayCommand(async _ => await SaveMenuCategoryAsync(), _ => CanSaveCategory);
            LoadCategoryCommand = new AsyncRelayCommand(async _ => await LoadMenuCategoryAsync());
            EditCategoryCommand = new RelayCommand(_ => SetEditMode(true), _ => SelectedCategory != null);
            DeleteCategoryCommand = new AsyncRelayCommand(async _ => await DeleteMenuCategoryAsync(), _ => SelectedCategory != null);
            ErrorsChanged += (_, __) => RaiseCanExecuteChanged();

            // Load initial data
            _ = LoadInitialDataAsync();
        }

        #region Permissions
        public bool CanEditMenuCategory => _userSessionService.HasPermission("RESTAURANT_MENU_CATEGORY_EDIT");
        public bool CanDeleteMenuCategory => _userSessionService.HasPermission("RESTAURANT_MENU_CATEGORY_DELETE");
        #endregion

        #region Properties
        public ObservableCollection<AccountDto> ChartOfAccounts { get; }
            = new ObservableCollection<AccountDto>();

        public ObservableCollection<MenuCategory> CategoryList { get; } = new ObservableCollection<MenuCategory>();
        public ObservableCollection<CategoryLookupItem> ParentCategoryList { get; }
                = new ObservableCollection<CategoryLookupItem>();

        public ObservableCollection<CategoryTreeNode> CategoryTree { get; } = new ObservableCollection<CategoryTreeNode>();

        // Currently selected brand in DataGrid
        private MenuCategory _selectedCategory;
        public MenuCategory SelectedCategory
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

        private int _displayOrder;
        public int DisplayOrder
        {
            get => _displayOrder;
            set
            {
                if (SetProperty(ref _displayOrder, value))
                {
                    ValidateDisplayOrder();
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
                        IncomeAccountCode = null;

                    OnPropertyChanged(nameof(IsParentCategory));
                }
            }
        }

        private string _incomeAccountCode;
        public string IncomeAccountCode
        {
            get => _incomeAccountCode;
            set => SetProperty(ref _incomeAccountCode, value);
        }

        public bool IsParentCategory => (SelectedParentId ?? 0) == 0;

        private bool _isActive = true;
        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        public bool CanSaveCategory => !HasErrors &&
            !string.IsNullOrWhiteSpace(Name) &&
            DisplayOrder > 0;
        #endregion

        #region Commands
        public ICommand SaveCategoryCommand { get; }
        public ICommand LoadCategoryCommand { get; }
        public ICommand EditCategoryCommand { get; }
        public ICommand DeleteCategoryCommand { get; }
        #endregion

        #region CRUD Methods
        private async Task LoadInitialDataAsync()
        {
            await LoadChartOfAccountsAsync();
            await LoadMenuCategoryAsync();
        }

        private async Task LoadChartOfAccountsAsync()
        {
            try
            {
                var accounts = await _accountingRepository.GetAccountsAsync();

                ChartOfAccounts.Clear();

                foreach (var account in accounts.Where(a => a.AccountTypeId == 4 && !a.IsHeader && a.IsActive))
                    ChartOfAccounts.Add(account);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load chart of accounts: {ex.Message}";
            }
        }

        private async Task LoadMenuCategoryAsync()
        {
            try
            {
                var categories = await _menuCategoryRepository.GetAllAsync();

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

                SelectedParentId = 0;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load menu categories: {ex.Message}";
            }

        }
        private async Task SaveMenuCategoryAsync()
        {
            ValidateAll();

            if (IsEditing && (SelectedParentId ?? 0) == CategoryId)
            {
                MessageBox.Show("A category cannot be its own Parent.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (HasErrors) return;

            try
            {
                int? parentIdToSave = (SelectedParentId ?? 0) == 0 ? (int?)null : SelectedParentId;

                if (IsEditing && SelectedCategory != null)
                {
                    SelectedCategory.DisplayOrder = DisplayOrder;
                    SelectedCategory.Name = Name;
                    SelectedCategory.ParentId = parentIdToSave;
                    SelectedCategory.IsActive = IsActive;
                    SelectedCategory.IncomeAccountCode = parentIdToSave.HasValue ? null : IncomeAccountCode;

                    await _menuCategoryRepository.UpdateAsync(SelectedCategory);
                    MessageBox.Show("Category updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    var newCategory = new MenuCategory
                    {
                        DisplayOrder = DisplayOrder,
                        Name = Name,
                        ParentId = parentIdToSave,
                        IsActive = IsActive,
                        IncomeAccountCode = parentIdToSave.HasValue ? null : IncomeAccountCode
                    };
                    await _menuCategoryRepository.CreateAsync(newCategory);
                    MessageBox.Show("Category created successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                await LoadMenuCategoryAsync();

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
        private async Task DeleteMenuCategoryAsync()
        {
            if (!CanDeleteMenuCategory)
            {
                MessageBox.Show("You do not have permission to delete menu categories.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SelectedCategory == null) return;

            var result = MessageBox.Show(
                $"Are you sure you want to delete \"{SelectedCategory.Name}\"?\nThis action cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _menuCategoryRepository.DeleteAsync(SelectedCategory.Id);
                MessageBox.Show("Category deleted successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadMenuCategoryAsync();
                CreateNewCategory();
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Delete Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting category: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Form Helpers
        private void CreateNewCategory()
        {
            SelectedCategory = null;

            CategoryId = 0;
            DisplayOrder = 0;
            Name = string.Empty;
            SelectedParentId = 0;
            IncomeAccountCode = null;
            IsActive = true;

            ClearAllErrors();

            RaiseCanExecuteChanged();
        }
        private void SetEditMode(bool isEditing)
        {
            IsEditing = isEditing;

            if (isEditing && SelectedCategory != null)
            {
                CategoryId = SelectedCategory.Id;
                DisplayOrder = SelectedCategory.DisplayOrder;
                Name = SelectedCategory.Name;
                SelectedParentId = SelectedCategory.ParentId ?? 0;
                IncomeAccountCode = IsParentCategory ? SelectedCategory.IncomeAccountCode : null;
                IsActive = SelectedCategory.IsActive;
            }
        }
        private void RaiseCanExecuteChanged()
        {
            (SaveCategoryCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (EditCategoryCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (DeleteCategoryCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }

        private void BuildHierarchy(IEnumerable<MenuCategory> allCategories, int parentId, int level)
        {
            // Filter: Get children of the current parentId
            var children = allCategories
                .Where(c => (c.ParentId ?? 0) == parentId)
                .OrderBy(c => c.DisplayOrder);

            foreach (var cat in children)
            {

                ParentCategoryList.Add(new CategoryLookupItem
                {
                    Id = cat.Id,
                    DisplayName = cat.Name,
                    Level = level
                });

                // RECURSION: Call this function again for the children of THIS category
                BuildHierarchy(allCategories, cat.Id, level + 1);
            }
        }

        private void BuildTreeForView(IEnumerable<MenuCategory> allCategories)
        {
            CategoryTree.Clear();

            // Local function to recursively find children
            void AddChildren(CategoryTreeNode node, int parentId)
            {
                var children = allCategories.Where(c => c.ParentId == parentId).OrderBy(c => c.DisplayOrder);
                foreach (var child in children)
                {
                    var childNode = new CategoryTreeNode
                    {
                        Id = child.Id,
                        Name = child.Name
                    };

                    node.Children.Add(childNode);
                    AddChildren(childNode, child.Id); // Recursion
                }
            }

            // Find Roots (ParentId is NULL or 0)
            var roots = allCategories.Where(c => (c.ParentId ?? 0) == 0).OrderBy(c => c.DisplayOrder);

            foreach (var root in roots)
            {
                var rootNode = new CategoryTreeNode
                {
                    Id = root.Id,
                    Name = root.Name
                };

                // Find children for this root
                AddChildren(rootNode, root.Id);

                CategoryTree.Add(rootNode);
            }
        }
        #endregion

        #region Validation
        private void ValidateAll()
        {
            ValidateDisplayOrder();
            ValidateCategoryName();
        }

        private void ValidateDisplayOrder()
        {
            ClearErrors(nameof(DisplayOrder));

            if (DisplayOrder == 0)
                AddError(nameof(DisplayOrder), "Order can't be 0.");

        }
        private void ValidateCategoryName()
        {
            ClearErrors(nameof(Name));
            if (string.IsNullOrWhiteSpace(Name))
                AddError(nameof(Name), "Category name is required.");
            else if (!Regex.IsMatch(Name, @"^[a-zA-Z\&\s]+$"))
                AddError(nameof(Name), "Cannot contain numbers or special characters.");
        }

        #endregion
    }
}
