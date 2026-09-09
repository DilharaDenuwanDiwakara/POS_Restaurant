using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CrystalDecisions.Shared;
using Microsoft.Win32;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Inventory;
using PointOfSale.Core.Interfaces.Services;
using PointOfSale.Core.Models.Inventory;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;
using StockAdjustmentReport = PointOfSale.UI.Reports.StockAdjustment;

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

            Locations = new ObservableCollection<Location>();
            Products = new ObservableCollection<Product>();
            StockAdjustmentLines = new ObservableCollection<StockAdjustmentLine>();
            AdjustmentHistory = new ObservableCollection<StockAdjustmentHeaderDto>();
            AddorDeduct = new ObservableCollection<string> { "ADD", "REDUCE" };

            AddLineCommand = new RelayCommand(_ => AddLine(), _ => CanAddLine);
            RemoveLineCommand = new RelayCommand<StockAdjustmentLine>(RemoveLine);
            ClearCommand = new RelayCommand(_ => ClearAll(), _ => !IsBusy);
            RefreshCommand = new RelayCommand(async _ => await LoadDependenciesAsync(), _ => !IsBusy);
            SaveCommand = new AsyncRelayCommand(async _ => await ExecuteSaveAsync(), _ => CanExecuteSave());
            SearchCommand = new AsyncRelayCommand(async _ => await SearchAdjustmentHistoryAsync(), _ => !IsBusy);

            SelectedAddOrDeduct = AddorDeduct.First();
            _ = LoadDependenciesAsync();
        }

        public ObservableCollection<Location> Locations { get; }
        public ObservableCollection<Product> Products { get; }
        public ObservableCollection<StockAdjustmentLine> StockAdjustmentLines { get; }
        public ObservableCollection<StockAdjustmentHeaderDto> AdjustmentHistory { get; }
        public ObservableCollection<string> AddorDeduct { get; }

        private DateTime _adjustDate = DateTime.Today;
        public DateTime AdjustDate
        {
            get => _adjustDate;
            set => SetProperty(ref _adjustDate, value);
        }

        private Location _selectedLocation;
        public Location SelectedLocation
        {
            get => _selectedLocation;
            set
            {
                if (SetProperty(ref _selectedLocation, value))
                {
                    StockAdjustmentLines.Clear();
                    ClearItemEntry();
                    RefreshCommands();
                }
            }
        }

        private string _note;
        public string Note
        {
            get => _note;
            set
            {
                if (SetProperty(ref _note, value))
                {
                    ValidateNote();
                    RefreshCommands();
                }
            }
        }

        private DateTime _searchDateFrom = DateTime.Today;
        public DateTime SearchDateFrom
        {
            get => _searchDateFrom;
            set => SetProperty(ref _searchDateFrom, value);
        }

        private DateTime _searchDateTo = DateTime.Today;
        public DateTime SearchDateTo
        {
            get => _searchDateTo;
            set => SetProperty(ref _searchDateTo, value);
        }

        private int _historyLocationId;
        public int HistoryLocationId
        {
            get => _historyLocationId;
            set => SetProperty(ref _historyLocationId, value);
        }

        private Product _selectedProduct;
        public Product SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                if (SetProperty(ref _selectedProduct, value))
                {
                    _ = LoadCurrentStockAsync();
                    RefreshCommands();
                }
            }
        }

        private decimal _currentStock;
        public decimal CurrentStock
        {
            get => _currentStock;
            set => SetProperty(ref _currentStock, value);
        }

        private string _selectedAddOrDeduct;
        public string SelectedAddOrDeduct
        {
            get => _selectedAddOrDeduct;
            set
            {
                if (SetProperty(ref _selectedAddOrDeduct, value))
                {
                    RefreshCommands();
                }
            }
        }

        private string _adjustmentQuantity;
        public string AdjustmentQuantity
        {
            get => _adjustmentQuantity;
            set
            {
                if (SetProperty(ref _adjustmentQuantity, value))
                {
                    ValidateQuantity();
                    RefreshCommands();
                }
            }
        }

        private string _lineReason;
        public string LineReason
        {
            get => _lineReason;
            set
            {
                if (SetProperty(ref _lineReason, value))
                {
                    ValidateLineReason();
                    RefreshCommands();
                }
            }
        }

        private StockAdjustmentLine _selectedLine;
        public StockAdjustmentLine SelectedLine
        {
            get => _selectedLine;
            set => SetProperty(ref _selectedLine, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    OnPropertyChanged(nameof(IsOverlayVisible));
                    RefreshCommands();
                }
            }
        }

        public bool IsOverlayVisible
        {
            get => IsBusy;
            set => IsBusy = value;
        }

        public ICommand AddLineCommand { get; }
        public ICommand RemoveLineCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand SearchCommand { get; }
        public AsyncRelayCommand SaveCommand { get; }

        public bool CanAddLine => !IsBusy
            && SelectedLocation != null
            && SelectedProduct != null
            && !string.IsNullOrWhiteSpace(SelectedAddOrDeduct)
            && decimal.TryParse(AdjustmentQuantity, out var quantity)
            && quantity > 0
            && !string.IsNullOrWhiteSpace(LineReason)
            && !HasErrors;

        private bool CanExecuteSave()
        {
            return !IsBusy
                && SelectedLocation != null
                && StockAdjustmentLines.Any()
                && !HasErrors;
        }

        private async Task LoadDependenciesAsync()
        {
            try
            {
                var branchId = _userSessionService.BranchId;
                var locations = await _inventoryRepository.GetLocationsByBranchAsync(branchId);
                var products = await _productRepository.GetAllAsync();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Locations.Clear();
                    foreach (var location in locations)
                    {
                        Locations.Add(location);
                    }

                    Products.Clear();
                    foreach (var product in products)
                    {
                        Products.Add(product);
                    }

                    if (SelectedLocation == null && Locations.Any())
                    {
                        SelectedLocation = Locations.First();
                    }

                    if (HistoryLocationId <= 0 && Locations.Any())
                    {
                        HistoryLocationId = Locations.First().Id;
                    }
                });

                await SearchAdjustmentHistoryAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load stock adjustment data: {ex.Message}", "Stock Adjustment", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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
                CurrentStock = match != null ? match.Quantity : 0;
            }
            catch (Exception ex)
            {
                CurrentStock = 0;
                MessageBox.Show($"Failed to load current stock: {ex.Message}", "Stock Adjustment", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddLine()
        {
            if (!CanAddLine)
                return;

            var inputQuantity = decimal.Parse(AdjustmentQuantity);
            var signedQuantity = string.Equals(SelectedAddOrDeduct, "REDUCE", StringComparison.OrdinalIgnoreCase)
                ? -inputQuantity
                : inputQuantity;

            var existingLine = StockAdjustmentLines.FirstOrDefault(x => x.ProductId == SelectedProduct.ProductId);
            if (existingLine != null)
            {
                MessageBox.Show("This product is already in the adjustment list. Remove the existing line before adding it again.", "Stock Adjustment", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            StockAdjustmentLines.Add(new StockAdjustmentLine
            {
                ProductId = SelectedProduct.ProductId,
                ProductName = SelectedProduct.ProductName,
                ProductCode = SelectedProduct.ProductCode,
                CurrentStock = CurrentStock,
                AdjustmentQuantity = inputQuantity,
                Quantity = signedQuantity,
                Action = SelectedAddOrDeduct,
                Reason = LineReason?.Trim()
            });

            ClearItemEntry();
            RefreshCommands();
        }

        private void RemoveLine(StockAdjustmentLine line)
        {
            if (line == null)
                return;

            StockAdjustmentLines.Remove(line);
            RefreshCommands();
        }

        private async Task ExecuteSaveAsync()
        {
            if (!CanExecuteSave())
                return;

            long stockAdjustmentId = 0;

            try
            {
                IsBusy = true;

                var stockAdjustment = new StockAdjustment
                {
                    BranchId = _userSessionService.BranchId,
                    UserId = _userSessionService.UserId,
                    LocationId = SelectedLocation.Id,
                    AdjustDate = AdjustDate,
                    Reason = Note?.Trim(),
                    Note = Note?.Trim(),
                    Lines = StockAdjustmentLines.ToList()
                };

                stockAdjustmentId = await _stockAdjustmentRepository.CreateAsync(stockAdjustment);
                //MessageBox.Show("Stock adjustment saved successfully.", "Stock Adjustment", MessageBoxButton.OK, MessageBoxImage.Information);
                ClearAll();
                await SearchAdjustmentHistoryAsync();

                try
                {
                    await GenerateStockAdjustmentReportAsync(stockAdjustmentId);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Stock adjustment saved, but the report could not be generated: {ex.Message}", "Stock Adjustment", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                var context = stockAdjustmentId > 0
                    ? "Stock adjustment saved, but report processing failed"
                    : "Failed to save stock adjustment";

                MessageBox.Show($"{context}: {ex.Message}", "Stock Adjustment", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task GenerateStockAdjustmentReportAsync(long stockAdjustmentId)
        {
            if (stockAdjustmentId <= 0)
            {
                throw new InvalidOperationException($"Invalid StockAdjustmentId returned from save operation: {stockAdjustmentId}.");
            }

            var reportData = await _stockAdjustmentRepository.GetStockAdjustmentReportAsync(stockAdjustmentId);
            if (reportData == null || reportData.Rows.Count == 0)
            {
                throw new Exception("The report query returned 0 rows. Check the StockAdjustmentId parameter.");
            }

            var saveDialog = new SaveFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf",
                FileName = $"StockAdjustment_{stockAdjustmentId}_{DateTime.Now:yyyyMMdd_HHmm}.pdf",
                Title = "Save Stock Adjustment Report"
            };

            if (saveDialog.ShowDialog() == true)
            {
                await Task.Run(() => ExportStockAdjustmentReportToDisk(
                    reportData, saveDialog.FileName, stockAdjustmentId));
            }
        }

        private void ExportStockAdjustmentReportToDisk(
            System.Data.DataTable reportData,
            string filePath,
            long stockAdjustmentId)
        {
            using (var reportDocument = new StockAdjustmentReport())
            {
                reportDocument.SetDataSource(reportData);
                TrySetStockAdjustmentIdParameter(reportDocument, stockAdjustmentId);
                reportDocument.ExportToDisk(ExportFormatType.PortableDocFormat, filePath);
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (MessageBox.Show("Report saved. Open now?", "Success", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
                }
            });
        }

        private static void TrySetStockAdjustmentIdParameter(StockAdjustmentReport reportDocument, long stockAdjustmentId)
        {
            try
            {
                reportDocument.SetParameterValue("@StockAdjustmentId", stockAdjustmentId);
            }
            catch
            {
                // The report is already bound to the retrieved data when no runtime parameter is exposed.
            }
        }

        private void ClearItemEntry()
        {
            SelectedProduct = null;
            CurrentStock = 0;
            AdjustmentQuantity = string.Empty;
            LineReason = string.Empty;
            SelectedAddOrDeduct = AddorDeduct.FirstOrDefault();
            ClearErrors(nameof(AdjustmentQuantity));
            ClearErrors(nameof(LineReason));
        }

        private void ClearAll()
        {
            StockAdjustmentLines.Clear();
            Note = string.Empty;
            AdjustDate = DateTime.Today;
            ClearItemEntry();
            ClearAllErrors();
            RefreshCommands();
        }

        private void RefreshCommands()
        {
            (AddLineCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (RemoveLineCommand as RelayCommand<StockAdjustmentLine>)?.RaiseCanExecuteChanged();
            (ClearCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (RefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SearchCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            SaveCommand?.RaiseCanExecuteChanged();
        }

        private async Task SearchAdjustmentHistoryAsync()
        {
            if (SearchDateTo.Date < SearchDateFrom.Date)
            {
                MessageBox.Show("To Date cannot be earlier than From Date.", "Adjustment History", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                IsBusy = true;

                var items = await _stockAdjustmentRepository.GetHistoryAsync(
                    SearchDateFrom,
                    SearchDateTo,
                    HistoryLocationId);

                AdjustmentHistory.Clear();
                foreach (var item in items)
                {
                    AdjustmentHistory.Add(item);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading stock adjustment history: {ex.Message}", "Adjustment History", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ValidateQuantity()
        {
            ClearErrors(nameof(AdjustmentQuantity));

            if (string.IsNullOrWhiteSpace(AdjustmentQuantity))
                return;

            if (!Regex.IsMatch(AdjustmentQuantity, @"^\d*\.?\d{0,3}$"))
            {
                AddError(nameof(AdjustmentQuantity), "Invalid format (max 3 decimals).");
                return;
            }

            if (decimal.TryParse(AdjustmentQuantity, out var quantity) && quantity <= 0)
            {
                AddError(nameof(AdjustmentQuantity), "Quantity must be greater than zero.");
            }
        }

        private void ValidateNote()
        {
            ClearErrors(nameof(Note));

            if (!string.IsNullOrWhiteSpace(Note)
                && !Regex.IsMatch(Note, @"^[-a-zA-Z0-9""%,.&/()\s+\[\]\\]+$"))
            {
                AddError(nameof(Note), "Cannot contain this character.");
            }
        }

        private void ValidateLineReason()
        {
            ClearErrors(nameof(LineReason));

            if (string.IsNullOrWhiteSpace(LineReason))
            {
                AddError(nameof(LineReason), "Line reason is required.");
            }
            else if (!Regex.IsMatch(LineReason, @"^[-a-zA-Z0-9""%,.&/()\s+\[\]\\]+$"))
            {
                AddError(nameof(LineReason), "Cannot contain this character.");
            }
        }
    }
}
