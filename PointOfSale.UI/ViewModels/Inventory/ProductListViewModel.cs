
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Inventory;
using PointOfSale.Core.Interfaces.Services;
using PointOfSale.Core.Models.Inventory;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Inventory
{
    public class ProductListViewModel : BaseViewModel
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly IProductRepository _productRepository;
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IUserSessionService _userSessionService;
        private readonly IExcelService _excelService;
        public ProductListViewModel(
            ICategoryRepository categoryRepository,
            IProductRepository productRepository,
            IInventoryRepository inventoryRepository,
            IUserSessionService userSessionService,
            IExcelService excelService)
        {
            _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
            _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
            _inventoryRepository = inventoryRepository ?? throw new ArgumentNullException(nameof(inventoryRepository));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));
            _excelService = excelService ?? throw new ArgumentNullException(nameof(excelService));

            SearchProductCommand = new RelayCommand(async _ => await SearchProductsAsync());
            ExportToExcelCommand = new RelayCommand(_ => ExportToExcel(), _ => ProductList?.Any() == true);

            Categories = new ObservableCollection<CategoryLookupItem>();
            ItemTypes = new ObservableCollection<ItemTypeModel>();
            Locations = new ObservableCollection<Location>();

            // Load initial data
            _ = LoadDependanciesAsync();

        }

        #region Properties
        private ObservableCollection<Location> _locations;
        public ObservableCollection<Location> Locations
        {
            get => _locations;
            set => SetProperty(ref _locations, value);
        }

        private Location _selectedLocation;
        public Location SelectedLocation
        {
            get => _selectedLocation;
            set
            {
                if (SetProperty(ref _selectedLocation, value))
                {
                    // Trigger reload immediately when user changes branch
                    //_ = SearchProductsAsync();
                }
            }
        }

        private ObservableCollection<CategoryLookupItem> _categories;
        public ObservableCollection<CategoryLookupItem> Categories
        {
            get => _categories;
            set => SetProperty(ref _categories, value);
        }

        private ObservableCollection<ItemTypeModel> _itemTypes;
        public ObservableCollection<ItemTypeModel> ItemTypes
        {
            get => _itemTypes;
            set => SetProperty(ref _itemTypes, value);
        }

        private CategoryLookupItem _selectedCategory;
        public CategoryLookupItem SelectedCategory
        {
            get => _selectedCategory;
            set => SetProperty(ref _selectedCategory, value);
        }

        private ItemTypeModel _selectedItemType;
        public ItemTypeModel SelectedItemType
        {
            get => _selectedItemType;
            set => SetProperty(ref _selectedItemType, value);
        }

        private ObservableCollection<Product> _productList;
        public ObservableCollection<Product> ProductList
        {
            get => _productList;
            set
            {
                if (SetProperty(ref _productList, value))
                {
                    UpdateProductCounts();
                    ExportToExcelCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    SearchProductCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private int _totalProductCount;
        public int TotalProductCount
        {
            get => _totalProductCount;
            set => SetProperty(ref _totalProductCount, value);
        }

        private int _activeProductCount;
        public int ActiveProductCount
        {
            get => _activeProductCount;
            set => SetProperty(ref _activeProductCount, value);
        }

        private int _inactiveProductCount;
        public int InactiveProductCount
        {
            get => _inactiveProductCount;
            set => SetProperty(ref _inactiveProductCount, value);
        }

        private int _lowStockProductCount;
        public int LowStockProductCount
        {
            get => _lowStockProductCount;
            set => SetProperty(ref _lowStockProductCount, value);
        }

        #endregion

        #region Commands
        public RelayCommand SearchProductCommand { get; }
        public RelayCommand ExportToExcelCommand { get; }
        #endregion

        #region Command Methods
        private async Task SearchProductsAsync()
        {
            try
            {
                int? categoryId = SelectedCategory?.Id;

                if (categoryId == 0) categoryId = null;

                int? itemTypeId = SelectedItemType?.Id;

                if (itemTypeId == 0) itemTypeId = null;

                string searchTerm = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim();

                int branchId = SelectedLocation?.Id ?? 0;

                var products = await _productRepository.SearchProductWithFilterAsync(branchId, searchTerm, categoryId, itemTypeId);
                ProductList = new ObservableCollection<Product>(products);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to search products: {ex.Message}");
            }
            finally
            {
                // IsBusy = false;
            }
        }

        private void ExportToExcel()
        {
            try
            {
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel Files (*.xlsx)|*.xlsx",
                    Title = "Export Products to Excel",
                    FileName = $"Products_List_{DateTime.Now:yyyyMMdd}.xlsx"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    _excelService.ExportProducts(ProductList, saveFileDialog.FileName);
                    MessageBox.Show("Export completed successfully!", "Export Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "File Locked", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while exporting: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Helper Methods
        private void BuildHierarchy(IEnumerable<Category> allCategories, int parentId, int level)
        {
            // 1. Get children of the current parent
            var children = allCategories
                .Where(c => (c.ParentCategoryId ?? 0) == parentId)
                .OrderBy(c => c.Name);

            foreach (var cat in children)
            {
                // 2. Add to the list with the correct Level
                Categories.Add(new CategoryLookupItem
                {
                    Id = cat.CategoryId,
                    DisplayName = cat.Name, // Clean name (Visual indentation handled by XAML)
                    Level = level
                });

                // 3. Recursion: Find children of this category
                BuildHierarchy(allCategories, cat.CategoryId, level + 1);
            }
        }
        private async Task LoadDependanciesAsync()
        {
            try
            {
                var branchId = _userSessionService.BranchId;

                var locations = await _inventoryRepository.GetLocationsByBranchAsync(branchId);
                Locations = new ObservableCollection<Location>(locations);

                // Set Default Location (e.g., Main Branch or User's Default)
                if (Locations.Any())
                {
                    SelectedLocation = Locations.First(); // This will trigger the first SearchProductsAsync via Setter
                }

                var rawCategoryList = await _categoryRepository.GetAllAsync();

                if (rawCategoryList != null)
                {
                    Categories.Clear();

                    Categories.Add(new CategoryLookupItem { Id = 0, DisplayName = "ALL CATEGORIES", Level = 0 });

                    BuildHierarchy(rawCategoryList, 0, 0);
                }

                var rawItemTypeList = await _productRepository.GetItemTypesAsync();

                ItemTypes.Clear();
                ItemTypes.Add(new ItemTypeModel { Id = 0, TypeName = "ALL ITEM TYPES" });

                if (rawItemTypeList != null)
                {
                    foreach (var itemType in rawItemTypeList.OrderBy(i => i.TypeName))
                    {
                        ItemTypes.Add(itemType);
                    }
                }

                SelectedItemType = ItemTypes.FirstOrDefault();

            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load initial data: {ex.Message}";
            }
        }
        private void UpdateProductCounts()
        {
            if (ProductList == null)
            {
                TotalProductCount = 0;
                ActiveProductCount = 0;
                InactiveProductCount = 0;
                LowStockProductCount = 0;
                return;
            }

            TotalProductCount = ProductList.Count;
            ActiveProductCount = ProductList.Count(p => p.IsActive);
            InactiveProductCount = ProductList.Count(p => !p.IsActive);
            LowStockProductCount = ProductList.Count(p => p.AvailableQuantity <= p.ReorderPoint);
        }
        #endregion
    }
}
