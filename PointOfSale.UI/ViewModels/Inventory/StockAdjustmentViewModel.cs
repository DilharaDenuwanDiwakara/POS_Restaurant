using PointOfSale.Core.Interfaces.Repositories.Inventory;
using PointOfSale.Core.Interfaces.Services;
using PointOfSale.Core.Models.Inventory;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace PointOfSale.UI.ViewModels.Inventory
{
    public class StockAdjustmentViewModel : BaseViewModel
    {
        private readonly IStockAdjustmentRepository _stockAdjustmentRepository;
        private readonly IProductRepository _productRepository;
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IUserSessionService _userSessionService;

        public StockAdjustmentViewModel(
            IProductRepository productRepository,
            IDialogService dialogService,
            IStockAdjustmentRepository stockAdjustmentRepository,
            IInventoryRepository inventoryRepository,
            IUserSessionService userSessionService)
        {
            _stockAdjustmentRepository = stockAdjustmentRepository ?? throw new ArgumentNullException(nameof(stockAdjustmentRepository));
            _inventoryRepository = inventoryRepository ?? throw new ArgumentNullException(nameof(inventoryRepository));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));
            _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));

            if (dialogService == null)
                throw new ArgumentNullException(nameof(dialogService));

            Locations = new ObservableCollection<Location>();
            StockAdjustments = new ObservableCollection<StockAdjustment>();
            Products = new ObservableCollection<Product>();

            // Initialize Commands
            RefreshCommand = new RelayCommand(async _ => await LoadCurrentStockAsync());
            AddLineCommand = new RelayCommand(_ => AddLine(), _ => CanAddLine());
            RemoveLineCommand = new RelayCommand(RemoveLine, CanRemoveLine);
            ClearCommand = new RelayCommand(_ => ClearLines(), _ => StockAdjustments?.Any() == true);
            SaveCommand = new AsyncRelayCommand(async _ => await ExecuteSave(), _ => CanExecuteSave());

            AdjustDate = DateTime.Today;

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
                    AdjustmentQuantity = string.Empty;
                    _ = LoadCurrentStockAsync();
                    AddLineCommand.RaiseCanExecuteChanged();
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
                    AddLineCommand.RaiseCanExecuteChanged();
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

        private StockAdjustment _selectedLine;
        public StockAdjustment SelectedLine
        {
            get => _selectedLine;
            set => SetProperty(ref _selectedLine, value);
        }

        private DateTime? _adjustDate;
        public DateTime? AdjustDate
        {
            get => _adjustDate;
            set
            {
                if (SetProperty(ref _adjustDate, value))
                {
                    SaveCommand.RaiseCanExecuteChanged();
                }
            }
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
                    _ = LoadCurrentStockAsync();
                    AddLineCommand.RaiseCanExecuteChanged();
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
                    AddLineCommand.RaiseCanExecuteChanged();
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
                    ValidateReason();
                    AddLineCommand.RaiseCanExecuteChanged();
                    SaveCommand.RaiseCanExecuteChanged();
                }
            }
        }
        #endregion

        #region Commands
        public RelayCommand RefreshCommand { get; }
        public RelayCommand AddLineCommand { get; }
        public RelayCommand RemoveLineCommand { get; }
        public RelayCommand ClearCommand { get; }
        public AsyncRelayCommand SaveCommand { get; }
        #endregion

        #region Methods
        private async Task LoadCurrentStockAsync()
        {
            try
            {
                var product = SelectedProduct;
                var location = SelectedLocation;

                if (product == null || location == null)
                {
                    CurrentStock = 0;
                    return;
                }

                var list = await _stockAdjustmentRepository.GetAvailableQty(location.Id, product.ProductId);

                if (SelectedProduct?.ProductId != product.ProductId || SelectedLocation?.Id != location.Id)
                    return;

                var match = list?.FirstOrDefault();
                var databaseQuantity = match != null ? match.Quantity : 0;
                CurrentStock = databaseQuantity + GetPendingStockChange(product.ProductId);
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
        private bool CanAddLine()
        {
            return SelectedProduct != null
                && !string.IsNullOrWhiteSpace(SelectedAddOrDeduct)
                && decimal.TryParse(AdjustmentQuantity, out var quantity)
                && quantity > 0;
        }

        private void AddLine()
        {
            // Snapshot editable UI values before changing the collection. WPF can update
            // SelectedItem while the command is being dispatched from an editable ComboBox.
            var product = SelectedProduct;
            var actionType = SelectedAddOrDeduct;

            if (product == null
                || string.IsNullOrWhiteSpace(actionType)
                || !decimal.TryParse(AdjustmentQuantity, out var quantity)
                || quantity <= 0)
            {
                return;
            }

            var lines = StockAdjustments;
            if (lines == null)
            {
                lines = new ObservableCollection<StockAdjustment>();
                StockAdjustments = lines;
            }

            var existingLine = lines.FirstOrDefault(x => x.ProductId == product.ProductId);
            var previousStockChange = existingLine == null ? 0 : GetSignedQuantity(existingLine);
            var incomingStockChange = string.Equals(
                actionType,
                "REDUCE",
                StringComparison.OrdinalIgnoreCase)
                    ? -Math.Abs(quantity)
                    : Math.Abs(quantity);

            if (incomingStockChange < 0 && Math.Abs(incomingStockChange) > CurrentStock)
            {
                MessageBox.Show(
                    $"Cannot reduce {Math.Abs(incomingStockChange):N3}. Only {CurrentStock:N3} is available.",
                    "Insufficient Stock",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (existingLine != null)
            {
                var mergedSignedQuantity = previousStockChange + incomingStockChange;
                var existingIndex = lines.IndexOf(existingLine);

                if (mergedSignedQuantity == 0)
                {
                    lines.RemoveAt(existingIndex);
                }
                else
                {
                    lines[existingIndex] = new StockAdjustment
                    {
                        ProductId = existingLine.ProductId,
                        ProductName = existingLine.ProductName,
                        Quantity = Math.Abs(mergedSignedQuantity),
                        ActionType = mergedSignedQuantity < 0 ? "REDUCE" : "ADD"
                    };
                }
            }
            else
            {
                lines.Add(new StockAdjustment
                {
                    ProductId = product.ProductId,
                    ProductName = product.ProductName,
                    Quantity = Math.Abs(quantity),
                    ActionType = actionType
                });
            }

            var mergedLine = lines.FirstOrDefault(x => x.ProductId == product.ProductId);
            var currentStockChange = mergedLine == null ? 0 : GetSignedQuantity(mergedLine);
            CurrentStock += currentStockChange - previousStockChange;

            AdjustmentQuantity = string.Empty;

            RaiseLineCommandStates();
        }

        private void RemoveLine(object parameter)
        {
            if (!(parameter is StockAdjustment line))
                return;

            StockAdjustments.Remove(line);

            if (SelectedProduct?.ProductId == line.ProductId)
            {
                CurrentStock -= GetSignedQuantity(line);
            }

            if (ReferenceEquals(SelectedLine, line))
            {
                SelectedLine = null;
            }

            RaiseLineCommandStates();
        }

        private bool CanRemoveLine(object parameter)
        {
            return parameter is StockAdjustment line && StockAdjustments.Contains(line);
        }

        private void ClearLines()
        {
            StockAdjustments.Clear();
            SelectedLine = null;
            ResetForm();
            RaiseLineCommandStates();
        }

        private void RaiseLineCommandStates()
        {
            AddLineCommand.RaiseCanExecuteChanged();
            RemoveLineCommand.RaiseCanExecuteChanged();
            ClearCommand.RaiseCanExecuteChanged();
            SaveCommand.RaiseCanExecuteChanged();
        }

        private decimal GetPendingStockChange(int productId)
        {
            return StockAdjustments?
                .Where(x => x.ProductId == productId)
                .Sum(GetSignedQuantity) ?? 0;
        }

        private static decimal GetSignedQuantity(StockAdjustment line)
        {
            return string.Equals(line.ActionType, "REDUCE", StringComparison.OrdinalIgnoreCase)
                ? -Math.Abs(line.Quantity)
                : Math.Abs(line.Quantity);
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
        private bool CanExecuteSave()
        {
            return SelectedLocation != null
                && AdjustDate.HasValue
                && StockAdjustments != null
                && StockAdjustments.Any()
                && !HasErrors;
        }
        private async Task ExecuteSave()
        {
            try
            {
                if (StockAdjustments == null || !StockAdjustments.Any())
                {
                    MessageBox.Show("Please add items to the adjustment list before saving.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (SelectedLocation == null)
                {
                    MessageBox.Show("Please select a location.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var stockAdjustmentHeader = new StockAdjustment
                {
                    BranchId = _userSessionService.BranchId,
                    UserId = _userSessionService.UserId,
                    LocationId = SelectedLocation.Id,
                    AdjustDate = AdjustDate.Value,
                    Note = string.IsNullOrWhiteSpace(Reason) ? null : Reason.Trim(),
                    Lines = new List<StockAdjustmentLine>()
                };

                foreach (var item in StockAdjustments)
                {
                    decimal finalQuantity = item.ActionType == "REDUCE" ? -Math.Abs(item.Quantity) : Math.Abs(item.Quantity);

                    stockAdjustmentHeader.Lines.Add(new StockAdjustmentLine
                    {
                        ProductId = item.ProductId,
                        Quantity = finalQuantity,
                        Reason = item.Reason
                    });
                }

                await _stockAdjustmentRepository.CreateAsync(stockAdjustmentHeader);

                StockAdjustments.Clear();
                SelectedLine = null;
                ResetForm();
                await LoadCurrentStockAsync();
                RaiseLineCommandStates();

                MessageBox.Show(
                    $"Stock adjustment {stockAdjustmentHeader.AdjustmentNumber} saved successfully!",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResetForm()
        {
            AdjustmentQuantity = string.Empty;
            Reason = string.Empty;
            SelectedProduct = null;
            SelectedProductId = null;
            CurrentStock = 0;

            ClearAllErrors();
        }

        #endregion

        #region Validation
        private void ValidateReason()
        {
            ClearErrors(nameof(Reason));

            if (!string.IsNullOrWhiteSpace(Reason) && Reason.Trim().Length > 200)
            {
                AddError(nameof(Reason), "Note cannot exceed 200 characters.");
            }
            else if (!string.IsNullOrWhiteSpace(Reason)
                && !Regex.IsMatch(Reason, @"^[-a-zA-Z0-9""%,.&/()\s+\[\]\\]+$"))
            {
                AddError(nameof(Reason), "Note contains an invalid character.");
            }
        }
        #endregion
    }
}

