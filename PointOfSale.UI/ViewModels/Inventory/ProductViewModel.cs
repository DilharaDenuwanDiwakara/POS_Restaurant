using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using PointOfSale.Core.Enums;
using PointOfSale.Core.Interfaces.Repositories.Inventory;
using PointOfSale.Core.Interfaces.Services;
using PointOfSale.Core.Models.Inventory;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Inventory
{
    public class ProductViewModel : BaseViewModel
    {
        private readonly IProductRepository _productRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IUnitMeasureRepository _unitMeasureRepository;
        private readonly IUserSessionService _userSessionService;

        private readonly IDialogService _dialogService;

        public ProductViewModel(IDialogService dialogService,
                IProductRepository productRepository,
                ICategoryRepository categoryRepository,
                IUnitMeasureRepository unitMeasureRepository,
                IUserSessionService userSessionService)
        {
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
            _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
            _unitMeasureRepository = unitMeasureRepository ?? throw new ArgumentNullException(nameof(unitMeasureRepository));
            _userSessionService = userSessionService;

            PopulateItemTypes();

            AddUnitMeasureCommand = new RelayCommand(ExecuteOpenAddUnitMeasure);
            SaveProductCommand = new AsyncRelayCommand(async _ => await SaveProductAsync(), _ => CanSaveProduct);
            NewProductCommand = new RelayCommand(_ => CreateNewProduct());
            SearchProductCommand = new AsyncRelayCommand(async _ => await SearchProduct(), _ => CanSearchProduct);
            EditProductCommand = new RelayCommand(_ => SetEditMode(true), _ => SelectedProduct != null);
            DeleteProductCommand = new AsyncRelayCommand(async _ => await DeleteProductAsync(), _ => SelectedProduct != null);
            AddUnitConversionCommand = new RelayCommand(_ => AddUnitConversion(), _ => CanAddUnitConversion);
            RemoveUnitConversionCommand = new RelayCommand(RemoveUnitConversion, parameter => parameter is ProductUnitConversion || SelectedUnitConversion != null);

            _ = LoadDependanciesAsync();
        }

        #region Properties
        public ObservableCollection<Product> ProductList { get; } = new ObservableCollection<Product>();
        public ObservableCollection<KeyValuePair<ItemType, string>> ItemTypes { get; } =
            new ObservableCollection<KeyValuePair<ItemType, string>>();
        public ObservableCollection<ProductUnitConversion> UnitConversions { get; } =
            new ObservableCollection<ProductUnitConversion>();

        private ObservableCollection<Category> _categories;
        public ObservableCollection<Category> Categories
        {
            get => _categories;
            private set => SetProperty(ref _categories, value);
        }

        private Category _selectedCategory;
        public Category SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                SetProperty(ref _selectedCategory, value);
                ValidateCategory();
                RaiseCanExecuteChanged();
            }
        }

        private ObservableCollection<UnitMeasure> _unitMeasures;
        public ObservableCollection<UnitMeasure> UnitMeasures
        {
            get => _unitMeasures;
            private set
            {
                if (SetProperty(ref _unitMeasures, value))
                {
                    SyncSelectedUnitMeasure();
                }
            }
        }

        private UnitMeasure _selectedUnitMeasure;
        public UnitMeasure SelectedUnitMeasure
        {
            get => _selectedUnitMeasure;
            set
            {
                SetProperty(ref _selectedUnitMeasure, value);
                if (value != null)
                {
                    UnitMeasureId = value.UnitMeasureId;
                }
                ValidateUnitMeasure();
                (AddUnitConversionCommand as RelayCommand)?.RaiseCanExecuteChanged();
                RaiseCanExecuteChanged();
            }
        }

        private UnitMeasure _selectedTargetUnitMeasure;
        public UnitMeasure SelectedTargetUnitMeasure
        {
            get => _selectedTargetUnitMeasure;
            set
            {
                if (SetProperty(ref _selectedTargetUnitMeasure, value))
                {
                    (AddUnitConversionCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        private decimal _conversionRate;
        public decimal ConversionRate
        {
            get => _conversionRate;
            set
            {
                if (SetProperty(ref _conversionRate, value))
                {
                    (AddUnitConversionCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        private ProductUnitConversion _selectedUnitConversion;
        public ProductUnitConversion SelectedUnitConversion
        {
            get => _selectedUnitConversion;
            set
            {
                if (SetProperty(ref _selectedUnitConversion, value))
                {
                    (RemoveUnitConversionCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        private Product _selectedProduct;
        public Product SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                if (SetProperty(ref _selectedProduct, value))
                {
                    SetEditMode(false);
                    LoadUnitConversionsFromProduct(value);
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

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    (SearchProductCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        private int _productId;
        public int ProductId
        {
            get => _productId;
            set => SetProperty(ref _productId, value);
        }

        private string _barcode;
        public string Barcode
        {
            get => _barcode;
            set
            {
                if (SetProperty(ref _barcode, value))
                {
                    ValidateBarcode();
                }
            }
        }

        private string _productName;
        public string ProductName
        {
            get => _productName;
            set
            {
                SetProperty(ref _productName, value);
                ValidateProductName();
                RaiseCanExecuteChanged();
            }
        }

        private int _unitMeasureId;
        public int UnitMeasureId
        {
            get => _unitMeasureId;
            set
            {
                if (SetProperty(ref _unitMeasureId, value))
                {
                    SyncSelectedUnitMeasure();
                    ValidateUnitMeasure();
                    RaiseCanExecuteChanged();
                }
            }
        }

        public string BaseUnitMeasureName => SelectedUnitMeasure?.UnitMeasureName ?? "BASE";

        private ItemType _itemTypeId = ItemType.RAW_MATERIAL;
        public ItemType ItemTypeId
        {
            get => _itemTypeId;
            set
            {
                if (SetProperty(ref _itemTypeId, value))
                    RaiseCanExecuteChanged();
            }
        }

        private decimal _reorderPoint;
        public decimal ReorderPoint
        {
            get => _reorderPoint;
            set
            {
                SetProperty(ref _reorderPoint, value);
                ValidateReorderPoint();
                RaiseCanExecuteChanged();
            }
        }

        private decimal _maxStockQuantity;
        public decimal MaxStockQuantity
        {
            get => _maxStockQuantity;
            set => SetProperty(ref _maxStockQuantity, value);
        }

        private decimal _additionalStockQuantity;
        public decimal AdditionalStockQuantity
        {
            get => _additionalStockQuantity;
            set => SetProperty(ref _additionalStockQuantity, value);
        }

        private decimal _wastagePercentage;
        public decimal WastagePercentage
        {
            get => _wastagePercentage;
            set => SetProperty(ref _wastagePercentage, value);
        }

        private bool _isPurchasable = true;
        public bool IsPurchasable
        {
            get => _isPurchasable;
            set => SetProperty(ref _isPurchasable, value);
        }

        private bool _isActive = true;
        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        private bool _isTaxApplicable;
        public bool IsTaxApplicable
        {
            get => _isTaxApplicable;
            set => SetProperty(ref _isTaxApplicable, value);
        }

        private bool _trackExpiry;
        public bool TrackExpiry
        {
            get => _trackExpiry;
            set => SetProperty(ref _trackExpiry, value);
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
        #endregion

        #region Commands
        public ICommand SaveProductCommand { get; }
        public ICommand NewProductCommand { get; }
        public ICommand SearchProductCommand { get; }
        public ICommand EditProductCommand { get; }
        public ICommand DeleteProductCommand { get; }
        public ICommand AddUnitMeasureCommand { get; }
        public ICommand AddUnitConversionCommand { get; }
        public ICommand RemoveUnitConversionCommand { get; }
        #endregion

        #region CRUD Methods
        private async Task LoadDependanciesAsync()
        {
            try
            {
                var categoryList = await _categoryRepository.GetAllAsync();

                var parentIds = categoryList
                    .Where(c => c.ParentCategoryId.HasValue)
                    .Select(c => c.ParentCategoryId.Value)
                    .Distinct()
                    .ToHashSet();

                var leafCategories = categoryList
                    .Where(c => !parentIds.Contains(c.CategoryId))
                    .OrderBy(c => c.Name);

                Categories = new ObservableCollection<Category>(leafCategories);

                await LoadUnitMeasureAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load initial data: {ex.Message}";
            }
        }

        private async Task LoadUnitMeasureAsync()
        {
            try
            {
                var unitMeasureList = await _unitMeasureRepository.GetAllAsync();
                UnitMeasures = new ObservableCollection<UnitMeasure>(unitMeasureList);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load unit measures: {ex.Message}";
            }
        }

        public bool CanSaveProduct => !HasErrors &&
            !string.IsNullOrWhiteSpace(ProductName) &&
            (SelectedCategory != null);
        private async Task SaveProductAsync()
        {
            ValidateAll();
            try
            {
                if (IsEditing && SelectedProduct != null)
                {
                    SelectedProduct.CategoryId = SelectedCategory?.CategoryId ?? 0;
                    SelectedProduct.Barcode = string.IsNullOrWhiteSpace(Barcode) ? null : Barcode.Trim();
                    SelectedProduct.ProductName = ProductName.Trim();
                    SelectedProduct.UnitMeasureId = UnitMeasureId;
                    SelectedProduct.ItemTypeId = (int)ItemTypeId;
                    SelectedProduct.ReorderPoint = ReorderPoint;
                    SelectedProduct.MaxStockQuantity = MaxStockQuantity;
                    SelectedProduct.AdditionalStockQuantity = AdditionalStockQuantity;
                    SelectedProduct.WastagePercentage = WastagePercentage;

                    SelectedProduct.IsPurchasable = IsPurchasable;
                    SelectedProduct.IsActive = IsActive;
                    SelectedProduct.IsTaxApplicable = IsTaxApplicable;
                    SelectedProduct.TrackExpiry = TrackExpiry;
                    SelectedProduct.UnitConversions = UnitConversions.ToList();

                    SelectedProduct.UpdatedBy = _userSessionService?.UserId;

                    await _productRepository.UpdateAsync(SelectedProduct);
                    MessageBox.Show("Product updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    var newProduct = new Product
                    {
                        CategoryId = SelectedCategory?.CategoryId ?? 0,
                        Barcode = string.IsNullOrWhiteSpace(Barcode) ? null : Barcode.Trim(),
                        ProductName = ProductName.Trim(),
                        UnitMeasureId = UnitMeasureId,
                        ItemTypeId = (int)ItemTypeId,
                        ReorderPoint = ReorderPoint,
                        MaxStockQuantity = MaxStockQuantity,
                        AdditionalStockQuantity = AdditionalStockQuantity,
                        WastagePercentage = WastagePercentage,
                        IsPurchasable = IsPurchasable,
                        IsActive = IsActive,
                        IsTaxApplicable = IsTaxApplicable,
                        TrackExpiry = TrackExpiry,
                        CreatedBy = _userSessionService?.UserId,
                        UnitConversions = UnitConversions.ToList()
                    };
                    await _productRepository.CreateAsync(newProduct);
                    MessageBox.Show("Product created successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                CreateNewProduct(true);
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving product: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public bool CanSearchProduct => !string.IsNullOrWhiteSpace(SearchText);
        private async Task SearchProduct()
        {
            try
            {
                ProductList.Clear();
                var result = await _productRepository.SearchProductAsync(SearchText);

                foreach (var product in result)
                {
                    product.UnitConversions = (await _productRepository.GetUnitConversionsAsync(product.ProductId)).ToList();
                    ProductList.Add(product);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error searching product: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task DeleteProductAsync()
        {
            if (SelectedProduct == null) return;

            try
            {
                var result = MessageBox.Show(
                    "Are you sure you want to delete?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    await _productRepository.DeleteAsync(SelectedProduct.ProductId);
                    MessageBox.Show("Product deleted successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    CreateNewProduct();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error deleting product: {ex.Message}";
            }
        }
        #endregion

        #region FormHelpers
        private void PopulateItemTypes()
        {
            ItemTypes.Clear();
            ItemTypes.Add(new KeyValuePair<ItemType, string>(ItemType.RAW_MATERIAL, "RAW_MATERIAL"));
            ItemTypes.Add(new KeyValuePair<ItemType, string>(ItemType.FINISHED_GOODS, "FINISHED_GOODS"));
            ItemTypes.Add(new KeyValuePair<ItemType, string>(ItemType.CONSUMABLE, "CONSUMABLE"));
            ItemTypes.Add(new KeyValuePair<ItemType, string>(ItemType.SERVICE, "SERVICE"));
        }

        private async void ExecuteOpenAddUnitMeasure(object parameter)
        {
            try
            {
                IsOverlayVisible = true;

                if (_dialogService == null)
                {
                    MessageBox.Show("Dialog Service is not initialized.");
                    return;
                }

                _dialogService.ShowDialog<UnitMeasureViewModel>(out var unitMeasureVm);

                if (unitMeasureVm == null)
                {
                    return;
                }

                if (unitMeasureVm.AddedUnitMeasures != null && unitMeasureVm.AddedUnitMeasures.Count > 0)
                {
                    foreach (var unitMeasure in unitMeasureVm.AddedUnitMeasures)
                    {
                        if (!UnitMeasures.Any(u => u.UnitMeasureId == unitMeasure.UnitMeasureId))
                        {
                            UnitMeasures.Add(unitMeasure);
                        }
                    }
                    SelectedUnitMeasure = unitMeasureVm.AddedUnitMeasures.Last();
                }
                else
                {
                    await LoadUnitMeasureAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening Unit Measure Window: {ex.Message}\n\nStack Trace: {ex.StackTrace}");
            }
            finally
            {
                IsOverlayVisible = false;
            }
        }

        private void CreateNewProduct(bool keepLastSelections = false)
        {
            var lastCategory = keepLastSelections ? SelectedCategory : null;
            var lastItemType = keepLastSelections ? ItemTypeId : ItemType.RAW_MATERIAL;
            var lastUnitMeasure = keepLastSelections ? SelectedUnitMeasure : null;
            var lastUnitMeasureId = keepLastSelections ? UnitMeasureId : 0;
            SelectedProduct = null;

            ProductId = 0;

            Barcode = null;
            ProductName = string.Empty;
            ItemTypeId = lastItemType;
            ReorderPoint = 0;
            MaxStockQuantity = 0;
            AdditionalStockQuantity = 0;
            WastagePercentage = 0;
            IsPurchasable = true;
            IsActive = true;
            IsTaxApplicable = false;
            TrackExpiry = false;
            SearchText = string.Empty;
            SelectedCategory = lastCategory;
            SelectedUnitMeasure = lastUnitMeasure;
            UnitMeasureId = lastUnitMeasureId;
            SelectedTargetUnitMeasure = null;
            ConversionRate = 0;
            SelectedUnitConversion = null;
            UnitConversions.Clear();

            ClearAllErrors();

            ProductList?.Clear();

            RaiseCanExecuteChanged();
        }

        private void SetEditMode(bool isEditing)
        {
            IsEditing = isEditing;

            if (isEditing && SelectedProduct != null)
            {
                ProductId = SelectedProduct.ProductId;
                Barcode = SelectedProduct.Barcode;
                ProductName = SelectedProduct.ProductName;
                ItemTypeId = SelectedProduct.ItemTypeId.HasValue && SelectedProduct.ItemTypeId.Value > 0
                    ? (ItemType)SelectedProduct.ItemTypeId.Value
                    : ItemType.RAW_MATERIAL;
                ReorderPoint = SelectedProduct.ReorderPoint;
                MaxStockQuantity = SelectedProduct.MaxStockQuantity;
                AdditionalStockQuantity = SelectedProduct.AdditionalStockQuantity;
                WastagePercentage = SelectedProduct.WastagePercentage;
                IsPurchasable = SelectedProduct.IsPurchasable;
                IsActive = SelectedProduct.IsActive;
                IsTaxApplicable = SelectedProduct.IsTaxApplicable;
                TrackExpiry = SelectedProduct.TrackExpiry;

                UnitMeasureId = SelectedProduct.UnitMeasureId;

                if (Categories != null && Categories.Any())
                    SelectedCategory = Categories.FirstOrDefault(c => c.CategoryId == SelectedProduct.CategoryId);
                if (UnitMeasures != null && UnitMeasures.Any())
                    SelectedUnitMeasure = UnitMeasures.FirstOrDefault(u => u.UnitMeasureId == SelectedProduct.UnitMeasureId);

                LoadUnitConversionsFromProduct(SelectedProduct);
            }
        }

        private bool CanAddUnitConversion =>
            SelectedTargetUnitMeasure != null &&
            ConversionRate > 0 &&
            UnitMeasureId > 0;

        private void AddUnitConversion()
        {
            if (SelectedTargetUnitMeasure == null)
            {
                MessageBox.Show("Select a target unit measure.", "Unit Conversion", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SelectedTargetUnitMeasure.UnitMeasureId == UnitMeasureId)
            {
                MessageBox.Show("The alternative unit cannot be the same as the base unit.", "Unit Conversion", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ConversionRate <= 0)
            {
                MessageBox.Show("Conversion rate must be greater than zero.", "Unit Conversion", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (UnitConversions.Any(x => x.TargetUnitMeasureId == SelectedTargetUnitMeasure.UnitMeasureId))
            {
                MessageBox.Show("This alternative unit has already been added.", "Unit Conversion", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            UnitConversions.Add(new ProductUnitConversion
            {
                ProductId = ProductId,
                TargetUnitMeasureId = SelectedTargetUnitMeasure.UnitMeasureId,
                TargetUnitMeasureCode = SelectedTargetUnitMeasure.Code,
                TargetUnitMeasureName = SelectedTargetUnitMeasure.UnitMeasureName,
                ConversionRate = ConversionRate,
                IsActive = true
            });

            SelectedTargetUnitMeasure = null;
            ConversionRate = 0;
            (RemoveUnitConversionCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private void RemoveUnitConversion(object parameter)
        {
            var conversion = parameter as ProductUnitConversion ?? SelectedUnitConversion;
            if (conversion == null)
            {
                return;
            }

            UnitConversions.Remove(conversion);
            SelectedUnitConversion = null;
            (RemoveUnitConversionCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private void LoadUnitConversionsFromProduct(Product product)
        {
            UnitConversions.Clear();

            if (product?.UnitConversions == null)
            {
                return;
            }

            foreach (var conversion in product.UnitConversions)
            {
                UnitConversions.Add(conversion);
            }
        }

        private void SyncSelectedUnitMeasure()
        {
            var unitMeasure = UnitMeasures?.FirstOrDefault(u => u.UnitMeasureId == UnitMeasureId);
            if (!ReferenceEquals(_selectedUnitMeasure, unitMeasure))
            {
                _selectedUnitMeasure = unitMeasure;
                OnPropertyChanged(nameof(SelectedUnitMeasure));
            }

            OnPropertyChanged(nameof(BaseUnitMeasureName));
        }

        private void RaiseCanExecuteChanged()
        {
            (SaveProductCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (EditProductCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (DeleteProductCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (AddUnitConversionCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (RemoveUnitConversionCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
        #endregion

        #region Validation
        private void ValidateAll()
        {
            ValidateCategory();
            ValidateBarcode();
            ValidateProductName();
            ValidateUnitMeasure();
            ValidateReorderPoint();
        }

        private void ValidateCategory()
        {
            ClearErrors(nameof(SelectedCategory));
            if (SelectedCategory == null)
                AddError(nameof(SelectedCategory), "Category is required.");
        }

        private void ValidateBarcode()
        {
            ClearErrors(nameof(Barcode));
            if (string.IsNullOrWhiteSpace(Barcode))
            {
                return;
            }

            if (!Regex.IsMatch(Barcode, @"^[a-zA-Z0-9-]+$"))
            {
                AddError(nameof(Barcode), "Only contain letters, numbers, and dashes.");
            }
        }

        private void ValidateProductName()
        {
            ClearErrors(nameof(ProductName));

            if (string.IsNullOrWhiteSpace(ProductName))
            {
                AddError(nameof(ProductName), "Product name is required.");
            }
            else if (!Regex.IsMatch(ProductName, @"^[-a-zA-Z0-9""%,.&/()\s+\[\]\\]+$"))
            {
                AddError(nameof(ProductName), "Cannot contain this character.");
            }
        }

        private void ValidateUnitMeasure()
        {
            ClearErrors(nameof(SelectedUnitMeasure));
            if (UnitMeasureId <= 0)
                AddError(nameof(SelectedUnitMeasure), "Unit Measure is required.");
        }

        private void ValidateReorderPoint()
        {
            ClearErrors(nameof(ReorderPoint));
            if (ReorderPoint < 0)
                AddError(nameof(ReorderPoint), "Min stock quantity cannot be negative.");
        }
        #endregion
    }
}
