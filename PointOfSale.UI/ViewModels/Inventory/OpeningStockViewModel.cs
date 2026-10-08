using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CrystalDecisions.CrystalReports.Engine;
using Microsoft.Win32;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Inventory;
using PointOfSale.Core.Interfaces.Services;
using PointOfSale.Core.Models.Inventory;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;
using PointOfSale.UI.Views.Sales;

namespace PointOfSale.UI.ViewModels.Inventory
{
    public class OpeningStockViewModel : BaseViewModel
    {
        private const string DraftFolderName = "Nexora";
        private const string DraftFileName = "_OpeningStockDraft.json";

        private readonly IStockAdjustmentRepository _stockAdjustmentRepository;
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IExcelService _excelService;
        private readonly IUserSessionService _userSessionService;
        private readonly DispatcherTimer _draftTimer;
        private readonly string _draftFilePath;
        private bool _isLoading;

        public OpeningStockViewModel(
            IStockAdjustmentRepository stockAdjustmentRepository,
            IInventoryRepository inventoryRepository,
            IExcelService excelService,
            IUserSessionService userSessionService)
        {
            _stockAdjustmentRepository = stockAdjustmentRepository ?? throw new ArgumentNullException(nameof(stockAdjustmentRepository));
            _inventoryRepository = inventoryRepository ?? throw new ArgumentNullException(nameof(inventoryRepository));
            _excelService = excelService ?? throw new ArgumentNullException(nameof(excelService));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));

            Locations = new ObservableCollection<Location>();
            StockItems = new ObservableCollection<OpeningStockItemModel>();

            SaveCommand = new AsyncRelayCommand(async _ => await ExecuteSaveAsync(), _ => CanSave());
            ImportExcelCommand = new AsyncRelayCommand(async _ => await ImportExcelAsync());
            ClearDraftCommand = new RelayCommand(_ => ClearDraft());

            OpeningDate = DateTime.Today;

            _draftFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                DraftFolderName,
                DraftFileName);

            _draftTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            _draftTimer.Tick += (s, e) =>
            {
                _draftTimer.Stop();
                SaveDraft();
            };

            _ = InitializeAsync();
        }

        public ObservableCollection<Location> Locations { get; }

        private Location _selectedLocation;
        public Location SelectedLocation
        {
            get => _selectedLocation;
            set
            {
                if (SetProperty(ref _selectedLocation, value))
                {
                    SaveCommand?.RaiseCanExecuteChanged();
                    _ = RefreshCurrentStockAsync();
                }
            }
        }

        private DateTime? _openingDate;
        public DateTime? OpeningDate
        {
            get => _openingDate;
            set
            {
                if (SetProperty(ref _openingDate, value))
                {
                    SaveCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        private ObservableCollection<OpeningStockItemModel> _stockItems;
        public ObservableCollection<OpeningStockItemModel> StockItems
        {
            get => _stockItems;
            set => SetProperty(ref _stockItems, value);
        }

        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand ImportExcelCommand { get; }
        public RelayCommand ClearDraftCommand { get; }

        private async Task InitializeAsync()
        {
            try
            {
                _isLoading = true;

                var locations = await _inventoryRepository.GetLocationsByBranchAsync(_userSessionService.BranchId);
                Locations.Clear();
                foreach (var location in locations)
                {
                    Locations.Add(location);
                }

                SelectedLocation = Locations.FirstOrDefault();

                var activeProducts = await _inventoryRepository.GetOpeningStockItemsAsync(SelectedLocation?.Id ?? 0);
                var draftItems = LoadDraftItems();
                var draftByProductId = draftItems.ToDictionary(x => x.ProductId);

                foreach (var item in activeProducts)
                {
                    if (draftByProductId.TryGetValue(item.ProductId, out var draftedItem))
                    {
                        item.OpeningQuantity = draftedItem.OpeningQuantity;
                        item.UnitCost = draftedItem.UnitCost;
                    }

                    item.PropertyChanged += StockItem_PropertyChanged;
                    StockItems.Add(item);
                }

                await RefreshCurrentStockAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load opening stock data: {ex.Message}", "Opening Stock", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false;
                SaveCommand.RaiseCanExecuteChanged();
            }
        }

        private async Task RefreshCurrentStockAsync()
        {
            if (_isLoading || SelectedLocation == null || StockItems == null || StockItems.Count == 0)
                return;

            try
            {
                foreach (var item in StockItems)
                {
                    var availableQty = await _stockAdjustmentRepository.GetAvailableQty(SelectedLocation.Id, item.ProductId);
                    item.CurrentStock = availableQty.FirstOrDefault()?.Quantity ?? 0m;
                }

                OnPropertyChanged(nameof(StockItems));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to refresh current stock: {ex.Message}", "Opening Stock", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void StockItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(OpeningStockItemModel.OpeningQuantity) &&
                e.PropertyName != nameof(OpeningStockItemModel.UnitCost))
            {
                return;
            }

            SaveCommand.RaiseCanExecuteChanged();

            if (_isLoading)
                return;

            _draftTimer.Stop();
            _draftTimer.Start();
        }

        private bool CanSave()
        {
            return SelectedLocation != null
                && OpeningDate.HasValue
                && StockItems.Any(x => x.OpeningQuantity > 0);
        }

        private async Task ExecuteSaveAsync()
        {
            if (SelectedLocation == null)
            {
                MessageBox.Show("Please select a location.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!OpeningDate.HasValue)
            {
                MessageBox.Show("Please select an opening date.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var itemsToSave = StockItems
                    .Where(x => x.OpeningQuantity > 0)
                    .Select(x => new OpeningStockItemDto
                    {
                        ProductId = x.ProductId,
                        ProductName = x.ProductName,
                        UnitCost = x.UnitCost,
                        OpeningQuantity = x.OpeningQuantity,
                        Uom = x.DefaultUOM
                    })
                    .ToList();

                var documentNumber = await _inventoryRepository.SaveOpeningStockAsync(
                    SelectedLocation.Id,
                    _userSessionService.UserId,
                    OpeningDate.Value,
                    itemsToSave);

                DeleteDraftFile();
                ClearEnteredQuantities();
                await RefreshCurrentStockAsync();

                MessageBox.Show($"Opening stock saved successfully. Document No: {documentNumber}", "Opening Stock", MessageBoxButton.OK, MessageBoxImage.Information);
                OpenOpeningStockReport(documentNumber);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Save failed: {ex.Message}", "Opening Stock", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SaveCommand.RaiseCanExecuteChanged();
            }
        }

        private async Task ImportExcelAsync()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Import Opening Stock from Excel",
                Filter = "Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                var importedItems = await _excelService.ImportOpeningStockAsync(dialog.FileName, _userSessionService.UserId);
                ApplyImportedItems(importedItems);
                SaveDraft();

                MessageBox.Show(
                    $"{importedItems.Count} opening stock item(s) imported successfully. Please review the grid before saving.",
                    "Opening Stock Import",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Import failed: {ex.Message}", "Opening Stock Import", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SaveCommand.RaiseCanExecuteChanged();
            }
        }

        private void ApplyImportedItems(List<OpeningStockItemDto> importedItems)
        {
            if (importedItems == null || importedItems.Count == 0)
            {
                return;
            }

            _isLoading = true;
            try
            {
                var existingItems = StockItems.ToDictionary(x => x.ProductId);

                foreach (var importedItem in importedItems)
                {
                    if (existingItems.TryGetValue(importedItem.ProductId, out var stockItem))
                    {
                        stockItem.OpeningQuantity = importedItem.OpeningQuantity;
                        stockItem.UnitCost = importedItem.UnitCost;
                        continue;
                    }

                    var newItem = new OpeningStockItemModel
                    {
                        ProductId = importedItem.ProductId,
                        ProductName = importedItem.ProductName,
                        DefaultUOM = importedItem.Uom,
                        CurrentStock = 0m,
                        OpeningQuantity = importedItem.OpeningQuantity,
                        UnitCost = importedItem.UnitCost
                    };

                    newItem.PropertyChanged += StockItem_PropertyChanged;
                    StockItems.Add(newItem);
                    existingItems[importedItem.ProductId] = newItem;
                }
            }
            finally
            {
                _isLoading = false;
            }
        }

        private List<OpeningStockItemModel> LoadDraftItems()
        {
            if (!File.Exists(_draftFilePath))
                return new List<OpeningStockItemModel>();

            try
            {
                var json = File.ReadAllText(_draftFilePath);
                return JsonSerializer.Deserialize<List<OpeningStockItemModel>>(json) ?? new List<OpeningStockItemModel>();
            }
            catch
            {
                return new List<OpeningStockItemModel>();
            }
        }

        private void SaveDraft()
        {
            try
            {
                var folder = Path.GetDirectoryName(_draftFilePath);
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(StockItems, options);
                var tempPath = _draftFilePath + ".tmp";

                File.WriteAllText(tempPath, json);

                if (File.Exists(_draftFilePath))
                {
                    File.Delete(_draftFilePath);
                }

                File.Move(tempPath, _draftFilePath);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Draft could not be saved: {ex.Message}";
            }
        }

        private void ClearDraft()
        {
            if (MessageBox.Show("Discard the local opening stock draft?", "Opening Stock", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            DeleteDraftFile();
            ClearEnteredQuantities();
            SaveCommand.RaiseCanExecuteChanged();
        }

        private void ClearEnteredQuantities()
        {
            _isLoading = true;
            try
            {
                foreach (var item in StockItems)
                {
                    item.OpeningQuantity = 0m;
                    item.UnitCost = 0m;
                }
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void DeleteDraftFile()
        {
            _draftTimer.Stop();

            if (File.Exists(_draftFilePath))
            {
                File.Delete(_draftFilePath);
            }
        }

        private void OpenOpeningStockReport(string documentNumber)
        {
            try
            {
                var reportPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports", "OpeningStockNote.rpt");
                if (!File.Exists(reportPath))
                {
                    MessageBox.Show("Opening stock was saved, but the OpeningStockNote.rpt template was not found.", "Report", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                ReportDocument reportDocument = null;
                try
                {
                    reportDocument = new ReportDocument();
                    reportDocument.Load(reportPath);
                    SetDocumentNumberParameter(reportDocument, documentNumber);

                    var previewWindow = new ZReportViewerWindow(reportDocument, disposeReportOnClose: true)
                    {
                        Title = "Opening Stock"
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
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Opening stock was saved, but the report could not be opened: {ex.Message}", "Report", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static void SetDocumentNumberParameter(ReportDocument report, string documentNumber)
        {
            try
            {
                report.SetParameterValue("@DocumentNumber", documentNumber);
            }
            catch
            {
                report.SetParameterValue("DocumentNumber", documentNumber);
            }
        }
    }
}
