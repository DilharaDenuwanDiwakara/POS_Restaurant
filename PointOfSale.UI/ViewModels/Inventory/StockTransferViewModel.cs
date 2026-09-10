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
using PointOfSale.Core.Interfaces.Services;
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
        private readonly IUOMConversionService _uomConversionService;
        private readonly IUserSessionService _sessionService;// For saving transfer & loading batches

        public StockTransferViewModel(
                IProductRepository productRepository,
                IInventoryRepository inventoryRepository,
                IProductBatchRepository productBatchRepository,
                IUOMConversionService uomConversionService,
                IUserSessionService sessionService)
        {
            _productRepository = productRepository;
            _inventoryRepository = inventoryRepository;
            _productBatchRepository = productBatchRepository;
            _uomConversionService = uomConversionService ?? throw new ArgumentNullException(nameof(uomConversionService));
            _sessionService = sessionService;

            // Initialize Collections
            Locations = new ObservableCollection<Location>();
            Products = new ObservableCollection<Product>();
            AvailableBatches = new ObservableCollection<ProductBatch>();
            AllowedUOMs = new ObservableCollection<ProductUnitMeasureOption>();
            TransferLines = new ObservableCollection<StockTransferLine>();

            TransferLines.CollectionChanged += (s, e) =>
            {
                RefreshSaveCommand();
            };

            HistoryList = new ObservableCollection<StockTransfer>();

            // Initialize Commands
            SaveTransferCommand = new AsyncRelayCommand(async _ => await SaveTransferAsync(), _ => CanSaveTransfer);
            AddLineCommand = new AsyncRelayCommand(async _ => await AddLineAsync(), _ => CanAddLine);
            RemoveLineCommand = new RelayCommand<StockTransferLine>(RemoveLine);
            ClearCommand = new RelayCommand(_ => ClearAll());
            SearchHistoryCommand = new AsyncRelayCommand(async _ => await SearchStockTransferAsync(null));

            // Load Initial Data
            _ = LoadInitialDataAsync();

        }
        private void RefreshSaveCommand()
        {
            (SaveTransferCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SaveTransferCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
        private void RefreshAddCommand()
        {
            (AddLineCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AddLineCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }

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

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    (SearchHistoryCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }


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
                        _ = LoadAllowedUOMsAsync(_selectedProduct.ProductId);
                    }
                    else
                    {
                        SelectedUnitMeasureName = null;
                        SelectedUOM = null;
                        AllowedUOMs.Clear();
                    }

                    OnPropertyChanged(nameof(IsProductSelected));
                    RefreshAddCommand();
                }
            }
        }

        public bool IsProductSelected => SelectedProduct != null;

        public ObservableCollection<ProductUnitMeasureOption> AllowedUOMs { get; }

        private ProductUnitMeasureOption _selectedUOM;
        public ProductUnitMeasureOption SelectedUOM
        {
            get => _selectedUOM;
            set
            {
                if (SetProperty(ref _selectedUOM, value))
                {
                    SelectedUnitMeasureName = value?.DisplayName;
                    ValidateQuantity();
                    RefreshAddCommand();
                }
            }
        }

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

        private bool _isSavingTransfer;
        public bool IsSavingTransfer
        {
            get => _isSavingTransfer;
            set
            {
                if (SetProperty(ref _isSavingTransfer, value))
                    RefreshSaveCommand();
            }
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

        public bool CanSaveTransfer => !IsSavingTransfer && !HasErrors && TransferLines.Any() && FromLocationId > 0 && ToLocationId > 0 && FromLocationId != ToLocationId;
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
        private async Task LoadAllowedUOMsAsync(int productId)
        {
            try
            {
                AllowedUOMs.Clear();

                var units = await _uomConversionService.GetDistinctUOMsForProductAsync(productId);
                foreach (var unit in units)
                {
                    AllowedUOMs.Add(unit);
                }

                SelectedUOM = AllowedUOMs.FirstOrDefault(unit => unit.IsBaseUnit) ?? AllowedUOMs.FirstOrDefault();
            }
            catch (Exception ex)
            {
                SelectedUOM = null;
                SelectedUnitMeasureName = null;
                MessageBox.Show($"Failed to load product UOMs: {ex.Message}", "Unit Measure", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
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
            SelectedUOM != null &&
            !string.IsNullOrWhiteSpace(TransferQuantity) &&
            !HasErrors;
        private async Task AddLineAsync()
        {
            await ValidateLineAsync();
            if (HasErrors) return;

            decimal qtyToAdd = decimal.Parse(TransferQuantity);
            decimal baseQtyToAdd = await GetBaseQuantityAsync(qtyToAdd, SelectedUOM.UnitMeasureId);

            // check if batch already exists in grid
            var existing = TransferLines.FirstOrDefault(x =>
                x.BatchId == SelectedBatch.BatchId &&
                x.UnitMeasureId == SelectedUOM.UnitMeasureId);

            if (existing != null)
            {
                // Merge logic
                var existingBaseQty = await GetBaseQuantityAsync(existing.Quantity, existing.UnitMeasureId);
                if ((existingBaseQty + baseQtyToAdd) > SelectedBatch.AvailableQuantity)
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
                    UnitMeasureId = SelectedUOM.UnitMeasureId,
                    UnitMeasureCode = SelectedUOM.Code,
                    UnitMeasureName = SelectedUOM.DisplayName,
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
            SelectedUOM = null;
            SelectedUnitMeasureName = null;
            AllowedUOMs.Clear();
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
                IsSavingTransfer = true;
                ErrorMessage = null;

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
            catch (SqlException ex) when (ex.Number == -2)
            {
                ErrorMessage = "Save failed due to network timeout. Please try again.";
                MessageBox.Show(ErrorMessage, "Save Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            catch (SqlException ex)
            {
                ErrorMessage = $"Save failed due to a database error. Please try again. {ex.Message}";
                MessageBox.Show(ErrorMessage, "Save Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            catch (InvalidOperationException ex) when (ex.InnerException is SqlException sqlEx)
            {
                ErrorMessage = GetStockTransferSaveErrorMessage(sqlEx, ex.Message);
                MessageBox.Show(ErrorMessage, "Save Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Save failed. {ex.Message}";
                MessageBox.Show(ErrorMessage, "Save Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            finally
            {
                IsSavingTransfer = false;
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

        private static string GetStockTransferSaveErrorMessage(SqlException sqlException, string fallbackMessage)
        {
            if (sqlException.Number == -2)
                return "Save failed due to network timeout. Please try again.";

            return string.IsNullOrWhiteSpace(fallbackMessage)
                ? "Save failed due to a database error. Please try again."
                : fallbackMessage;
        }

        private async Task OpenStockTransferReportAsync(long transferId)
        {
            if (transferId <= 0)
            {
                throw new InvalidOperationException($"Invalid TransferId returned from save operation: {transferId}.");
            }

            var reportDataTable = await _inventoryRepository.GetStockTransferNoteReportAsync(transferId);
            if (reportDataTable.Rows.Count == 0)
            {
                throw new Exception("The report query returned 0 rows. Check the TransferId parameter.");
            }

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                StockTransferNote reportDocument = null;

                try
                {
                    reportDocument = new StockTransferNote();

                    reportDocument.SetDataSource(reportDataTable);
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

        private async Task SearchStockTransferAsync(object obj)
        {
            try
            {
                IsBusy = true;
                HistoryList.Clear();

                var from = HistoryDateFrom?.Date;
                var to = HistoryDateTo?.Date.AddDays(1).AddSeconds(-1);

                if (_sessionService.BranchId <= 0)
                {
                    ErrorMessage = "Current user branch is not assigned.";
                    return;
                }

                var flatResults = await _inventoryRepository.GetAllStockTransfers(_sessionService.BranchId, from, to);

                var groupedResults = flatResults
                    .GroupBy(t => new
                    {
                        t.Id,
                        t.TransferNumber,
                        t.BranchId,
                        t.FromLocationId,
                        t.FromLocationName,
                        t.ToLocationId,
                        t.ToLocationName,
                        t.TransferDate,
                        t.Note,
                        t.Status,
                        t.CreatedBy,
                        t.Username,
                        t.CreatedDate
                    })
                    .Select(g => new StockTransfer
                    {
                        TransferId = g.Key.Id,
                        TransferNumber = g.Key.TransferNumber,
                        BranchId = g.Key.BranchId,
                        FromLocationId = g.Key.FromLocationId,
                        FromLocationName = g.Key.FromLocationName,
                        ToLocationId = g.Key.ToLocationId,
                        ToLocationName = g.Key.ToLocationName,
                        TransferDate = g.Key.TransferDate,
                        Note = g.Key.Note,
                        Status = g.Key.Status,
                        CreatedBy = g.Key.CreatedBy,
                        Username = g.Key.Username,
                        CreatedDate = g.Key.CreatedDate,
                        IsExpanded = false,
                        Lines = g.Where(line => line.ProductName != null).Select(line => new StockTransferLine
                        {
                            TransferLineId = line.LineId,
                            TransferId = g.Key.Id,
                            ProductId = line.ProductId,
                            ProductName = line.ProductName,
                            ProductCode = line.ProductCode,
                            BatchId = line.BatchId,
                            Quantity = line.Qty,
                            UnitMeasureId = line.UnitMeasureId,
                            UnitMeasureName = line.UnitMeasureName,
                            UnitMeasureCode = line.UnitMeasureCode
                        }).ToList()
                    });

                foreach (var item in groupedResults)
                {
                    HistoryList.Add(item);
                }
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
        private async Task ValidateLineAsync()
        {
            ClearAllErrors();
            if (SelectedBatch == null)
                AddError(nameof(SelectedBatch), "Batch required");

            if (SelectedUOM == null)
                AddError(nameof(SelectedUOM), "UOM required");

            if (SelectedProduct == null || SelectedBatch == null || SelectedUOM == null)
                return;

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
            else if (SelectedProduct != null && SelectedUOM != null && SelectedBatch != null)
            {
                var baseQty = await GetBaseQuantityAsync(qty, SelectedUOM.UnitMeasureId);
                if (baseQty > SelectedBatch.AvailableQuantity)
                {
                    AddError(nameof(TransferQuantity), $"Insufficient Stock (Base Qty: {baseQty:N3}, Max: {SelectedBatch.AvailableQuantity:N3})");
                }
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
            }
            else
            {
                AddError(nameof(TransferQuantity), "Invalid number");
            }
        }
        #endregion

        private async Task<decimal> GetBaseQuantityAsync(decimal quantity, int unitMeasureId)
        {
            var baseUnitMeasureId = await _uomConversionService.GetProductBaseUnitMeasureIdAsync(SelectedProduct.ProductId);

            return await _uomConversionService.GetConvertedQuantityAsync(
                SelectedProduct.ProductId,
                unitMeasureId,
                baseUnitMeasureId,
                quantity);
        }

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
