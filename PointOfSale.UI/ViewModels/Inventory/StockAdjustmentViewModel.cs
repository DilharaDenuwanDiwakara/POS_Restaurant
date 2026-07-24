using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using PointOfSale.Core.Interfaces.Repositories.Inventory;
using PointOfSale.Core.Interfaces.Services;
using PointOfSale.Core.Models.Inventory;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Inventory
{
    public class StockAdjustmentViewModel : BaseViewModel
    {
        private readonly IStockAdjustmentRepository _stockAdjustmentRepository;
        private readonly IProductRepository _productRepository;
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IUserSessionService _userSessionService;
        private readonly IDialogService _dialogService;

        public StockAdjustmentViewModel(
            IProductRepository productRepository,
            IDialogService dialogService,
            IStockAdjustmentRepository stockAdjustmentRepository,
            IInventoryRepository inventoryRepository,
            IUserSessionService userSessionService)
        {
            _stockAdjustmentRepository = stockAdjustmentRepository ?? throw new ArgumentNullException(nameof(stockAdjustmentRepository));
            _inventoryRepository = inventoryRepository ?? throw new ArgumentNullException(nameof(inventoryRepository));
            _userSessionService = userSessionService;
            _productRepository = productRepository;

            Locations = new ObservableCollection<Location>();
            StockAdjustments = new ObservableCollection<StockAdjustment>();
            Products = new ObservableCollection<Product>();

            // Initialize Commands
            RefreshCommand = new RelayCommand(async _ => await LoadAdjustmentsAsync());
            SaveCommand = new AsyncRelayCommand(async _ => await ExecuteSave(), _ => CanExecuteSave());

            // Load Initial Data
            _ = LoadDependenciesAsync();

            AddorDeduct = new ObservableCollection<string>
            {
                "ADD", "REDUCE"
            };
            SelectedAddOrDeduct = AddorDeduct[0];
        }

        public ObservableCollection<string> AddorDeduct { get; }

        #region Properties
        private ObservableCollection<Product> _products;
        public ObservableCollection<Product> Products
        {
            get => _products;
            set => SetProperty(ref _products, value);
        }

        private Product _selectedProduct;
        public Product SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                if (SetProperty(ref _selectedProduct, value))
                {
                    if (value != null) SelectedProductId = value.ProductId;
                    _ = LoadCurrentStockAsync();
                    SaveCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private string _selectedAddOrDeduct;
        public string SelectedAddOrDeduct
        {
            get => _selectedAddOrDeduct;
            set
            {
                if (SetProperty(ref _selectedAddOrDeduct, value))
                {
                    SaveCommand.RaiseCanExecuteChanged(); // Update Button
                }

            }
        }

        private int? _selectedProductId;
        public int? SelectedProductId
        {
            get => _selectedProductId;
            set => SetProperty(ref _selectedProductId, value);
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

        private ObservableCollection<StockAdjustment> _stockAdjustments;
        public ObservableCollection<StockAdjustment> StockAdjustments
        {
            get => _stockAdjustments;
            set => SetProperty(ref _stockAdjustments, value);
        }

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

                    _ = LoadAdjustmentsAsync();
                    _ = LoadAvailableQuantityAsync();
                    SaveCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private decimal _currentStock;
        public decimal CurrentStock
        {
            get => _currentStock;
            set => SetProperty(ref _currentStock, value);
        }

        // 2. Adjustment Quantity (User Input - Bound to TextBox)
        private string _adjustmentQuantity;
        public string AdjustmentQuantity
        {
            get => _adjustmentQuantity;
            set
            {
                if (SetProperty(ref _adjustmentQuantity, value))
                {
                    // Fix 2: Notify Command to re-evaluate
                    SaveCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private decimal _quantity;
        public decimal Quantity
        {
            get => _quantity;
            set => SetProperty(ref _quantity, value);
        }

        private string _reason;
        public string Reason
        {
            get => _reason;
            set
            {
                if (SetProperty(ref _reason, value))
                {
                    SaveCommand.RaiseCanExecuteChanged();
                }
                ValidateReason();

            }
        }
        #endregion

        #region Commands
        public RelayCommand RefreshCommand { get; }
        public AsyncRelayCommand SaveCommand { get; }
        #endregion

        #region Methods
        private async Task LoadCurrentStockAsync()
        {
            try
            {
                if (SelectedProduct == null || SelectedLocation == null)
                {
                    CurrentStock = 0;
                    return;
                }

                var list = await _stockAdjustmentRepository.GetAvailableQty(SelectedLocation.Id, SelectedProduct.ProductId);
                var match = list?.FirstOrDefault();

                // Fix 3: Update the Display Property, NOT the Input Property
                CurrentStock = match != null ? match.Quantity : 0;

                // Optional: Reset the input field when product changes
                AdjustmentQuantity = string.Empty;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }
        private async Task LoadProductsAsync()
        {
            var allProducts = await _productRepository.GetAllAsync();
            Products = new ObservableCollection<Product>(allProducts);
        }
        private async Task LoadAvailableQuantityAsync()
        {
            try
            {
                if (SelectedProduct == null || SelectedLocation == null)
                {
                    Quantity = 0;
                    return;
                }
                ;

                var list = await _stockAdjustmentRepository.GetAvailableQty(SelectedLocation.Id, SelectedProduct.ProductId);

                var match = list?.FirstOrDefault();

                if (match != null)
                {
                    Quantity = match.Quantity;
                }
                else
                {
                    Quantity = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        private async Task LoadDependenciesAsync()
        {
            try
            {
                var branchId = _userSessionService.BranchId;

                var locations = await _inventoryRepository.GetLocationsByBranchAsync(branchId);
                Locations = new ObservableCollection<Location>(locations);

                if (Locations.Any())
                {
                    SelectedLocation = Locations.First();
                }
                await LoadProductsAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load locations: {ex.Message}";
            }
        }

        private async Task LoadAdjustmentsAsync()
        {
            try
            {
                // Using the GetAllAsync method we created earlier
                var data = await _stockAdjustmentRepository.GetAllAsync();

                // Apply local filtering if a search term exists
                if (!string.IsNullOrWhiteSpace(Reason))
                {
                    data = data.Where(x => x.Reason != null && x.Reason.IndexOf(Reason, StringComparison.OrdinalIgnoreCase) >= 0);
                }

                StockAdjustments = new ObservableCollection<StockAdjustment>(data);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load stock adjustments: {ex.Message}");
            }
        }

        private bool CanExecuteSave()
        {
            return SelectedLocation != null
            && SelectedProduct != null
            && !string.IsNullOrEmpty(SelectedAddOrDeduct)
            && decimal.TryParse(AdjustmentQuantity, out decimal q) && q > 0
            && !string.IsNullOrWhiteSpace(Reason);
        }
        private async Task ExecuteSave()
        {
            try
            {
                // Double check parsing (though CanExecute covers it)
                if (!decimal.TryParse(AdjustmentQuantity, out decimal quantityInput) || quantityInput <= 0)
                    return;

                decimal finalQuantity = SelectedAddOrDeduct == "REDUCE" ? -quantityInput : quantityInput;

                var stockAdjustment = new StockAdjustment
                {
                    BranchId = _userSessionService.BranchId,
                    UserId = _userSessionService.UserId,
                    LocationId = SelectedLocation.Id,
                    ProductId = SelectedProduct.ProductId, // Ensure this property is synced
                    Quantity = finalQuantity,
                    Reason = Reason.Trim()
                };

                await _stockAdjustmentRepository.CreateAsync(stockAdjustment);

                await LoadAdjustmentsAsync();
                await LoadCurrentStockAsync(); // Refresh the "Current Stock" display

                ResetForm();
                MessageBox.Show($"Successfully saved. New Stock: {CurrentStock}");
            }
            catch (Exception ex)
            {
                // ErrorMessage = ex.Message; // Assuming you have an ErrorMessage property
                MessageBox.Show(ex.Message);
            }
        }

        private void ResetForm()
        {
            AdjustmentQuantity = string.Empty; // Clear input
            Reason = string.Empty;

            ClearAllErrors();
        }

        #endregion

        #region Validation
        private void ValidateReason()
        {
            ClearErrors(nameof(Reason));

            if (string.IsNullOrWhiteSpace(Reason))
            {
                AddError(nameof(Reason), "Reason name is required.");
            }
            else if (!Regex.IsMatch(Reason, @"^[-a-zA-Z0-9""%,.&/()\s+\[\]\\]+$"))
            {
                AddError(nameof(Reason), "Cannot contain this character.");
            }
        }
        #endregion
    }
}

