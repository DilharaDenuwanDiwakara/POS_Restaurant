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
using PointOfSale.Core.Interfaces.Repositories.Inventory;
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
        private readonly IUserSessionService _userSessionService;
        private readonly DispatcherTimer _draftTimer;
        private readonly string _draftFilePath;
        private bool _isLoading;

        public OpeningStockViewModel(
            IStockAdjustmentRepository stockAdjustmentRepository,
            IInventoryRepository inventoryRepository,
            IUserSessionService userSessionService)
        {
            _stockAdjustmentRepository = stockAdjustmentRepository ?? throw new ArgumentNullException(nameof(stockAdjustmentRepository));
            _inventoryRepository = inventoryRepository ?? throw new ArgumentNullException(nameof(inventoryRepository));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));

            Locations = new ObservableCollection<Location>();
            StockItems = new ObservableCollection<OpeningStockItemModel>();

            SaveCommand = new AsyncRelayCommand(async _ => await ExecuteSaveAsync(), _ => CanSave());
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
                var documentNumber = await Task.Run(() =>
                    _inventoryRepository.SaveOpeningStock(
                        SelectedLocation.Id,
                        _userSessionService.UserId,
                        OpeningDate.Value,
                        StockItems.ToList()));

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
