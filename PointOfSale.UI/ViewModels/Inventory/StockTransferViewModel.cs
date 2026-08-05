using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CrystalDecisions.CrystalReports.Engine;
using CrystalDecisions.Shared;
using PointOfSale.Core.Interfaces.Repositories.Inventory;
using PointOfSale.Core.Models.Inventory;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;
using PointOfSale.UI.Reports;
using PointOfSale.UI.Views.Inventory;
using PointOfSale.UI.Views.Sales;

namespace PointOfSale.UI.ViewModels.Inventory
{
    public class StockTransferViewModel : BaseViewModel
    {
        private readonly IProductRepository _productRepository;
        private readonly IProductBatchRepository _productBatchRepository;
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IUserSessionService _sessionService;// For saving transfer & loading batches

        public StockTransferViewModel(
                IProductRepository productRepository,
                IInventoryRepository inventoryRepository,
                IProductBatchRepository productBatchRepository,
                IUserSessionService sessionService)
        {
            _productRepository = productRepository;
            _inventoryRepository = inventoryRepository;
            _productBatchRepository = productBatchRepository;
            _sessionService = sessionService;

            // Initialize Collections
            Locations = new ObservableCollection<Location>();
            Products = new ObservableCollection<Product>();
            AvailableBatches = new ObservableCollection<ProductBatch>();
            TransferLines = new ObservableCollection<StockTransferLine>();

            TransferLines.CollectionChanged += (s, e) =>
            {
                RefreshSaveCommand();
            };

            HistoryList = new ObservableCollection<StockTransfer>();

            // Initialize Commands
            SaveTransferCommand = new RelayCommand(async _ => await SaveTransferAsync(), _ => CanSaveTransfer);
            AddLineCommand = new RelayCommand(_ => AddLine(), _ => CanAddLine);
            RemoveLineCommand = new RelayCommand<StockTransferLine>(RemoveLine);
            ClearCommand = new RelayCommand(_ => ClearAll());
            SearchHistoryCommand = new AsyncRelayCommand(async _ => await SearchHistoryAsync());

            // Load Initial Data
            _ = LoadInitialDataAsync();

        }
        private void RefreshSaveCommand() => (SaveTransferCommand as RelayCommand)?.RaiseCanExecuteChanged();
        private void RefreshAddCommand() => (AddLineCommand as RelayCommand)?.RaiseCanExecuteChanged();

        #region Properties - Header

        private DateTime _transferDate = DateTime.Now;
        public DateTime TransferDate
        {
            get => _transferDate;
            set
            {
                if (SetProperty(ref _transferDate, value))
                    RefreshSaveCommand(); // Check save status
            }
        }

        public ObservableCollection<Location> Locations { get; }

        private int _fromLocationId;
        public int FromLocationId
        {
            get => _fromLocationId;
            set
            {
                if (SetProperty(ref _fromLocationId, value))
                {
                    // Logic: Clear lines if source changes
                    if (TransferLines.Any())
                    {
                        TransferLines.Clear();
                        ErrorMessage = "Item list cleared because Source Location changed.";
                    }

                    ClearItemEntry();
                    _ = LoadProducts();

                    // CRITICAL FIX 2: Trigger Save Button Check
                    RefreshSaveCommand();
                }
            }
        }

        private int _toLocationId;
        public int ToLocationId
        {
            get => _toLocationId;
            set
            {
                if (SetProperty(ref _toLocationId, value))
                {
                    // CRITICAL FIX 2: Trigger Save Button Check
                    RefreshSaveCommand();
                }
            }
        }

        private string _note;
        public string Note
        {
            get => _note;
            set => SetProperty(ref _note, value);
        }

        #endregion

        #region Properties

        private string _barcode;
        public string Barcode
        {
            get => _barcode;
            set => SetProperty(ref _barcode, value);
        }

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
                    SelectedBatch = null;

                    if (_selectedProduct != null)
                    {
                        SelectedUnitMeasureName = _selectedProduct.UnitMeasureCode;
                    }
                    else
                    {
                        SelectedUnitMeasureName = null;
                    }

                    OnPropertyChanged(nameof(IsProductSelected));
                    RefreshAddCommand();
                }
            }
        }

        public bool IsProductSelected => SelectedProduct != null;

        private string _selectedUnitMeasureName;
        public string SelectedUnitMeasureName
        {
            get => _selectedUnitMeasureName;
            private set => SetProperty(ref _selectedUnitMeasureName, value);
        }

        private ObservableCollection<ProductBatch> _availableBatches;
        public ObservableCollection<ProductBatch> AvailableBatches
        {
            get => _availableBatches;
            set => SetProperty(ref _availableBatches, value);
        }

        private ProductBatch _selectedBatch;
        public ProductBatch SelectedBatch
        {
            get => _selectedBatch;
            set
            {
                if (SetProperty(ref _selectedBatch, value))
                    RefreshAddCommand();
            }
        }

        private string _transferQuantity;
        public string TransferQuantity
        {
            get => _transferQuantity;
            set
            {
                if (SetProperty(ref _transferQuantity, value))
                {
                    ValidateQuantity();
                    RefreshAddCommand();
                }
            }
        }

        #endregion

        #region Properties - Grid

        public ObservableCollection<StockTransferLine> TransferLines { get; }

        private StockTransferLine _selectedLine;
        public StockTransferLine SelectedLine
        {
            get => _selectedLine;
            set => SetProperty(ref _selectedLine, value);
        }

        #endregion

        #region Properties - History

        private ObservableCollection<StockTransfer> _historyList;
        public ObservableCollection<StockTransfer> HistoryList
        {
            get => _historyList;
            set => SetProperty(ref _historyList, value);
        }

        private DateTime? _historyDateFrom = DateTime.Today.AddDays(-30);
        public DateTime? HistoryDateFrom
        {
            get => _historyDateFrom;
            set => SetProperty(ref _historyDateFrom, value);
        }

        private DateTime? _historyDateTo = DateTime.Today;
        public DateTime? HistoryDateTo
        {
            get => _historyDateTo;
            set => SetProperty(ref _historyDateTo, value);
        }

        #endregion

        #region Commands
        public ICommand SaveTransferCommand { get; }
        public ICommand AddLineCommand { get; }
        public ICommand RemoveLineCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand SearchHistoryCommand { get; }

        public bool CanSaveTransfer => !HasErrors && TransferLines.Any() && FromLocationId > 0 && ToLocationId > 0 && FromLocationId != ToLocationId;
        #endregion

        #region Methods
        private async Task LoadInitialDataAsync()
        {
            var branchId = _sessionService.BranchId;

            var locs = await _inventoryRepository.GetLocationsByBranchAsync(branchId);
            Locations.Clear();
            foreach (var l in locs) Locations.Add(l);
            if (Locations.Any()) FromLocationId = Locations.First().Id;

            await LoadProducts();
        }
        private async Task LoadProducts()
        {
            var allProducts = await _productRepository.GetAllAsync();
            Products = new ObservableCollection<Product>(allProducts);
        }
        public void SearchAndSelectProduct(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                FocusProduct();
                return;
            }

            if (Products == null) return;

            var trimmedCode = code.Trim();

            // Search by Barcode OR ProductCode
            var foundProduct = Products.FirstOrDefault(p =>
                string.Equals(p.Barcode, trimmedCode, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.ProductCode, trimmedCode, StringComparison.OrdinalIgnoreCase));

            if (foundProduct != null)
            {
                SelectedProduct = foundProduct;
                _ = ResolveBatchForTransferAsync();
            }
        }
        public async Task ResolveBatchForTransferAsync()
        {
            // Clear previous selections
            SelectedBatch = null;

            if (SelectedProduct == null || FromLocationId <= 0) return;

            try
            {
                // 1. Fetch Batches for the specific SOURCE location
                var batches = await _productBatchRepository.GetAvailableBatchesAsync(SelectedProduct.ProductId, FromLocationId);

                // 2. Filter out empty batches
                var availableBatches = batches.Where(b => b.AvailableQuantity > 0).ToList();

                if (!availableBatches.Any())
                {
                    MessageBox.Show("No stock available for this product in the selected source location.", "Out of Stock", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ClearItemEntry();
                    FocusBarcode();
                    return;
                }

                // 3. Decide: Auto-Select vs Pop-up
                if (availableBatches.Count == 1)
                {
                    // Single batch found - Auto select
                    SelectedBatch = availableBatches.First();
                }
                else
                {
                    // Multiple batches found - Show Pop-up to force user to choose what they are physically holding
                    var batchCollection = new ObservableCollection<ProductBatch>(availableBatches);

                    // Note: Assuming you have a 'StockTransfer' enum in your BatchSelectionContext
                    var batchVm = new BatchSelectionViewModel(batchCollection, BatchSelectionContext.StockTransfer);

                    var batchWindow = new BatchSelectionView
                    {
                        DataContext = batchVm,
                        Owner = Application.Current.MainWindow // Ensure it centers correctly over the main window
                    };

                    bool? result = batchWindow.ShowDialog();

                    if (result == true && batchVm.SelectedBatch != null)
                    {
                        SelectedBatch = batchVm.SelectedBatch;
                    }
                    else
                    {
                        // User cancelled the pop-up - Clear the product selection
                        SelectedProduct = null;
                    }
                }

                // Optional: If you use an 'AvailableBatches' ComboBox in your UI as a backup, 
                // you can still populate it here:
                // AvailableBatches = new ObservableCollection<ProductBatch>(availableBatches);

                if (SelectedBatch != null)
                    FocusQuantity();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error resolving batches: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public bool CanAddLine => SelectedProduct != null &&
            SelectedBatch != null &&
            !string.IsNullOrWhiteSpace(TransferQuantity) &&
            !HasErrors;
        private void AddLine()
        {
            ValidateLine();
            if (HasErrors) return;

            decimal qtyToAdd = decimal.Parse(TransferQuantity);

            // check if batch already exists in grid
            var existing = TransferLines.FirstOrDefault(x => x.BatchId == SelectedBatch.BatchId);

            if (existing != null)
            {
                // Merge logic
                if ((existing.Quantity + qtyToAdd) > SelectedBatch.AvailableQuantity)
                {
                    MessageBox.Show($"Cannot merge. Total quantity would exceed stock ({SelectedBatch.AvailableQuantity}).");
                    return;
                }
                existing.Quantity += qtyToAdd;
            }
            else
            {
                // Create new line
                var line = new StockTransferLine
                {
                    ProductId = SelectedProduct.ProductId,
                    ProductName = SelectedProduct.ProductName,
                    UnitMeasureCode = SelectedProduct.UnitMeasureCode,
                    BatchId = SelectedBatch.BatchId,
                    ExpiryDate = SelectedBatch.ExpiryDate,
                    UnitCost = SelectedBatch.UnitCost,
                    Quantity = qtyToAdd
                };
                TransferLines.Add(line);
            }

            // Reset Entry fields for next item
            ClearItemEntry();

            // Force command update
            (SaveTransferCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private void RemoveLine(StockTransferLine line)
        {
            if (line != null)
            {
                TransferLines.Remove(line);
            }
        }

        private void ClearItemEntry()
        {
            Barcode = string.Empty;
            SelectedProduct = null;
            SelectedBatch = null;
            TransferQuantity = string.Empty;
            AvailableBatches.Clear();
            ClearAllErrors();
        }
        private async Task SaveTransferAsync()
        {
            if (FromLocationId == ToLocationId)
            {
                ErrorMessage = "Source and Destination locations cannot be the same.";
                return;
            }

            long newTransferId;

            try
            {
                var transfer = new StockTransfer
                {
                    BranchId = _sessionService.BranchId,
                    FromLocationId = FromLocationId,
                    ToLocationId = ToLocationId,
                    TransferDate = TransferDate,
                    Note = Note,
                    CreatedBy = _sessionService.UserId,
                    Lines = TransferLines.ToList()
                };

                newTransferId = await _inventoryRepository.CreateStockTransferAsync(transfer);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Save Failed: {ex.Message}");
                return;
            }

            MessageBox.Show("Transfer Saved Successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            ClearAll();

            try
            {
                await OpenStockTransferReportAsync(newTransferId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Transfer saved, but the report could not be opened: {ex.Message}", "Report Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async Task OpenStockTransferReportAsync(long transferId)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                StockTransferNote reportDocument = null;

                try
                {
                    reportDocument = new StockTransferNote();

                    ApplyLogonCredentials(reportDocument);
                    SetTransferIdParameter(reportDocument, transferId);

                    var previewWindow = new ZReportViewerWindow(reportDocument, disposeReportOnClose: true)
                    {
                        Title = "Stock Transfer Note"
                    };

                    var owner = Application.Current.MainWindow;
                    if (owner != null && owner != previewWindow)
                    {
                        previewWindow.Owner = owner;
                        previewWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    }
                    else
                    {
                        previewWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    }

                    previewWindow.ShowDialog();
                    reportDocument = null;
                }
                finally
                {
                    if (reportDocument != null)
                    {
                        reportDocument.Close();
                        reportDocument.Dispose();
                    }
                }
            });
        }

        private static void SetTransferIdParameter(ReportDocument report, long transferId)
        {
            try
            {
                report.SetParameterValue("@TransferId", transferId);
            }
            catch (ParameterFieldException)
            {
                report.SetParameterValue("TransferId", transferId);
            }
        }

        private async Task SearchHistoryAsync()
        {
            try
            {
                var results = await _inventoryRepository.GetAllStockTransfersAsync(
                    _sessionService.BranchId, HistoryDateFrom, HistoryDateTo);

                HistoryList = new ObservableCollection<StockTransfer>(results);

                if (HistoryList.Count == 0)
                    MessageBox.Show("No records found for the selected date range.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading transfer history: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearAll()
        {
            TransferLines.Clear();
            ClearItemEntry();
            Note = string.Empty;
            ClearAllErrors();
            RefreshSaveCommand();
        }
        private void ApplyLogonCredentials(ReportDocument report)
        {
            // 1. Get your application's connection string
            // (Adjust this line to where you store your connection string)
            string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["CoreConnection"].ConnectionString;

            // 2. Parse the connection string to get the parts
            var builder = new SqlConnectionStringBuilder(connectionString);

            ConnectionInfo myConnectionInfo = new ConnectionInfo();
            myConnectionInfo.ServerName = builder.DataSource;
            myConnectionInfo.DatabaseName = builder.InitialCatalog;

            // Check if using Windows Authentication (Integrated Security)
            if (builder.IntegratedSecurity)
            {
                myConnectionInfo.IntegratedSecurity = true;
            }
            else
            {
                myConnectionInfo.UserID = builder.UserID;
                myConnectionInfo.Password = builder.Password;
            }

            // 3. Apply to ALL tables in the main report
            foreach (Table table in report.Database.Tables)
            {
                TableLogOnInfo logOnInfo = table.LogOnInfo;
                logOnInfo.ConnectionInfo = myConnectionInfo;
                table.ApplyLogOnInfo(logOnInfo);
            }

            // 4. Important: Apply to Subreports (if you have any)
            foreach (ReportDocument subReport in report.Subreports)
            {
                foreach (Table table in subReport.Database.Tables)
                {
                    TableLogOnInfo logOnInfo = table.LogOnInfo;
                    logOnInfo.ConnectionInfo = myConnectionInfo;
                    table.ApplyLogOnInfo(logOnInfo);
                }
            }
        }

        #endregion

        #region Validation
        private void ValidateLine()
        {
            ClearAllErrors();
            if (SelectedBatch == null)
                AddError(nameof(SelectedBatch), "Batch required");

            if (string.IsNullOrWhiteSpace(TransferQuantity))
            {
                AddError(nameof(TransferQuantity), "Quantity is required");
                return;
            }

            if (!decimal.TryParse(TransferQuantity, out decimal qty))
            {
                AddError(nameof(TransferQuantity), "Must be a valid number");
                return;
            }

            // 4. Check Logic (Positive & Stock Limits)
            if (qty <= 0)
            {
                AddError(nameof(TransferQuantity), "Quantity must be > 0");
            }
            else if (SelectedBatch != null && qty > SelectedBatch.AvailableQuantity)
            {
                AddError(nameof(TransferQuantity), $"Insufficient Stock (Max: {SelectedBatch.AvailableQuantity})");
            }
        }
        private void ValidateQuantity()
        {
            // 1. Always start by clearing previous errors for this property
            ClearErrors(nameof(TransferQuantity));

            // 2. Empty Check
            if (string.IsNullOrWhiteSpace(TransferQuantity))
            {
                // Optional: Add error if empty is invalid, or just return if waiting for input
                AddError(nameof(TransferQuantity), "Quantity is required");
                return;
            }

            // 3. Format Check
            if (!System.Text.RegularExpressions.Regex.IsMatch(TransferQuantity, @"^\d*\.?\d{0,3}$"))
            {
                AddError(nameof(TransferQuantity), "Invalid format (max 3 decimals)");
                return;
            }

            // 4. Logic Check
            if (decimal.TryParse(TransferQuantity, out decimal currentQty))
            {
                if (currentQty <= 0)
                {
                    AddError(nameof(TransferQuantity), "Must be > 0");
                }
                else if (SelectedBatch != null && currentQty > SelectedBatch.AvailableQuantity)
                {
                    // This was the error sticking before. Now it will clear if currentQty <= Available
                    AddError(nameof(TransferQuantity), $"Exceeds Stock (Max: {SelectedBatch.AvailableQuantity})");
                }
            }
            else
            {
                AddError(nameof(TransferQuantity), "Invalid number");
            }
        }
        #endregion

        #region EventHandlers
        public event Action RequestQuantityFocus;
        public event Action RequestProductFocus;
        public event Action RequestBarcodeFocus;

        private void FocusQuantity() => RequestQuantityFocus?.Invoke();
        private void FocusProduct() => RequestProductFocus?.Invoke();
        private void FocusBarcode() => RequestBarcodeFocus?.Invoke();
        #endregion
    }
}
