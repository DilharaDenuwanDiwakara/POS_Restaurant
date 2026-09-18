using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using PointOfSale.Core.Common;
using PointOfSale.Core.Interfaces.Repositories.Inventory;
using PointOfSale.Core.Interfaces.Repositories.Restaurant;
using PointOfSale.Core.Interfaces.Repositories.System;
using PointOfSale.Core.Interfaces.Services;
using PointOfSale.Core.Models.Inventory;
using PointOfSale.Core.Models.Restaurant;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;
using PointOfSale.UI.Services;

namespace PointOfSale.UI.ViewModels.Restaurant
{
    public class MenuItemViewModel : BaseViewModel
    {
        private readonly IMenuItemRepository _menuItemRepository;
        private readonly IMenuCategoryRepository _categoryRepository;
        private readonly IProductRepository _productRepository;
        private readonly IUnitMeasureRepository _unitMeasureRepository;
        private readonly IStationRepository _stationRepository;
        private readonly ITaxConfigurationRepository _taxConfigurationRepository;
        private readonly IUserSessionService _userSessionService;
        private readonly IDialogService _dialogService;
        private readonly CloudStorageService _storageService;
        private const decimal VatRate = 18m;
        private const decimal VatInclusiveFactor = 1.18m;

        private string _localImageToUploadPath;

        public MenuItemViewModel(
            IMenuItemRepository menuItemRepository,
            IMenuCategoryRepository categoryRepository,
            IProductRepository productRepository,
            IUnitMeasureRepository unitMeasureRepository,
            IStationRepository stationRepository,
            ITaxConfigurationRepository taxConfigurationRepository,
            IUserSessionService sessionService,
            IDialogService dialogService,
            CloudStorageService storageService)
        {
            _menuItemRepository = menuItemRepository;
            _categoryRepository = categoryRepository;
            _productRepository = productRepository;
            _unitMeasureRepository = unitMeasureRepository;
            _stationRepository = stationRepository;
            _taxConfigurationRepository = taxConfigurationRepository;
            _userSessionService = sessionService;
            _dialogService = dialogService;
            _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));

            // Header Commands
            AddStationCommand = new RelayCommand(ExecuteOpenStation);
            SaveItemCommand = new AsyncRelayCommand(async _ => await SaveMenuItemAsync(), _ => CanSaveItem);
            ClearCommand = new RelayCommand(_ => ClearForm());
            UploadImageCommand = new RelayCommand(_ => BrowseImage());

            // Tab 1 (Variants) Commands
            AddVariantCommand = new RelayCommand(_ => AddVariantToGrid(), _ => CanAddVariant);
            RemoveVariantCommand = new RelayCommand(RemoveVariantFromGrid);

            // Tab 2 (Recipe) Commands
            AddIngredientCommand = new RelayCommand(_ => AddIngredientToVariant(), _ => CanAddIngredient);
            RemoveIngredientCommand = new RelayCommand(RemoveIngredientFromVariant);

            // Load initial lookups
            _ = LoadReferenceDataAsync();
            _productRepository = productRepository;
        }

        #region Properties - Header & Configuration

        private bool _isEditing;
        public bool IsEditing
        {
            get => _isEditing;
            set
            {
                if (SetProperty(ref _isEditing, value))
                {
                    OnPropertyChanged(nameof(SubmitButtonText));
                }
            }
        }

        public string SubmitButtonText => IsEditing ? "Update" : "Save";

        private int _menuItemId;
        public int MenuItemId
        {
            get => _menuItemId;
            set => SetProperty(ref _menuItemId, value);
        }

        private string _name;
        public string Name
        {
            get => _name;
            set
            {
                SetProperty(ref _name, value);
                ValidateName();
                RaiseCanExecuteChanged();
            }
        }

        private string _description;
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }
        private bool _isNoVariant;
        public bool IsNoVariant
        {
            get => _isNoVariant;
            set
            {
                if (SetProperty(ref _isNoVariant, value))
                {
                    if (value)
                    {
                        // Auto-fill Logic
                        NewVariantName = "Standard";
                        NewVariantPortion = "1";
                    }
                    else
                    {
                        // Reset if unchecked
                        NewVariantName = string.Empty;
                        NewVariantPortion = string.Empty;
                    }
                    // Update the UI enabled state
                    OnPropertyChanged(nameof(IsVariantInputEnabled));
                    RaiseCanExecuteChanged();
                }
            }
        }

        // Helper to disable the textboxes when "No Variant" is checked
        public bool IsVariantInputEnabled => !IsNoVariant;

        private string _imageUrl;
        public string ImageUrl
        {
            get => _imageUrl;
            set
            {
                SetProperty(ref _imageUrl, value);
                RaiseCanExecuteChanged(); // <--- ADDED THIS so the button enables immediately after upload
            }
        }

        private bool _isActive = true;
        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        private bool _isAvailable = true;
        public bool IsAvailable
        {
            get => _isAvailable;
            set => SetProperty(ref _isAvailable, value);
        }

        // --- Lookups ---
        public ObservableCollection<MenuCategory> Categories { get; } = new ObservableCollection<MenuCategory>();
        public ObservableCollection<Station> Stations { get; } = new ObservableCollection<Station>();
        public ObservableCollection<TaxSelectionItem> AvailableTaxes { get; } = new ObservableCollection<TaxSelectionItem>();

        private int? _selectedCategoryId;
        public int? SelectedCategoryId
        {
            get => _selectedCategoryId;
            set
            {
                SetProperty(ref _selectedCategoryId, value);
                ValidateCategory();
                RaiseCanExecuteChanged();
            }
        }

        private int? _selectedStationId;
        public int? SelectedStationId
        {
            get => _selectedStationId;
            set
            {
                SetProperty(ref _selectedStationId, value);
                ValidateStation();
                RaiseCanExecuteChanged();
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

        #endregion

        #region Properties - Pricing & Variants

        // The Main Collection being built
        public ObservableCollection<Variant> Variants { get; } = new ObservableCollection<Variant>();

        // Scratchpad properties (TextBoxes for new variant)
        private string _newVariantName;
        public string NewVariantName
        {
            get => _newVariantName;
            set
            {
                var normalizedValue = value?.ToUpperInvariant();
                if (SetProperty(ref _newVariantName, normalizedValue))
                {
                    ValidateVariantName();
                    RaiseCanExecuteChanged();
                }
            }
        }

        private string _newVariantPortion;
        public string NewVariantPortion
        {
            get => _newVariantPortion;
            set
            {
                if (SetProperty(ref _newVariantPortion, value))
                {
                    ValidatePortionSize();
                    RaiseCanExecuteChanged();
                }
            }
        }

        private decimal _newVariantPrice;
        public decimal NewVariantPrice
        {
            get => _newVariantPrice;
            set
            {
                if (SetProperty(ref _newVariantPrice, value))
                {
                    ValidatePrice();
                    CalculateTaxBreakdown();
                    RaiseCanExecuteChanged();
                }
            }
        }

        private decimal _taxExclusivePrice;
        public decimal TaxExclusivePrice
        {
            get => _taxExclusivePrice;
            private set => SetProperty(ref _taxExclusivePrice, value);
        }

        private decimal _calculatedTaxAmount;
        public decimal CalculatedTaxAmount
        {
            get => _calculatedTaxAmount;
            private set => SetProperty(ref _calculatedTaxAmount, value);
        }

        public bool IsTaxBreakdownVisible => NewVariantPrice > 0m;

        public void CalculateTaxBreakdown()
        {
            if (IsVatSelected)
            {
                TaxExclusivePrice = NewVariantPrice / VatInclusiveFactor;
                CalculatedTaxAmount = NewVariantPrice - TaxExclusivePrice;
            }
            else
            {
                TaxExclusivePrice = NewVariantPrice;
                CalculatedTaxAmount = 0m;
            }

            OnPropertyChanged(nameof(IsTaxBreakdownVisible));
        }

        private bool IsVatSelected => AvailableTaxes.Any(t =>
            t.IsSelected &&
            string.Equals(t.TaxCode, "VAT", StringComparison.OrdinalIgnoreCase) &&
            t.Rate == VatRate);

        #endregion

        #region Properties - Recipe & Ingredients

        // The user selects a variant in the ComboBox to edit its recipe
        private Variant _selectedVariantForRecipe;
        public Variant SelectedVariantForRecipe
        {
            get => _selectedVariantForRecipe;
            set
            {
                var previous = _selectedVariantForRecipe;
                if (SetProperty(ref _selectedVariantForRecipe, value))
                {
                    DetachRecipeHandlers(previous);
                    AttachRecipeHandlers(value);

                    OnPropertyChanged(nameof(IsRecipeInputEnabled));
                    RaiseCanExecuteChanged(); // Updates AddIngredientCommand check
                    RecalculateFinancials();
                }
            }
        }

        public bool IsRecipeInputEnabled => SelectedVariantForRecipe != null;

        // --- Margin & Cost Calculator (real-time, driven by RecipeLines changes) ---
        private decimal _totalRecipeCost;
        public decimal TotalRecipeCost
        {
            get => _totalRecipeCost;
            private set => SetProperty(ref _totalRecipeCost, value);
        }

        private decimal _grossProfit;
        public decimal GrossProfit
        {
            get => _grossProfit;
            private set => SetProperty(ref _grossProfit, value);
        }

        private decimal _profitMarginPercent;
        public decimal ProfitMarginPercent
        {
            get => _profitMarginPercent;
            private set => SetProperty(ref _profitMarginPercent, value);
        }

        private void AttachRecipeHandlers(Variant variant)
        {
            if (variant == null) return;

            variant.PropertyChanged += SelectedVariant_PropertyChanged;
            variant.RecipeLines.CollectionChanged += RecipeLines_CollectionChanged;
            foreach (var line in variant.RecipeLines)
            {
                line.PropertyChanged += RecipeLine_PropertyChanged;
            }
        }

        private void DetachRecipeHandlers(Variant variant)
        {
            if (variant == null) return;

            variant.PropertyChanged -= SelectedVariant_PropertyChanged;
            variant.RecipeLines.CollectionChanged -= RecipeLines_CollectionChanged;
            foreach (var line in variant.RecipeLines)
            {
                line.PropertyChanged -= RecipeLine_PropertyChanged;
            }
        }

        private void RecipeLines_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (MenuRecipe item in e.OldItems)
                    item.PropertyChanged -= RecipeLine_PropertyChanged;
            }

            if (e.NewItems != null)
            {
                foreach (MenuRecipe item in e.NewItems)
                    item.PropertyChanged += RecipeLine_PropertyChanged;
            }

            RecalculateFinancials();
        }

        private void RecipeLine_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MenuRecipe.TotalCost))
            {
                RecalculateFinancials();
            }
        }

        private void SelectedVariant_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Variant.Price))
            {
                RecalculateFinancials();
            }
        }

        private void RecalculateFinancials()
        {
            var sellingPrice = SelectedVariantForRecipe?.Price ?? 0m;

            TotalRecipeCost = SelectedVariantForRecipe?.RecipeLines.Sum(x => x.TotalCost) ?? 0m;
            GrossProfit = sellingPrice - TotalRecipeCost;
            ProfitMarginPercent = sellingPrice > 0m ? (GrossProfit / sellingPrice) * 100m : 0m;
        }

        // Ingredient Lookups
        public ObservableCollection<Product> Products { get; } = new ObservableCollection<Product>();
        public ObservableCollection<RecipeUnitDto> AvailableUnits { get; } = new ObservableCollection<RecipeUnitDto>();
        private List<UnitMeasure> _unitMeasures = new List<UnitMeasure>();

        private Product _selectedRecipeProduct;
        public Product SelectedRecipeProduct
        {
            get => _selectedRecipeProduct;
            set
            {
                if (SetProperty(ref _selectedRecipeProduct, value))
                {
                    _ = LoadAvailableUnits();
                    RaiseCanExecuteChanged();
                }
            }
        }

        private RecipeUnitDto _selectedRecipeUnit;
        public RecipeUnitDto SelectedRecipeUnit
        {
            get => _selectedRecipeUnit;
            set
            {
                if (SetProperty(ref _selectedRecipeUnit, value))
                {
                    RaiseCanExecuteChanged();
                }
            }
        }

        private string _recipeQuantity;
        public string RecipeQuantity
        {
            get => _recipeQuantity;
            set
            {
                if (SetProperty(ref _recipeQuantity, value))
                {
                    ValidateRecipeQuantity();
                    RaiseCanExecuteChanged();
                }
            }
        }

        #endregion

        #region Commands & Logic
        public ICommand SaveItemCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand UploadImageCommand { get; }
        public ICommand AddVariantCommand { get; }
        public ICommand RemoveVariantCommand { get; }
        public ICommand AddIngredientCommand { get; }
        public ICommand RemoveIngredientCommand { get; }
        public ICommand AddStationCommand { get; }

        public event Action RequestRecipeProductFocus;
        public event Action RequestPricingTabFocus;

        public bool CanSaveItem => !HasErrors
                           && !string.IsNullOrWhiteSpace(Name)
                           && SelectedCategoryId.HasValue
                           && SelectedStationId.GetValueOrDefault() > 0
                           && Variants.Any();
        public bool CanAddVariant => IsNoVariant
            ? NewVariantPrice > 0
            : !string.IsNullOrWhiteSpace(NewVariantName)
              && !string.IsNullOrWhiteSpace(NewVariantPortion)
              && NewVariantPrice > 0;
        public bool CanAddIngredient => SelectedVariantForRecipe != null
                                        && SelectedRecipeProduct != null
                                        && SelectedRecipeUnit != null
                                        && SelectedRecipeUnit.UnitId > 0
                                        && !string.IsNullOrWhiteSpace(RecipeQuantity);

        private async Task LoadReferenceDataAsync()
        {
            try
            {
                var branchId = _userSessionService.BranchId;

                // 1. Load Categories
                var categoryList = await _categoryRepository.GetAllAsync();

                var parentIds = categoryList
                    .Where(c => c.ParentId.HasValue)
                    .Select(c => c.ParentId.Value)
                    .Distinct()
                    .ToHashSet();

                var leafCategories = categoryList
                    .Where(c => !parentIds.Contains(c.Id))
                    .OrderBy(c => c.Name);

                foreach (var c in leafCategories) Categories.Add(new MenuCategory { Id = c.Id, Name = c.Name });

                // 2. Load Stations (Hardcoded or from DB enum)
                await LoadStationAsync();

                // 3. Load Unit Measures and Ingredients/Products for Recipe
                _unitMeasures = (await _unitMeasureRepository.GetAllAsync()).ToList();

                var prods = await _productRepository.GetAllAsync();
                Products.Clear();
                foreach (var p in prods) Products.Add(p);

                // 4. Load active taxes for dynamic checkbox rendering
                await LoadTaxesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load data: {ex.Message}");
            }
        }

        private async Task LoadTaxesAsync()
        {
            var taxConfigs = await _taxConfigurationRepository.GetAllAsync();

            foreach (var tax in AvailableTaxes)
            {
                tax.PropertyChanged -= TaxSelectionItem_PropertyChanged;
            }

            AvailableTaxes.Clear();
            foreach (var tax in taxConfigs.Where(t => t.IsActive).OrderBy(t => t.CalculationOrder).ThenBy(t => t.TaxName))
            {
                var taxSelectionItem = new TaxSelectionItem
                {
                    TaxId = tax.Id,
                    TaxCode = tax.TaxCode,
                    TaxName = tax.TaxName,
                    Rate = tax.Rate,
                    IsSelected = true
                };

                taxSelectionItem.PropertyChanged += TaxSelectionItem_PropertyChanged;
                AvailableTaxes.Add(taxSelectionItem);
            }

            CalculateTaxBreakdown();
        }

        private void TaxSelectionItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TaxSelectionItem.IsSelected))
            {
                CalculateTaxBreakdown();
            }
        }
        private async Task LoadStationAsync()
        {
            var branchId = _userSessionService.BranchId;

            var station = await _stationRepository.GetAllAsync(branchId);
            Stations.Clear();
            foreach (var s in station) Stations.Add(s);
        }

        private async void ExecuteOpenStation(object parameter)
        {
            try
            {
                IsOverlayVisible = true;

                if (_dialogService == null)
                {
                    MessageBox.Show("Dialog Service is not initialized.");
                    return;
                }

                _dialogService.ShowDialog<StationViewModel>(out var stationViewModel);

                if (stationViewModel == null)
                {
                    // Log this warning
                    return;
                }

                if (stationViewModel.AddedStation != null && stationViewModel.AddedStation.Count > 0)
                {
                    // Loop through the accumulated list and add them to the ComboBox source
                    foreach (var station in stationViewModel.AddedStation)
                    {
                        // Check if it already exists to be safe (optional)
                        if (!Stations.Any(s => s.Id == station.Id))
                        {
                            Stations.Add(station);
                        }
                    }
                    SelectedStationId = stationViewModel.AddedStation.Last().Id;
                }
                else
                {
                    await LoadStationAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening station Window: {ex.Message}\n\nStack Trace: {ex.StackTrace}");
            }
            finally
            {
                IsOverlayVisible = false;
            }
        }

        private void BrowseImage()
        {
            var dlg = new OpenFileDialog { Filter = "Images|*.jpg;*.png;*.jpeg" };
            if (dlg.ShowDialog() == true)
            {
                var fileInfo = new FileInfo(dlg.FileName);
                if (fileInfo.Length > FileUploadConstraints.MaxFileSizeBytes)
                {
                    MessageBox.Show(FileUploadConstraints.BuildFileTooLargeMessage(fileInfo.Name), "File Too Large", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 1. Store the local path so we can upload it later
                _localImageToUploadPath = dlg.FileName;

                // 2. Update the UI immediately so the user sees the preview
                ImageUrl = dlg.FileName;
            }
        }

        // --- Variant Logic ---
        private void AddVariantToGrid()
        {
            var variant = new Variant
            {
                Name = NewVariantName,
                PortionSize = NewVariantPortion,
                Price = NewVariantPrice
                // Note: RecipeLines is auto-initialized in the Model constructor
            };
            Variants.Add(variant);

            if (Variants.Any())
            {
                SelectedVariantForRecipe = Variants.First();
            }

            NewVariantName = string.Empty;
            NewVariantPortion = string.Empty;
            NewVariantPrice = 0;

            ClearErrors(nameof(NewVariantName));
            ClearErrors(nameof(NewVariantPrice));

            RaiseCanExecuteChanged();
        }
        private void RemoveVariantFromGrid(object obj)
        {
            if (obj is Variant v)
            {
                Variants.Remove(v);
                if (SelectedVariantForRecipe == v) SelectedVariantForRecipe = null;
            }
        }

        // --- Ingredient Logic ---
        private void AddIngredientToVariant()
        {
            if (SelectedVariantForRecipe == null || SelectedRecipeProduct == null || SelectedRecipeUnit == null) return;

            if (decimal.TryParse(RecipeQuantity, out decimal qty))
            {
                var unitCode = SelectedRecipeUnit.UnitCode;
                var unitMeasureId = SelectedRecipeUnit.UnitId;
                var lineCost = CalculateRecipeLineCost(qty, SelectedRecipeProduct, SelectedRecipeUnit);
                var costPerSelectedUnit = qty > 0m ? lineCost / qty : 0m;

                var wastagePercentage = SelectedRecipeProduct.WastagePercentage;

                var wastageQty = qty * (wastagePercentage / 100m);
                var actualQty = qty + wastageQty;

                var actualCost = actualQty * costPerSelectedUnit;


                // 1. Check if this Product + Unit already exists in the current Variant's recipe
                var existingLine = SelectedVariantForRecipe.RecipeLines
                    .FirstOrDefault(x => x.ProductId == SelectedRecipeProduct.ProductId &&
                                         x.UnitMeasureId == unitMeasureId);

                if (existingLine != null)
                {
                    // 2. UPDATE: Item exists, just add to the quantity
                    existingLine.Quantity += qty;
                    if (existingLine.UnitMeasureId <= 0)
                    {
                        existingLine.UnitMeasureId = unitMeasureId;
                    }
                    if (string.IsNullOrWhiteSpace(existingLine.UnitName))
                    {
                        existingLine.UnitName = unitCode;
                    }
                    existingLine.CostPerUnit = costPerSelectedUnit;
                }
                else
                {
                    // 3. ADD: Item does not exist, create new line
                    var line = new MenuRecipe
                    {
                        ProductId = SelectedRecipeProduct.ProductId,
                        UnitMeasureId = unitMeasureId,
                        ProductName = SelectedRecipeProduct.ProductName,
                        WastagePercentage=SelectedRecipeProduct.WastagePercentage,
                        UnitName = unitCode,
                        Quantity = qty,
                        ActualQty = actualQty,
                        ActualCost = actualCost,
                        CostPerUnit = costPerSelectedUnit
                    };

                    SelectedVariantForRecipe.RecipeLines.Add(line);
                }

                // 4. Clear input
                SelectedRecipeProduct = null;
                RecipeQuantity = string.Empty;
                FocusRecipeProduct();
            }

        }

        private decimal CalculateRecipeLineCost(decimal qtyNeeded, Product ingredient, RecipeUnitDto selectedUnit)
        {
            if (ingredient == null || selectedUnit == null)
            {
                return 0m;
            }

            var actualQtyInBaseUnit = selectedUnit.IsBaseUnit || selectedUnit.ConversionRate <= 0m
                ? qtyNeeded
                : selectedUnit.IsMultiply
                    ? qtyNeeded * selectedUnit.ConversionRate
                    : qtyNeeded / selectedUnit.ConversionRate;

            return actualQtyInBaseUnit * ingredient.StandardCost;
        }

        private async Task LoadAvailableUnits()
        {
            var ingredient = SelectedRecipeProduct;

            AvailableUnits.Clear();
            SelectedRecipeUnit = null;

            if (ingredient == null)
            {
                return;
            }

            var baseUnit = new RecipeUnitDto
            {
                UnitId = ingredient.UnitMeasureId,
                UnitCode = !string.IsNullOrWhiteSpace(ingredient.UnitMeasureCode)
                            ? ingredient.UnitMeasureCode
                            : ingredient.UnitMeasureName,
                ConversionRate = 1m,
                IsMultiply = true,
                IsBaseUnit = true
            };

            AvailableUnits.Add(baseUnit);
            AddGlobalRecipeUnitConversions(ingredient.UnitMeasureCode, ingredient.UnitMeasureName);
            SelectedRecipeUnit = baseUnit;

            try
            {
                foreach (var conversion in ingredient.UnitConversions.Where(c => c.IsActive && c.ConversionRate > 0m))
                {
                    AddRecipeUnitIfMissing(new RecipeUnitDto
                    {
                        UnitId = conversion.TargetUnitMeasureId,
                        UnitCode = !string.IsNullOrWhiteSpace(conversion.TargetUnitMeasureCode)
                            ? conversion.TargetUnitMeasureCode
                            : conversion.TargetUnitMeasureName,
                        ConversionRate = conversion.ConversionRate,
                        IsMultiply = conversion.IsMultiply,
                        IsBaseUnit = false
                    });
                }

                var conversions = await _productRepository.GetUnitConversionsAsync(ingredient.ProductId);

                if (SelectedRecipeProduct != ingredient)
                {
                    return;
                }

                foreach (var conversion in conversions.Where(c => c.IsActive && c.ConversionRate > 0m))
                {
                    AddRecipeUnitIfMissing(new RecipeUnitDto
                    {
                        UnitId = conversion.TargetUnitMeasureId,
                        UnitCode = !string.IsNullOrWhiteSpace(conversion.TargetUnitMeasureCode)
                            ? conversion.TargetUnitMeasureCode
                            : conversion.TargetUnitMeasureName,
                        ConversionRate = conversion.ConversionRate,
                        IsMultiply = conversion.IsMultiply,
                        IsBaseUnit = false
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load unit conversions: {ex.Message}", "Unit Conversion", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void AddGlobalRecipeUnitConversions(params string[] baseUnitValues)
        {
            if (MatchesAnyUnit(baseUnitValues, "Kg", "KILOGRAM"))
            {
                var gramUnit = FindUnitMeasure("g", "gram");
                if (gramUnit != null)
                {
                    AddRecipeUnitIfMissing(new RecipeUnitDto
                    {
                        UnitId = gramUnit.UnitMeasureId,
                        UnitCode = GetUnitDisplayName(gramUnit),
                        ConversionRate = 1000m,
                        IsMultiply = false,
                        IsBaseUnit = false
                    });
                }
            }
            else if (MatchesAnyUnit(baseUnitValues, "L", "LITER"))
            {
                var milliliterUnit = FindUnitMeasure("ml", "milliliter", "millilitre");
                if (milliliterUnit != null)
                {
                    AddRecipeUnitIfMissing(new RecipeUnitDto
                    {
                        UnitId = milliliterUnit.UnitMeasureId,
                        UnitCode = GetUnitDisplayName(milliliterUnit),
                        ConversionRate = 1000m,
                        IsMultiply = false,
                        IsBaseUnit = false
                    });
                }
            }
        }

        private UnitMeasure FindUnitMeasure(params string[] matches)
        {
            return _unitMeasures.FirstOrDefault(unit =>
                matches.Any(match =>
                    string.Equals(unit.Code?.Trim(), match, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(unit.UnitMeasureName?.Trim(), match, StringComparison.OrdinalIgnoreCase)));
        }

        private static string GetUnitDisplayName(UnitMeasure unit)
        {
            return !string.IsNullOrWhiteSpace(unit.Code)
                ? unit.Code.Trim()
                : unit.UnitMeasureName;
        }

        private static bool MatchesAnyUnit(IEnumerable<string> unitValues, params string[] matches)
        {
            return unitValues
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Any(value => matches.Any(match => string.Equals(value.Trim(), match, StringComparison.OrdinalIgnoreCase)));
        }

        private void AddRecipeUnitIfMissing(RecipeUnitDto unit)
        {
            if (unit == null || unit.UnitId <= 0 || string.IsNullOrWhiteSpace(unit.UnitCode))
            {
                return;
            }

            if (AvailableUnits.Any(u =>
                u.UnitId == unit.UnitId ||
                string.Equals(u.UnitCode, unit.UnitCode, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            AvailableUnits.Add(unit);
        }
        private void RemoveIngredientFromVariant(object obj)
        {
            if (obj is MenuRecipe item && SelectedVariantForRecipe != null)
            {
                // 2. Remove it from the ObservableCollection
                SelectedVariantForRecipe.RecipeLines.Remove(item);
            }
        }

        // --- Save Logic ---
        private async Task SaveMenuItemAsync()
        {
            ValidateAll();
            if (HasErrors) return;

            try
            {
                if (!string.IsNullOrEmpty(_localImageToUploadPath) && File.Exists(_localImageToUploadPath))
                {
                    string fileName = Path.GetFileName(_localImageToUploadPath);

                    ImageUrl = await _storageService.UploadPublicFileAsync(_localImageToUploadPath, "menu-images", fileName);
                    _localImageToUploadPath = null;
                }

                var menuItem = new MenuItem
                {
                    Id = MenuItemId,
                    Name = Name,
                    Description = Description,
                    ImageUrl = ImageUrl,
                    CategoryId = SelectedCategoryId.Value,
                    StationId = SelectedStationId ?? 0,
                    IsActive = IsActive,
                    IsAvailable = IsAvailable,
                    Variants = Variants.ToList()
                };

                var selectedTaxIds = AvailableTaxes
                    .Where(t => t.IsSelected)
                    .Select(t => t.TaxId)
                    .Distinct()
                    .ToList();

                int createdBy = _userSessionService.UserId;

                if (IsEditing)
                {
                    await _menuItemRepository.UpdateAsync(menuItem, selectedTaxIds, createdBy);
                    MessageBox.Show("Menu Item Updated Successfully!", "Menu Item", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    await _menuItemRepository.CreateAsync(menuItem, selectedTaxIds, createdBy);
                    MessageBox.Show("Menu Item Saved Successfully!", "Menu Item", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                ClearForm();
                FocusPricingTab();
            }
            catch (Exception ex)
            {
                var detail = ex.InnerException?.Message ?? string.Empty;
                var fullMessage = string.IsNullOrEmpty(detail)
                    ? ex.Message
                    : $"{ex.Message}\n\n--- SQL Detail ---\n{detail}";

                MessageBox.Show(fullMessage, "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearForm()
        {
            MenuItemId = 0;
            IsEditing = false;
            Name = string.Empty;
            Description = string.Empty;
            ImageUrl = null;
            _localImageToUploadPath = null;
            Variants.Clear();
            SelectedCategoryId = null;
            SelectedStationId = null;
            SelectedRecipeProduct = null;
            RecipeQuantity = string.Empty;
            NewVariantName = string.Empty;
            NewVariantPrice = 0;
            SelectedVariantForRecipe = null;

            foreach (var tax in AvailableTaxes)
            {
                tax.IsSelected = false;
            }

            ClearAllErrors();
            RaiseCanExecuteChanged();
        }

        public async Task LoadItemForEditAsync(MenuItem item, IEnumerable<int> selectedTaxIds)
        {
            ClearForm();

            IsEditing = true;
            MenuItemId = item.Id;
            Name = item.Name;
            Description = item.Description;
            ImageUrl = item.ImageUrl;
            SelectedCategoryId = item.CategoryId;
            SelectedStationId = item.StationId == 0 ? null : (int?)item.StationId;
            IsActive = item.IsActive;
            IsAvailable = item.IsAvailable;

            // Load variants
            Variants.Clear();
            foreach (var variant in item.Variants)
            {
                Variants.Add(variant);
            }

            if (Variants.Any())
            {
                SelectedVariantForRecipe = Variants.First();
            }

            // SelectedVariantForRecipe's setter already wires up RecipeLines change
            // tracking and recalculates the summary, but call it explicitly here too
            // so the Margin/Cost panel is correct immediately after an Edit load,
            // even if no variant was selected above (e.g. an item with no variants yet).
            RecalculateFinancials();

            // Wait until AvailableTaxes has items (if background load is still in progress)
            int retries = 0;
            while (!AvailableTaxes.Any() && retries < 20)
            {
                await Task.Delay(100);
                retries++;
            }

            foreach (var tax in AvailableTaxes)
            {
                tax.IsSelected = selectedTaxIds.Contains(tax.TaxId);
            }

            RaiseCanExecuteChanged();
        }

        #endregion

        #region Validation Helpers
        private void ValidateAll()
        {
            ValidateName();
            ValidateCategory();
            ValidateStation();
        }

        private void ValidateName()
        {
            ClearErrors(nameof(Name));
            if (string.IsNullOrWhiteSpace(Name)) AddError(nameof(Name), "Item Name is required.");
        }

        private void ValidateCategory()
        {
            ClearErrors(nameof(SelectedCategoryId));
            if (!SelectedCategoryId.HasValue) AddError(nameof(SelectedCategoryId), "Category is required.");
        }

        private void ValidateStation()
        {
            ClearErrors(nameof(SelectedStationId));
            if (SelectedStationId.GetValueOrDefault() <= 0) AddError(nameof(SelectedStationId), "Station is required.");
        }

        private void ValidateVariantName()
        {
            ClearErrors(nameof(NewVariantName));
            if (string.IsNullOrWhiteSpace(NewVariantName))
            {
                AddError(nameof(NewVariantName), "This is required");
            }
            else if (!Regex.IsMatch(NewVariantName, @"^[a-zA-Z0-9\s]+$"))
                AddError(nameof(NewVariantName), "Only contain numbers");

        }
        private void ValidatePortionSize()
        {
            ClearErrors(nameof(NewVariantPortion));
            if (!string.IsNullOrWhiteSpace(NewVariantPortion))
            {
                if (!Regex.IsMatch(NewVariantPortion, @"^[0-9]+$"))
                    AddError(nameof(NewVariantPortion), "Only contain numbers");
            }

        }
        private void ValidatePrice()
        {
            ClearErrors(nameof(NewVariantPrice));
            if (NewVariantPrice <= 0)
                AddError(nameof(NewVariantPrice), "Enter valid unit price");
        }
        private void ValidateRecipeQuantity()
        {
            ClearErrors(nameof(RecipeQuantity));

            if (!System.Text.RegularExpressions.Regex.IsMatch(RecipeQuantity, @"^\d*\.?\d{0,3}$"))
            {
                AddError(nameof(RecipeQuantity), "Must be a number (max 3 decimals)");
                return;
            }

        }
        private void RaiseCanExecuteChanged()
        {
            (SaveItemCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (AddVariantCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AddIngredientCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private void FocusRecipeProduct()
        {
            RequestRecipeProductFocus?.Invoke();
        }

        private void FocusPricingTab()
        {
            RequestPricingTabFocus?.Invoke();
        }

        #endregion

    }

    public class RecipeUnitDto
    {
        public int UnitId { get; set; }
        public string UnitCode { get; set; }
        public decimal ConversionRate { get; set; }
        public bool IsMultiply { get; set; } = true;
        public bool IsBaseUnit { get; set; }
    }
}
