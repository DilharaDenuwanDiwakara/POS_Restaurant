using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Inventory;
using PointOfSale.Core.Interfaces.Services;
using PointOfSale.Core.Models.Inventory;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Inventory
{
    public class BatchProductionViewModel : BaseViewModel
    {
        private readonly IBatchProductionService _batchProductionService;
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IProductRepository _productRepository;
        private readonly IStockAdjustmentRepository _stockAdjustmentRepository;
        private readonly IUserSessionService _userSessionService;
        private readonly IDialogService _dialogService;

        private int _selectedLocationId;
        private int _selectedProductId;
        private string _producedQuantity;
        private bool _isBusy;
        private Product _selectedProduct;
        private DateTime _fromDate;
        private DateTime _toDate;
        private decimal _selectedProductAvailableQuantity;

        public BatchProductionViewModel(
            IBatchProductionService batchProductionService,
            IInventoryRepository inventoryRepository,
            IProductRepository productRepository,
            IStockAdjustmentRepository stockAdjustmentRepository,
            IUserSessionService userSessionService,
            IDialogService dialogService)
        {
            _batchProductionService = batchProductionService ?? throw new ArgumentNullException(nameof(batchProductionService));
            _inventoryRepository = inventoryRepository ?? throw new ArgumentNullException(nameof(inventoryRepository));
            _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
            _stockAdjustmentRepository = stockAdjustmentRepository ?? throw new ArgumentNullException(nameof(stockAdjustmentRepository));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

            _fromDate = DateTime.Today;
            _toDate = DateTime.Today;

            Locations = new ObservableCollection<Location>();
            PrepItems = new ObservableCollection<Product>();
            RecentProductions = new ObservableCollection<BatchProductionHistoryDto>();
            ProcessBatchCommand = new AsyncRelayCommand(async _ => await ProcessBatchAsync(), _ => CanProcessBatch());
            SearchHistoryCommand = new AsyncRelayCommand(async _ => await LoadProductionHistoryAsync(), _ => !IsBusy);

            _ = LoadDependenciesAsync();
        }

        public ObservableCollection<Location> Locations { get; }
        public ObservableCollection<Product> PrepItems { get; }
        public ObservableCollection<BatchProductionHistoryDto> RecentProductions { get; }
        public ICommand ProcessBatchCommand { get; }
        public ICommand SearchHistoryCommand { get; }

        public int SelectedLocationId
        {
            get => _selectedLocationId;
            set
            {
                if (SetProperty(ref _selectedLocationId, value))
                {
                    _ = RefreshSelectedProductAvailableQuantityAsync();
                    RaiseCommandState();
                }
            }
        }

        public int SelectedProductId
        {
            get => _selectedProductId;
            set
            {
                if (SetProperty(ref _selectedProductId, value))
                {
                    if (_selectedProduct == null || _selectedProduct.ProductId != value)
                    {
                        SelectedProduct = PrepItems.FirstOrDefault(product => product.ProductId == value);
                    }

                    RaiseCommandState();
                }
            }
        }

        public Product SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                if (SetProperty(ref _selectedProduct, value))
                {
                    var productId = value?.ProductId ?? 0;
                    if (_selectedProductId != productId)
                    {
                        _selectedProductId = productId;
                        OnPropertyChanged(nameof(SelectedProductId));
                    }

                    OnPropertyChanged(nameof(TargetUomName));
                    _ = RefreshSelectedProductAvailableQuantityAsync();
                    RaiseCommandState();
                }
            }
        }

        public string TargetUomName => SelectedProduct?.UnitMeasureName ?? string.Empty;

        public decimal SelectedProductAvailableQuantity
        {
            get => _selectedProductAvailableQuantity;
            private set => SetProperty(ref _selectedProductAvailableQuantity, value);
        }

        public DateTime FromDate
        {
            get => _fromDate;
            set
            {
                if (SetProperty(ref _fromDate, value))
                {
                    RaiseCommandState();
                }
            }
        }

        public DateTime ToDate
        {
            get => _toDate;
            set
            {
                if (SetProperty(ref _toDate, value))
                {
                    RaiseCommandState();
                }
            }
        }

        public string ProducedQuantity
        {
            get => _producedQuantity;
            set
            {
                if (SetProperty(ref _producedQuantity, value))
                    RaiseCommandState();
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                    RaiseCommandState();
            }
        }

        private async Task LoadDependenciesAsync()
        {
            try
            {
                IsBusy = true;

                var locations = await _inventoryRepository.GetLocationsByBranchAsync(_userSessionService.BranchId);
                Locations.Clear();
                foreach (var location in locations)
                    Locations.Add(location);

                SelectedLocationId = Locations.FirstOrDefault()?.Id ?? 0;

                var products = await _productRepository.GetAllAsync();
                PrepItems.Clear();
                foreach (var product in products.Where(product => product.IsActive).OrderBy(product => product.ProductName))
                    PrepItems.Add(product);

                SelectedProduct = PrepItems.FirstOrDefault(product => product.ProductId == SelectedProductId);
                await RefreshSelectedProductAvailableQuantityAsync();
                await LoadProductionHistoryAsync();
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage(
                    "Failed to load batch production data: " + ex.Message,
                    "Batch Production",
                    DialogMessageType.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ProcessBatchAsync()
        {
            if (!TryGetProducedQuantity(out var producedQty))
            {
                _dialogService.ShowMessage("Enter a valid produced quantity.", "Batch Production", DialogMessageType.Warning);
                return;
            }

            try
            {
                IsBusy = true;

                var request = new BatchProductionRequest
                {
                    OutputProductId = SelectedProductId,
                    ProducedQty = producedQty,
                    BranchId = _userSessionService.BranchId,
                    LocationId = SelectedLocationId,
                    UserId = _userSessionService.UserId
                };

                await _batchProductionService.ProcessBatchAsync(request);

                ProducedQuantity = string.Empty;
                await RefreshSelectedProductAvailableQuantityAsync();
                await LoadProductionHistoryAsync();
                _dialogService.ShowMessage("Batch production processed successfully.", "Batch Production");
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage(
                    "Batch production failed: " + ex.Message,
                    "Batch Production",
                    DialogMessageType.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool CanProcessBatch()
        {
            return !IsBusy &&
                   SelectedLocationId > 0 &&
                   SelectedProductId > 0 &&
                   TryGetProducedQuantity(out var producedQty) &&
                   producedQty > 0m;
        }

        private bool TryGetProducedQuantity(out decimal producedQty)
        {
            var text = (ProducedQuantity ?? string.Empty).Trim();
            return decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out producedQty) ||
                   decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out producedQty);
        }

        private void RaiseCommandState()
        {
            (ProcessBatchCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (SearchHistoryCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }

        private async Task LoadProductionHistoryAsync()
        {
            if (FromDate.Date > ToDate.Date)
            {
                _dialogService.ShowMessage("From Date cannot be later than To Date.", "Batch Production", DialogMessageType.Warning);
                return;
            }

            try
            {
                IsBusy = true;

                var fromDate = FromDate.Date;
                var toDate = ToDate.Date.AddDays(1).AddTicks(-1);
                var history = await _batchProductionService.GetHistoryAsync(fromDate, toDate);

                RecentProductions.Clear();
                foreach (var line in history)
                {
                    RecentProductions.Add(line);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage(
                    "Failed to load production history: " + ex.Message,
                    "Batch Production",
                    DialogMessageType.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task RefreshSelectedProductAvailableQuantityAsync()
        {
            if (SelectedLocationId <= 0 || SelectedProductId <= 0)
            {
                SelectedProductAvailableQuantity = 0m;
                return;
            }

            try
            {
                var availableQuantities = await _stockAdjustmentRepository.GetAvailableQty(SelectedLocationId, SelectedProductId);
                SelectedProductAvailableQuantity = availableQuantities.FirstOrDefault()?.Quantity ?? 0m;
            }
            catch
            {
                SelectedProductAvailableQuantity = 0m;
            }
        }
    }
}
