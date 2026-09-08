using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using PointOfSale.Core.Interfaces.Purchasing;
using PointOfSale.Core.Interfaces.Repositories.Inventory;
using PointOfSale.Core.Interfaces.Repositories.Purchasing;
using PointOfSale.Core.Models.Inventory;
using PointOfSale.Core.Models.Purchasing;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;
using PointOfSale.UI.ViewModels.Inventory;
using PointOfSale.UI.Views.Inventory;

namespace PointOfSale.UI.ViewModels.Purchasing
{
    public class SupplierReturnViewModel : BaseViewModel
    {
        private readonly ISupplierReturnRepository _supplierReturnRepository;
        private readonly ISupplierRepository _supplierRepository;
        private readonly IProductRepository _productRepository;
        private readonly IProductBatchRepository _productBatchRepository;
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IUserSessionService _userSessionService;
        private OriginalGrnReturnLine _selectedOriginalGrnLine;

        // Renamed for clarity and to store all batches for a single product entry session
        private List<ProductBatch> _currentProductBatches = new List<ProductBatch>();

        public SupplierReturnViewModel(ISupplierReturnRepository supplierReturnRepository,
                                       ISupplierRepository supplierRepository,
                                       IProductRepository productRepository,
                                       IProductBatchRepository productBatchRepository,
                                       IInventoryRepository inventoryRepository,
                                       IUserSessionService userSessionService)
        {
            _supplierReturnRepository = supplierReturnRepository;
            _supplierRepository = supplierRepository;
            _productRepository = productRepository;
            _productBatchRepository = productBatchRepository;
            _inventoryRepository = inventoryRepository;
            _userSessionService = userSessionService;


            // Initialize Commands
            AddLineCommand = new RelayCommand(_ => OnAddLine(), _ => CanAddLine());
            DeleteItemCommand = new RelayCommand(_ => OnDeleteItem(), _ => CanDeleteItem());
            SaveSupplierReturnCommand = new AsyncRelayCommand(async _ => await OnSaveSupplierReturnAsync(), _ => CanSaveSupplierReturn());
            NewSupplierReturnCommand = new RelayCommand(_ => OnNewSupplierReturn());
            SearchCommand = new AsyncRelayCommand(async _ => await SearchReturnAsync());

            OnNewSupplierReturn();

            // Load dependencies
            _ = LoadDependenciesAsync();
            _ = LoadSuppliers();
            _ = LoadReturnReasonsAsync();
        }

        #region Properties - Header

        public ObservableCollection<Location> Locations { get; } = new ObservableCollection<Location>();
        public ObservableCollection<ReturnReasonModel> ReturnReasons { get; } = new ObservableCollection<ReturnReasonModel>();

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    (SearchCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        private int _locationId;
        public int LocationId
        {
            get => _locationId;
            set
            {
                if (SetProperty(ref _locationId, value))
                {
                    // Clear grid if location changes (Stock is location-specific)
                    SupplierReturnLines.Clear();
                    ClearItemEntryFields(fullClear: true);
                    CalculateTotals();
                }
            }
        }

        private DateTime _returnDate = DateTime.Now;
        public DateTime ReturnDate
        {
            get => _returnDate;
            set => SetProperty(ref _returnDate, value);
        }

        private ObservableCollection<Supplier> _suppliers;
        public ObservableCollection<Supplier> Suppliers
        {
            get => _suppliers;
            private set => SetProperty(ref _suppliers, value);
        }

        private Supplier _selectedSupplier;
        public Supplier SelectedSupplier
        {
            get => _selectedSupplier;
            set
            {
                if (SetProperty(ref _selectedSupplier, value))
                {
                    SelectedSupplierId = value?.SupplierId ?? 0;
                    SelectedSupplierName = value?.SupplierName;
                    RefreshCommands();
                }
            }
        }

        private int _selectedSupplierId;
        public int SelectedSupplierId
        {
            get => _selectedSupplierId;
            set => SetProperty(ref _selectedSupplierId, value);
        }

        private string _selectedSupplierName;
        public string SelectedSupplierName
        {
            get => _selectedSupplierName;
            set => SetProperty(ref _selectedSupplierName, value);
        }

        private int _selectedLineReturnReasonId;
        public int SelectedLineReturnReasonId
        {
            get => _selectedLineReturnReasonId;
            set
            {
                if (SetProperty(ref _selectedLineReturnReasonId, value))
                {
                    ValidateLineReturnReason();
                    RefreshCommands();
                }
            }
        }

        private string _returnedBy;
        public string ReturnedBy
        {
            get => _returnedBy;
            set
            {
                SetProperty(ref _returnedBy, value);
                ValidateReturnBy();
                RefreshCommands();
            }
        }

        private string _note;
        public string Note
        {
            get => _note;
            set
            {
                SetProperty(ref _note, value);
                ValidateNote();
            }
        }

        #endregion

        #region Properties - Item Entry

        public ObservableCollection<Product> Products { get; } = new ObservableCollection<Product>();

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
                        SelectedUnitMeasureName = value.UnitMeasureCode;
                        _ = UpdateBatchAndPricingForSelectedProductAsync();
                    }
                    else
                    {
                        SelectedUnitMeasureName = null;
                        ClearItemEntryFields(fullClear: false);
                    }

                    RefreshCommands();
                }
            }
        }

        private string _selectedUnitMeasureName;
        public string SelectedUnitMeasureName
        {
            get => _selectedUnitMeasureName;
            private set => SetProperty(ref _selectedUnitMeasureName, value);
        }

        private ProductBatch _selectedBatch;
        public ProductBatch SelectedBatch
        {
            get => _selectedBatch;
            set
            {
                if (SetProperty(ref _selectedBatch, value))
                {
                    _selectedOriginalGrnLine = null;

                    if (value != null)
                    {
                        _ = LoadOriginalGrnValuesForSelectedBatchAsync(value.BatchId);

                        if (Suppliers != null)
                        {
                            // Find the supplier object in the list that matches the Batch's SupplierId
                            var matchingSupplier = Suppliers.FirstOrDefault(s => s.SupplierId == value.SupplierId);

                            // Set it (This triggers the UI ComboBox to update)
                            if (matchingSupplier != null)
                            {
                                SelectedSupplier = matchingSupplier;
                            }
                        }
                    }

                    RefreshCommands();
                }
            }
        }

        private decimal _quantity = 1;
        public decimal Quantity
        {
            get => _quantity;
            set
            {
                SetProperty(ref _quantity, value);
                ValidateQuantity();
                RefreshCommands();
            }
        }

        private decimal _unitPrice;
        public decimal UnitPrice
        {
            get => _unitPrice;
            set
            {
                SetProperty(ref _unitPrice, value);
                RefreshCommands();
            }
        }

        private string _barcode;
        public string Barcode
        {
            get => _barcode;
            set
            {
                if (SetProperty(ref _barcode, value))
                    FilterProductByCodeOrBarcode(value);
            }
        }

        #endregion

        #region Properties - Grid & Footer

        private ObservableCollection<SupplierReturnLine> _supplierReturnLines;
        public ObservableCollection<SupplierReturnLine> SupplierReturnLines
        {
            get => _supplierReturnLines;
            set => SetProperty(ref _supplierReturnLines, value);
        }

        private SupplierReturnLine _selectedItem;
        public SupplierReturnLine SelectedItem
        {
            get => _selectedItem;
            set => SetProperty(ref _selectedItem, value);
        }

        private decimal _netAmount;
        public decimal SubTotal
        {
            get => _subTotal;
            set => SetProperty(ref _subTotal, value);
        }
        private decimal _subTotal;

        private decimal _discountAmount;
        public decimal DiscountAmount
        {
            get => _discountAmount;
            set => SetProperty(ref _discountAmount, value);
        }

        private decimal _taxAmount;
        public decimal TaxAmount
        {
            get => _taxAmount;
            set => SetProperty(ref _taxAmount, value);
        }

        public decimal NetAmount
        {
            get => _netAmount;
            set => SetProperty(ref _netAmount, value);
        }

        #endregion

        #region Search Properties (Simple Implementation)
        private SupplierReturnModel _selectedHistoryReturn;
        public SupplierReturnModel SelectedHistoryReturn
        {
            get => _selectedHistoryReturn;
            set { _selectedHistoryReturn = value; OnPropertyChanged(); }
        }

        private ObservableCollection<SupplierReturnModel> _historyList = new ObservableCollection<SupplierReturnModel>();
        public ObservableCollection<SupplierReturnModel> HistoryList
        {
            get => _historyList;
            set => SetProperty(ref _historyList, value);
        }

        private decimal _totalNetAmount;
        public decimal TotalNetAmount
        {
            get => _totalNetAmount;
            set => SetProperty(ref _totalNetAmount, value);
        }

        private ObservableCollection<SupplierReturnLine> _supplierReturnLine;

        public ObservableCollection<SupplierReturnLine> SupplierReturnLine
        {
            get => _supplierReturnLine;
            set => SetProperty(ref _supplierReturnLine, value);
        }

        private ObservableCollection<Supplier> _searchSuppliers;
        public ObservableCollection<Supplier> SearchSuppliers
        {
            get => _searchSuppliers;
            private set => SetProperty(ref _searchSuppliers, value);
        }
        public int FilterSupplierId { get; set; } = -1;
        public DateTime? SearchDateFrom { get; set; } = DateTime.Today.AddDays(-30);
        public DateTime? SearchDateTo { get; set; } = DateTime.Today;
        #endregion

        #region Commands
        public RelayCommand AddLineCommand { get; }
        public RelayCommand DeleteItemCommand { get; }
        public AsyncRelayCommand SaveSupplierReturnCommand { get; }
        public RelayCommand NewSupplierReturnCommand { get; }
        public ICommand SearchCommand { get; }
        #endregion

        #region Core Logic
        private void RefreshCommands()
        {
            AddLineCommand?.RaiseCanExecuteChanged();
            DeleteItemCommand?.RaiseCanExecuteChanged();
            SaveSupplierReturnCommand?.RaiseCanExecuteChanged();
        }
        private async Task LoadDependenciesAsync()
        {
            try
            {
                var locs = await _inventoryRepository.GetLocationsByBranchAsync(_userSessionService.BranchId);


                // 3. Load Products
                var products = await _productRepository.GetAllAsync();

                // 4. Update UI on the Main Thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // Update Locations
                    Locations.Clear();
                    foreach (var l in locs) Locations.Add(l);

                    // Set Default Location
                    if (Locations.Any()) LocationId = Locations.First().Id;

                    // Update Products
                    Products.Clear();
                    foreach (var p in products) Products.Add(p);

                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading data: {ex.Message}");
            }
        }

        private async Task LoadSuppliers()
        {
            var supplierList = await _supplierRepository.GetAllAsync();

            Suppliers = new ObservableCollection<Supplier>(supplierList);

            var searchList = new List<Supplier>(supplierList);

            searchList.Insert(0, new Supplier
            {
                SupplierId = -1,
                SupplierName = "ALL SUPPLIERS"
            });

            SearchSuppliers = new ObservableCollection<Supplier>(searchList);

            FilterSupplierId = -1;
        }

        private async Task LoadReturnReasonsAsync()
        {
            try
            {
                var reasons = await _supplierReturnRepository.GetActiveReturnReasonsAsync();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    ReturnReasons.Clear();
                    foreach (var reason in reasons) ReturnReasons.Add(reason);

                    if (ReturnReasons.Any() && SelectedLineReturnReasonId <= 0)
                    {
                        SelectedLineReturnReasonId = ReturnReasons.First().Id;
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading return reasons: {ex.Message}");
            }
        }

        private async Task LoadOriginalGrnValuesForSelectedBatchAsync(long batchId)
        {
            try
            {
                var originalLine = await _supplierReturnRepository.GetOriginalGrnLineForBatchAsync(batchId);
                if (SelectedBatch == null || SelectedBatch.BatchId != batchId)
                {
                    return;
                }

                if (originalLine == null || originalLine.OriginalQuantity <= 0)
                {
                    UnitPrice = 0m;
                    _selectedOriginalGrnLine = null;
                    MessageBox.Show("Original GRN pricing, discount, and tax details were not found for the selected batch.");
                    RefreshCommands();
                    return;
                }

                _selectedOriginalGrnLine = originalLine;
                UnitPrice = originalLine.UnitPrice;
                RefreshCommands();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading original GRN values: {ex.Message}");
            }
        }

        private async Task UpdateBatchAndPricingForSelectedProductAsync()
        {
            if (SelectedProduct == null || LocationId <= 0) return;

            try
            {
                // 1. Fetch Batches
                var batches = await _productBatchRepository.GetAvailableBatchesAsync(SelectedProduct.ProductId, LocationId);

                // 2. Filter available
                _currentProductBatches = batches.Where(b => b.AvailableQuantity > 0).ToList();

                if (!_currentProductBatches.Any())
                {
                    MessageBox.Show("No stock available for this product in the selected location.");
                    SelectedProduct = null; // Reset selection
                    return;
                }

                // 3. Decide: Auto-Select vs Pop-up
                if (_currentProductBatches.Count == 1)
                {
                    // Single batch found - Auto select
                    SelectedBatch = _currentProductBatches.First();
                }
                else
                {
                    // Multiple batches found - Show Pop-up
                    var batchCollection = new ObservableCollection<ProductBatch>(_currentProductBatches);
                    var batchVm = new BatchSelectionViewModel(batchCollection, BatchSelectionContext.SupplierReturn);

                    var batchWindow = new BatchSelectionView
                    {
                        DataContext = batchVm,
                        Owner = Application.Current.MainWindow // Ensure it centers correctly
                    };

                    bool? result = batchWindow.ShowDialog();

                    if (result == true && batchVm.SelectedBatch != null)
                    {
                        SelectedBatch = batchVm.SelectedBatch;
                    }
                    else
                    {
                        // User cancelled - Clear selection
                        SelectedProduct = null;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading batches: {ex.Message}");
            }
        }
        private bool CanAddLine()
        {
            return SelectedProduct != null &&
                   SelectedBatch != null &&
                   _selectedOriginalGrnLine != null &&
                   SelectedLineReturnReasonId > 0 &&
                   Quantity > 0 &&
                   UnitPrice > 0 &&
                   !HasErrors;
        }
        private void OnAddLine()
        {
            if (!CanAddLine()) return;

            // 1. Validate against cached stock
            var trackingBatch = _currentProductBatches.FirstOrDefault(b => b.BatchId == SelectedBatch.BatchId);

            if (trackingBatch == null || Quantity > trackingBatch.AvailableQuantity)
            {
                MessageBox.Show($"Insufficient stock! Available: {trackingBatch?.AvailableQuantity ?? 0}");
                return;
            }

            // 2. Add or Merge to Grid
            var existingLine = SupplierReturnLines.FirstOrDefault(l => l.BatchId == SelectedBatch.BatchId);
            if (existingLine != null)
            {
                existingLine.Quantity += Quantity;
                if (string.IsNullOrWhiteSpace(existingLine.UnitMeasure))
                {
                    existingLine.UnitMeasure = SelectedProduct.UnitMeasureCode;
                }
            }
            else
            {
                var line = new SupplierReturnLine
                {
                    ProductId = SelectedProduct.ProductId,
                    ProductName = SelectedProduct.ProductName,
                    UnitMeasure = SelectedProduct.UnitMeasureCode,
                    BatchId = SelectedBatch.BatchId,
                    ReturnReasonId = SelectedLineReturnReasonId,
                    ReturnReasonDescription = ReturnReasons.FirstOrDefault(r => r.Id == SelectedLineReturnReasonId)?.Description,
                    Quantity = Quantity,
                };
                line.SetOriginalGrnValues(_selectedOriginalGrnLine);
                SupplierReturnLines.Add(line);
            }

            // 3. Deduct from local cache
            trackingBatch.AvailableQuantity -= Quantity;

            CalculateTotals();
            ClearItemEntryFields(fullClear: false);
            RefreshCommands();
        }
        private bool CanDeleteItem() => SelectedItem != null;
        private void OnDeleteItem()
        {
            if (SelectedItem == null) return;

            // Restore stock to local cache
            var trackingBatch = _currentProductBatches.FirstOrDefault(b => b.BatchId == SelectedItem.BatchId);
            if (trackingBatch != null) trackingBatch.AvailableQuantity += SelectedItem.Quantity;

            SupplierReturnLines.Remove(SelectedItem);
            CalculateTotals();
            RefreshCommands();
        }
        private void OnNewSupplierReturn()
        {
            SupplierReturnLines = new ObservableCollection<SupplierReturnLine>();
            _currentProductBatches.Clear();
            ClearItemEntryFields(fullClear: true);

            SelectedSupplier = null;
            ReturnedBy = string.Empty;
            Note = string.Empty;
            ReturnDate = DateTime.Now;
            SubTotal = 0;
            DiscountAmount = 0;
            TaxAmount = 0;
            NetAmount = 0;

            ClearAllValidationErrors();
            RefreshCommands();
        }
        private void ClearItemEntryFields(bool fullClear)
        {
            SelectedBatch = null;
            _selectedOriginalGrnLine = null;
            Quantity = 1;
            UnitPrice = 0.00m;
            SelectedUnitMeasureName = null;
            if (ReturnReasons.Any()) SelectedLineReturnReasonId = ReturnReasons.First().Id;
            Barcode = string.Empty;
            if (fullClear) SelectedProduct = null;
        }
        private void CalculateTotals()
        {
            SubTotal = SupplierReturnLines.Sum(l => l.LineSubTotal);
            DiscountAmount = SupplierReturnLines.Sum(l => l.LineDiscount);
            TaxAmount = SupplierReturnLines.Sum(l => l.TaxAmount);
            NetAmount = SubTotal - DiscountAmount + TaxAmount;
        }
        private void FilterProductByCodeOrBarcode(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return;

            if (Products == null) return;

            var product = Products?.FirstOrDefault(p =>
                string.Equals(p.Barcode, Barcode.Trim(), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.ProductCode, Barcode.Trim(), StringComparison.OrdinalIgnoreCase));

            if (product != null)
            {
                SelectedProduct = product;
                FocusQuantity();
            }
        }

        // Save Logic
        private bool CanSaveSupplierReturn() => SelectedSupplierId > 0 &&
                                                SupplierReturnLines.Any() &&
                                                !string.IsNullOrWhiteSpace(ReturnedBy);

        private async Task OnSaveSupplierReturnAsync()
        {
            try
            {
                var doc = new SupplierReturn
                {
                    BranchId = _userSessionService.BranchId,
                    LocationId = LocationId,
                    SupplierId = SelectedSupplierId,
                    ReturnedBy = ReturnedBy,
                    Note = Note,
                    ReturnDate = ReturnDate,
                    SubTotal = SubTotal,
                    DiscountAmount = DiscountAmount,
                    TaxAmount = TaxAmount,
                    NetAmount = NetAmount,
                    CreatedBy = _userSessionService.UserId,
                    Lines = SupplierReturnLines.ToList()
                };

                await _supplierReturnRepository.CreateAsync(doc);
                MessageBox.Show("Supplier return saved for approval.", "Suplier Return", MessageBoxButton.OK, MessageBoxImage.Information);
                OnNewSupplierReturn();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Save Failed: {ex.Message}", "Supplier Return", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Search Logic
        private async Task SearchReturnAsync(object obj = null)
        {
            try
            {
                IsBusy = true;
                ErrorMessage = string.Empty;
                HistoryList.Clear();

                var from = SearchDateFrom?.Date ?? DateTime.Today;
                var to = (SearchDateTo?.Date ?? DateTime.Today).AddDays(1).AddSeconds(-1);

                if (_userSessionService.BranchId <= 0)
                {
                    ErrorMessage = "Current user branch is not assigned.";
                    return;
                }

                int supplierId = FilterSupplierId > 0 ? FilterSupplierId : 0;

                var flatResults = await _supplierReturnRepository.GetSupplierReturnsAsync(from, to, supplierId);

                if (flatResults != null)
                {
                    var groupedResults = flatResults
                        .GroupBy(item => new
                        {
                            item.SupplierReturnId,
                            item.ReturnNumber,
                            item.ReturnDate,
                            item.SupplierName,
                            item.ReturnedBy,
                            item.NetAmount
                        })
                        .Select(group => new SupplierReturnModel(
                            group.Key.SupplierReturnId,
                            group.Key.ReturnNumber,
                            group.Key.ReturnDate,
                            string.Join(", ", group
                                .Select(line => line.InvoiceNo)
                                .Where(invoiceNo => !string.IsNullOrWhiteSpace(invoiceNo))
                                .Distinct()),
                            string.Join(", ", group
                                .Where(line => line.InvoiceDate.HasValue)
                                .Select(line => line.InvoiceDate.Value.ToString("dd/MM/yyyy"))
                                .Distinct()),
                            group.Key.SupplierName,
                            group.Key.ReturnedBy,
                            group.Key.NetAmount,
                            group));

                    foreach (var historyModel in groupedResults)
                    {
                        HistoryList.Add(historyModel);
                    }
                }

                TotalNetAmount = HistoryList.Sum(x => x.NetAmount);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        #endregion

        #region Validation
        private void ClearAllValidationErrors()
        {
            ClearErrors(nameof(ReturnedBy));
            ClearErrors(nameof(Quantity));
            ClearErrors(nameof(Note));
            ClearErrors(nameof(SelectedLineReturnReasonId));
        }
        private void ValidateReturnBy()
        {
            ClearErrors(nameof(ReturnedBy));
            if (string.IsNullOrWhiteSpace(ReturnedBy)) AddError(nameof(ReturnedBy), "Required.");
            else if (!Regex.IsMatch(ReturnedBy, @"^[a-zA-Z\s]+$")) AddError(nameof(ReturnedBy), "Invalid characters.");
        }
        private void ValidateNote()
        {
            ClearErrors(nameof(Note));
            if (!string.IsNullOrEmpty(Note) && !Regex.IsMatch(Note, @"^[a-zA-Z0-9\s.,-]*$")) AddError(nameof(Note), "Invalid characters.");
        }
        private void ValidateLineReturnReason()
        {
            ClearErrors(nameof(SelectedLineReturnReasonId));
            if (SelectedLineReturnReasonId <= 0) AddError(nameof(SelectedLineReturnReasonId), "Required.");
        }
        private void ValidateQuantity()
        {
            ClearErrors(nameof(Quantity));
            if (Quantity <= 0) AddError(nameof(Quantity), "Must be > 0.");
            else if (SelectedBatch != null && Quantity > SelectedBatch.AvailableQuantity) AddError(nameof(Quantity), $"Only {SelectedBatch.AvailableQuantity} available.");
        }
        #endregion

        #region EventHandlers
        public event Action RequestQuantityFocus;
        public event Action RequestProductFocus;

        private void FocusQuantity() => RequestQuantityFocus?.Invoke();
        private void FocusProduct() => RequestProductFocus?.Invoke();
        #endregion
    }
}
