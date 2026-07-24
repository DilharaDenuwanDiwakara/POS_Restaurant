using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using PointOfSale.Core.Interfaces.Repositories.Inventory;
using PointOfSale.Core.Interfaces.Services;
using PointOfSale.Core.Models.Inventory;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;
using PointOfSale.UI.Views.Inventory;

namespace PointOfSale.UI.ViewModels.Inventory
{
    public class WastageViewModel : BaseViewModel
    {
        private readonly IWastageRepository _wastageRepository;
        private readonly IWastageReasonRepository _wastageReasonRepository;
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IProductRepository _productRepository;
        private readonly IProductBatchRepository _productBatchRepository;
        private readonly IUserSessionService _sessionService;
        private readonly IDialogService _dialogService;

        public WastageViewModel(
            IWastageRepository wastageRepository,
            IWastageReasonRepository wastageReasonRepository,
            IInventoryRepository inventoryRepository,
            IProductRepository productRepository,
            IProductBatchRepository productBatchRepository,
            IUserSessionService sessionService,
            IDialogService dialogService)
        {
            _wastageRepository = wastageRepository;
            _wastageReasonRepository = wastageReasonRepository;
            _inventoryRepository = inventoryRepository;
            _productRepository = productRepository;
            _productBatchRepository = productBatchRepository;
            _sessionService = sessionService;
            _dialogService = dialogService;

            // Initialize Commands
            SaveCommand = new AsyncRelayCommand(async _ => await SaveWastageAsync(), _ => CanSave);
            AddLineCommand = new RelayCommand(_ => AddLine(), _ => CanAddLine);
            RemoveLineCommand = new RelayCommand<WastageLine>(RemoveLine);
            ClearCommand = new RelayCommand(_ => ClearAll());
            AddReasonCommand = new RelayCommand(ExecuteOpenWastageReason);

            Locations = new ObservableCollection<Location>();
            Products = new ObservableCollection<Product>();
            Reasons = new ObservableCollection<WastageReason>();
            AvailableBatches = new ObservableCollection<ProductBatch>();

            // Load Initial Data
            _ = LoadInitialDataAsync();

        }

        #region Properties - Header

        private DateTime _wastageDate = DateTime.Now;
        public DateTime WastageDate
        {
            get => _wastageDate;
            set => SetProperty(ref _wastageDate, value);
        }

        public ObservableCollection<Location> Locations { get; }

        private int _locationId;
        public int LocationId
        {
            get => _locationId;
            set
            {
                if (SetProperty(ref _locationId, value))
                {
                    // If location changes, clear the grid because those items might not exist in the new location
                    WastageLines.Clear();
                    ClearItemEntry();
                    RefreshCommands();
                }
            }
        }

        private string _note;
        public string Note
        {
            get => _note;
            set => SetProperty(ref _note, value);
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

        #region Properties - Item Entry
        public ObservableCollection<Product> Products { get; }
        public ObservableCollection<WastageReason> Reasons { get; }
        public ObservableCollection<ProductBatch> AvailableBatches { get; }

        private string _barcode;
        public string Barcode
        {
            get => _barcode;
            set
            {
                if (SetProperty(ref _barcode, value))
                {
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        SearchAndSelectProduct(value);
                    }
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
                    if (value != null)
                    {
                        _ = LoadBatchesForProductAsync();
                    }
                    else
                    {
                        ClearItemEntry();
                    }
                    RefreshCommands();
                }
            }
        }

        private void SearchAndSelectProduct(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                FocusProduct();
                return;
            }

            if (Products == null) return;

            var trimmedCode = code.Trim();
            var foundProduct = Products.FirstOrDefault(p =>
                string.Equals(p.Barcode, trimmedCode, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.ProductCode, trimmedCode, StringComparison.OrdinalIgnoreCase));

            if (foundProduct != null)
            {
                SelectedProduct = foundProduct;
                FocusQuantity();
            }
        }

        private ProductBatch _selectedBatch;
        public ProductBatch SelectedBatch
        {
            get => _selectedBatch;
            set
            {
                if (SetProperty(ref _selectedBatch, value))
                {
                    RefreshCommands(); // <--- Add This
                }
            }
        }

        private int? _reasonId;
        public int? ReasonId
        {
            get => _reasonId;
            set => SetProperty(ref _reasonId, value);
        }

        private string _quantity;
        public string Quantity
        {
            get => _quantity;
            set
            {
                if (SetProperty(ref _quantity, value))
                {
                    ValidateQuantity();
                    RefreshCommands();

                }

            }
        }

        #endregion

        #region Properties - Grid

        public ObservableCollection<WastageLine> WastageLines { get; } = new ObservableCollection<WastageLine>();

        private WastageLine _selectedLine;
        public WastageLine SelectedLine
        {
            get => _selectedLine;
            set => SetProperty(ref _selectedLine, value);
        }

        #endregion

        #region Commands
        public ICommand SaveCommand { get; }
        public ICommand AddLineCommand { get; }
        public ICommand RemoveLineCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand AddReasonCommand { get; }
        #endregion

        #region Logic
        private void RefreshCommands()
        {
            (AddLineCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SaveCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
        public bool CanSave => WastageLines.Any() && LocationId > 0;
        public bool CanAddLine => SelectedProduct != null
            && SelectedBatch != null
            && ReasonId > 0
            && (!string.IsNullOrWhiteSpace(Quantity))
            && !HasErrors;

        private async Task LoadInitialDataAsync()
        {
            try
            {
                // 1. Load Locations
                var locs = await _inventoryRepository.GetLocationsByBranchAsync(_sessionService.BranchId);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Locations.Clear();
                    foreach (var l in locs) Locations.Add(l);
                    if (Locations.Any()) LocationId = Locations.First().Id;
                });

                await LoadReasonsAsync();

                await LoadProductsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading data: {ex.Message}");
            }
        }

        private async Task LoadReasonsAsync()
        {
            try
            {
                var reasons = await _wastageReasonRepository.GetAllAsync(); // Use WastageReasonRepository
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Reasons.Clear();
                    foreach (var r in reasons) Reasons.Add(r);
                    if (Reasons.Any()) ReasonId = Reasons.First().Id; // Use correct ID property
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading reasons: {ex.Message}");
            }
        }
        private async Task LoadProductsAsync()
        {
            try
            {
                var prods = await _productRepository.GetAllAsync();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Products.Clear();
                    foreach (var p in prods) Products.Add(p);
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading products: {ex.Message}");
            }
        }
        private async Task LoadBatchesForProductAsync()
        {
            AvailableBatches.Clear();
            SelectedBatch = null;

            if (SelectedProduct == null || LocationId == 0) return;

            try
            {
                // 2. Fetch from DB
                // Ensure you use .Id or .ProductId depending on your Model logic
                var batches = await _productBatchRepository.GetAvailableBatchesAsync(SelectedProduct.ProductId, LocationId);

                // 3. Filter for positive stock
                var validBatches = batches.Where(b => b.AvailableQuantity > 0).ToList();

                if (!validBatches.Any())
                {
                    MessageBox.Show("No stock available for this product in the selected location.");
                    SelectedProduct = null; // Reset selection
                    return;
                }

                // 4. Update Internal List
                foreach (var b in validBatches)
                    AvailableBatches.Add(b);

                // 5. Decision: Auto-Select OR Pop-up
                if (validBatches.Count == 1)
                {
                    // Only one batch -> Auto-select
                    SelectedBatch = validBatches.First();
                }
                else
                {
                    // Multiple batches -> Show Selection Dialog
                    var batchCollection = new ObservableCollection<ProductBatch>(validBatches);

                    // Reuse your existing BatchSelectionViewModel
                    // Note: You can add 'Wastage' to the enum or reuse 'SupplierReturn' if logic is identical
                    var batchVm = new BatchSelectionViewModel(batchCollection, BatchSelectionContext.SupplierReturn);

                    var batchWindow = new BatchSelectionView
                    {
                        DataContext = batchVm,
                        Owner = Application.Current.MainWindow // Center over main app
                    };

                    bool? result = batchWindow.ShowDialog();

                    if (result == true && batchVm.SelectedBatch != null)
                    {
                        SelectedBatch = batchVm.SelectedBatch;
                    }
                    else
                    {
                        // User cancelled dialog -> Reset product
                        SelectedProduct = null;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading batches: {ex.Message}");
                SelectedProduct = null;
            }
        }

        private void AddLine()
        {
            if (!CanAddLine) return;

            // 1. Find Reason Name for display
            var reasonObj = Reasons.FirstOrDefault(r => r.Id == ReasonId);

            // 2. Create Line
            var line = new WastageLine
            {
                ProductId = SelectedProduct.ProductId,
                ProductName = SelectedProduct.ProductName,
                BatchId = SelectedBatch.BatchId,
                WastageReasonId = ReasonId,
                ReasonName = reasonObj?.Reason,
                UnitCost = SelectedBatch.UnitCost,
                Quantity = Convert.ToDecimal(Quantity)
            };

            WastageLines.Add(line);

            // 3. Clear Input for next item
            ClearItemEntry();
            RefreshCommands();
        }
        private void RemoveLine(WastageLine line)
        {
            if (line != null)
            {
                WastageLines.Remove(line);
                RefreshCommands();
            }
        }

        private void ClearItemEntry()
        {
            Barcode = string.Empty;
            SelectedProduct = null;
            SelectedBatch = null;
            Quantity = string.Empty;
            AvailableBatches.Clear();
        }
        private void ClearAll()
        {
            WastageLines.Clear();
            ClearItemEntry();
            Note = string.Empty;
        }

        private async Task SaveWastageAsync()
        {
            try
            {
                var wastage = new Wastage
                {
                    BranchId = _sessionService.BranchId,
                    LocationId = LocationId,
                    WastageDate = WastageDate,
                    Note = Note,
                    CreatedBy = _sessionService.UserId,
                    Lines = WastageLines.ToList()
                };

                await _wastageRepository.CreateWastageAsync(wastage);

                MessageBox.Show("Wastage saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                ClearAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        private async void ExecuteOpenWastageReason(object parameter)
        {
            try
            {
                IsOverlayVisible = true;

                if (_dialogService == null)
                {
                    MessageBox.Show("Dialog Service is not initialized.");
                    return;
                }

                // Open the WastageReason Dialog
                _dialogService.ShowDialog<WastageReasonViewModel>(out var wastageReasonViewModel);

                if (wastageReasonViewModel == null)
                    return;

                // REFRESH REASONS LIST
                // Fetch fresh list from DB to ensure we have the latest (including the one just added/edited)
                await LoadReasonsAsync();

                if (wastageReasonViewModel.AddedReason != null && wastageReasonViewModel.AddedReason.Any())
                {
                    // Now it is safe to call Last()
                    var lastAddedId = wastageReasonViewModel.AddedReason.Last().Id;

                    var newReason = Reasons.FirstOrDefault(r => r.Id == lastAddedId);

                    if (newReason != null)
                        ReasonId = newReason.Id;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening Reason Window: {ex.Message}\n\nStack Trace: {ex.StackTrace}");
            }
            finally
            {
                IsOverlayVisible = false;
            }
        }

        #endregion

        #region EventHandlers
        public event Action RequestQuantityFocus;
        public event Action RequestProductFocus;

        private void FocusQuantity() => RequestQuantityFocus?.Invoke();
        private void FocusProduct() => RequestProductFocus?.Invoke();
        #endregion

        #region Validation
        private void ValidateQuantity()
        {
            ClearErrors(nameof(Quantity));

            if (string.IsNullOrWhiteSpace(Quantity))
            {

                return;
            }

            // 3. Check Format (Regex)
            // Matches "10", "0.5", ".5", but fails "abc" or "10.1234"
            if (!System.Text.RegularExpressions.Regex.IsMatch(Quantity, @"^\d*\.?\d{0,3}$"))
            {
                AddError(nameof(Quantity), "Invalid format (max 3 decimals)");
                return;
            }

            if (decimal.TryParse(Quantity, out decimal currentQty))
            {
                // Check Positive
                if (currentQty <= 0)
                {
                    AddError(nameof(Quantity), "Must be greater than 0");
                }
                // Check Stock
                else if (SelectedBatch != null && currentQty > SelectedBatch.AvailableQuantity)
                {
                    AddError(nameof(Quantity), $"Exceeds Stock (Max: {SelectedBatch.AvailableQuantity})");
                }
            }

        }
        #endregion
    }
}
