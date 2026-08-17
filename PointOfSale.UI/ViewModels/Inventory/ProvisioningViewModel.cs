using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Inventory;
using PointOfSale.Core.Interfaces.Services;
using PointOfSale.Core.Models.Inventory;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Inventory
{
    public class ProvisioningViewModel : BaseViewModel
    {
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IProductRepository _productRepository;
        private readonly IUnitMeasureRepository _unitMeasureRepository;
        private readonly IUOMConversionService _uomConversionService;
        private readonly IUserSessionService _userSessionService;

        private Location _selectedLocation;
        private Product _selectedInputProduct;
        private ProductUnitMeasureOption _selectedUOM;
        private long? _selectedInputBatchId;
        private decimal _inputQty;
        private decimal _inputUnitCost;
        private decimal _totalInputCost;
        private decimal _totalAllocationPercentage;
        private ProvisioningOutputModel _selectedOutputLine;
        private DateTime _filterFromDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        private DateTime _filterToDate = DateTime.Today;
        private string _yieldReportStatus;

        public ProvisioningViewModel(
            IInventoryRepository inventoryRepository,
            IProductRepository productRepository,
            IUnitMeasureRepository unitMeasureRepository,
            IUOMConversionService uomConversionService,
            IUserSessionService userSessionService)
        {
            _inventoryRepository = inventoryRepository ?? throw new ArgumentNullException(nameof(inventoryRepository));
            _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
            _unitMeasureRepository = unitMeasureRepository ?? throw new ArgumentNullException(nameof(unitMeasureRepository));
            _uomConversionService = uomConversionService ?? throw new ArgumentNullException(nameof(uomConversionService));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));

            SelectedBranchId = _userSessionService.BranchId;
            Locations = new ObservableCollection<Location>();
            Products = new ObservableCollection<Product>();
            Units = new ObservableCollection<UnitMeasure>();
            AllowedUOMs = new ObservableCollection<ProductUnitMeasureOption>();
            OutputLines = new ObservableCollection<ProvisioningOutputModel>();
            YieldReportData = new ObservableCollection<ProvisioningYieldModel>();
            OutputLines.CollectionChanged += OutputLinesCollectionChanged;

            AddOutputLineCommand = new RelayCommand(_ => AddOutputLine());
            RemoveOutputLineCommand = new RelayCommand(_ => RemoveSelectedOutputLine(), _ => SelectedOutputLine != null);
            ProcessProvisioningCommand = new AsyncRelayCommand(async _ => await ProcessProvisioningAsync(), _ => CanProcessProvisioning());
            LoadYieldReportCommand = new AsyncRelayCommand(async _ => await LoadYieldReportAsync(), _ => SelectedLocation != null && SelectedLocation.Id > 0);

            _ = LoadDependenciesAsync();
        }

        public ObservableCollection<Location> Locations { get; private set; }
        public ObservableCollection<Product> Products { get; private set; }
        public ObservableCollection<UnitMeasure> Units { get; private set; }
        public ObservableCollection<ProductUnitMeasureOption> AllowedUOMs { get; private set; }
        public ObservableCollection<ProvisioningOutputModel> OutputLines { get; private set; }
        public ObservableCollection<ProvisioningYieldModel> YieldReportData { get; private set; }

        public RelayCommand AddOutputLineCommand { get; }
        public RelayCommand RemoveOutputLineCommand { get; }
        public AsyncRelayCommand ProcessProvisioningCommand { get; }
        public ICommand LoadYieldReportCommand { get; }

        public int SelectedBranchId { get; set; }

        public int SelectedLocationId { get; set; }

        public Location SelectedLocation
        {
            get => _selectedLocation;
            set
            {
                if (SetProperty(ref _selectedLocation, value))
                {
                    SelectedLocationId = value?.Id ?? 0;
                    OnPropertyChanged(nameof(SelectedLocationId));
                    RaiseCommandStates();
                }
            }
        }

        public DateTime FilterFromDate
        {
            get => _filterFromDate;
            set => SetProperty(ref _filterFromDate, value);
        }

        public DateTime FilterToDate
        {
            get => _filterToDate;
            set => SetProperty(ref _filterToDate, value);
        }

        public string YieldReportStatus
        {
            get => _yieldReportStatus;
            private set => SetProperty(ref _yieldReportStatus, value);
        }

        public Product SelectedInputProduct
        {
            get => _selectedInputProduct;
            set
            {
                if (SetProperty(ref _selectedInputProduct, value))
                {
                    if (value != null)
                    {
                        _ = LoadAllowedUOMsAsync(value.ProductId);
                    }
                    else
                    {
                        AllowedUOMs.Clear();
                        SelectedUOM = null;
                    }

                    CalculateTotalInputCost();

                    RaiseCommandStates();
                }
            }
        }

        public ProductUnitMeasureOption SelectedUOM
        {
            get => _selectedUOM;
            set
            {
                if (SetProperty(ref _selectedUOM, value))
                {
                    CalculateTotalInputCost();
                    RaiseCommandStates();
                }
            }
        }

        public ProductUnitMeasureOption SelectedInputUnit
        {
            get => SelectedUOM;
            set => SelectedUOM = value;
        }

        public long? SelectedInputBatchId
        {
            get => _selectedInputBatchId;
            set
            {
                if (SetProperty(ref _selectedInputBatchId, value))
                {
                    RaiseCommandStates();
                }
            }
        }

        public decimal InputQty
        {
            get => _inputQty;
            set
            {
                if (SetProperty(ref _inputQty, value))
                {
                    CalculateTotalInputCost();
                    RaiseCommandStates();
                }
            }
        }

        public decimal InputUnitCost
        {
            get => _inputUnitCost;
            set
            {
                if (SetProperty(ref _inputUnitCost, value))
                {
                    RaiseCommandStates();
                }
            }
        }

        public decimal TotalInputCost
        {
            get => _totalInputCost;
            private set => SetProperty(ref _totalInputCost, value);
        }

        public decimal TotalAllocationPercentage
        {
            get => _totalAllocationPercentage;
            private set => SetProperty(ref _totalAllocationPercentage, value);
        }

        public ProvisioningOutputModel SelectedOutputLine
        {
            get => _selectedOutputLine;
            set
            {
                if (SetProperty(ref _selectedOutputLine, value))
                {
                    RemoveOutputLineCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private async Task LoadDependenciesAsync()
        {
            try
            {
                var locations = await _inventoryRepository.GetLocationsByBranchAsync(SelectedBranchId);
                Locations = new ObservableCollection<Location>(locations);
                OnPropertyChanged(nameof(Locations));
                SelectedLocation = Locations.FirstOrDefault();

                var products = await _productRepository.GetAllAsync();
                Products = new ObservableCollection<Product>(products);
                OnPropertyChanged(nameof(Products));

                var units = await _unitMeasureRepository.GetAllAsync();
                Units = new ObservableCollection<UnitMeasure>(units);
                OnPropertyChanged(nameof(Units));
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to load provisioning dependencies: " + ex.Message;
            }
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
                ErrorMessage = "Failed to load product UOMs: " + ex.Message;
            }
        }

        private void AddOutputLine()
        {
            var line = new ProvisioningOutputModel();
            OutputLines.Add(line);
            SelectedOutputLine = line;
        }

        private void RemoveSelectedOutputLine()
        {
            if (SelectedOutputLine == null)
            {
                return;
            }

            OutputLines.Remove(SelectedOutputLine);
            SelectedOutputLine = OutputLines.FirstOrDefault();
        }

        private async Task ProcessProvisioningAsync()
        {
            try
            {
                if (SelectedInputProduct != null && InputQty > SelectedInputProduct.AvailableQuantity)
                {
                    ShowErrorMessage($"Cannot provision {InputQty:N3}. Only {SelectedInputProduct.AvailableQuantity:N3} available.");
                    return;
                }

                await _inventoryRepository.ProcessItemProvisioningAsync(
                    SelectedBranchId,
                    SelectedLocationId,
                    SelectedInputProduct.ProductId,
                    SelectedUOM.UnitMeasureId,
                    SelectedInputBatchId,
                    InputQty,
                    InputUnitCost,
                    _userSessionService.UserId,
                    OutputLines.ToList());

                ResetForm();
                MessageBox.Show("Item provisioning processed successfully.", "Provisioning", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Provisioning", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowErrorMessage(string message)
        {
            ErrorMessage = message;
            MessageBox.Show(message, "Provisioning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private async Task LoadYieldReportAsync()
        {
            if (SelectedLocation == null || SelectedLocation.Id <= 0)
            {
                MessageBox.Show("Please select a location before loading yield analysis.", "Yield Analysis", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (FilterFromDate.Date > FilterToDate.Date)
            {
                MessageBox.Show("From Date cannot be after To Date.", "Yield Analysis", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                ErrorMessage = string.Empty;
                YieldReportStatus = "Loading yield report...";

                var fromDate = FilterFromDate.Date;
                var toDate = FilterToDate.Date.AddDays(1).AddTicks(-1);

                var data = await _inventoryRepository.GetProvisioningYieldReportAsync(
                    SelectedLocation.Id,
                    fromDate,
                    toDate);

                YieldReportData.Clear();
                foreach (var item in data)
                {
                    item.ConfigureDetailLoader(
                        async provisionNumber => await _inventoryRepository.GetProvisioningYieldDetailsAsync(provisionNumber),
                        ex => MessageBox.Show(ex.Message, "Yield Analysis Details", MessageBoxButton.OK, MessageBoxImage.Error));

                    YieldReportData.Add(item);
                }

                YieldReportStatus = $"{YieldReportData.Count} row(s) loaded for {SelectedLocation.Name} ({fromDate:yyyy-MM-dd HH:mm:ss} to {toDate:yyyy-MM-dd HH:mm:ss}).";

                if (YieldReportData.Count == 0)
                {
                    MessageBox.Show(
                        $"No yield records found for LocationId {SelectedLocation.Id} ({SelectedLocation.Name}) between {fromDate:yyyy-MM-dd HH:mm:ss} and {toDate:yyyy-MM-dd HH:mm:ss}.",
                        "Yield Analysis",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                YieldReportStatus = "Failed to load yield report.";
                MessageBox.Show(ex.Message, "Yield Analysis", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanProcessProvisioning()
        {
            return SelectedBranchId > 0
                && SelectedLocationId > 0
                && SelectedInputProduct != null
                && SelectedUOM != null
                && InputQty > 0
                && InputUnitCost >= 0
                && OutputLines.Any()
                && OutputLines.All(x => x.OutputProductId > 0 && x.OutputUnitId > 0 && x.OutputQty > 0 && x.CostAllocationPercentage >= 0)
                && TotalAllocationPercentage == 100.00m;
        }

        private void OutputLinesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (ProvisioningOutputModel line in e.OldItems)
                {
                    line.PropertyChanged -= OutputLinePropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (ProvisioningOutputModel line in e.NewItems)
                {
                    line.PropertyChanged += OutputLinePropertyChanged;
                    UpdateOutputLineDisplayNames(line);
                }
            }

            RecalculateAllocation();
            RaiseCommandStates();
        }

        private void OutputLinePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var line = sender as ProvisioningOutputModel;
            if (line != null && e.PropertyName == nameof(ProvisioningOutputModel.OutputProductId))
            {
                UpdateOutputLineDisplayNames(line);
                _ = LoadAllowedOutputUOMsAsync(line);
            }

            if (line != null && e.PropertyName == nameof(ProvisioningOutputModel.OutputUnitId))
            {
                UpdateOutputLineDisplayNames(line);
            }

            if (e.PropertyName == nameof(ProvisioningOutputModel.CostAllocationPercentage) || e.PropertyName == nameof(ProvisioningOutputModel.IsWastage))
            {
                RecalculateAllocation();
            }

            RaiseCommandStates();
        }

        private void UpdateOutputLineDisplayNames(ProvisioningOutputModel line)
        {
            var product = Products.FirstOrDefault(x => x.ProductId == line.OutputProductId);
            line.ProductName = product?.ProductName;

            var allowedUnit = line.AllowedUOMs.FirstOrDefault(x => x.UnitMeasureId == line.OutputUnitId);
            if (allowedUnit != null)
            {
                line.UnitName = allowedUnit.DisplayName;
                return;
            }

            var fallbackUnit = Units.FirstOrDefault(x => x.UnitMeasureId == line.OutputUnitId);
            line.UnitName = fallbackUnit?.UnitMeasureName;
        }

        private async Task LoadAllowedOutputUOMsAsync(ProvisioningOutputModel line)
        {
            if (line == null || line.OutputProductId <= 0)
            {
                return;
            }

            try
            {
                line.AllowedUOMs.Clear();

                var units = await _uomConversionService.GetDistinctUOMsForProductAsync(line.OutputProductId);
                foreach (var unit in units)
                {
                    line.AllowedUOMs.Add(unit);
                }

                var defaultUnit = line.AllowedUOMs.FirstOrDefault(unit => unit.IsBaseUnit)
                    ?? line.AllowedUOMs.FirstOrDefault();

                if (defaultUnit != null)
                {
                    line.OutputUnitId = defaultUnit.UnitMeasureId;
                    line.UnitName = defaultUnit.DisplayName;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to load output UOMs: " + ex.Message;
            }
        }

        private void RecalculateAllocation()
        {
            TotalAllocationPercentage = Math.Round(OutputLines.Where(x => !x.IsWastage).Sum(x => x.CostAllocationPercentage), 2);
        }

        private void ResetForm()
        {
            SelectedInputProduct = null;
            SelectedUOM = null;
            AllowedUOMs.Clear();
            SelectedInputBatchId = null;
            InputQty = 0;
            InputUnitCost = 0;
            OutputLines.Clear();
            RecalculateAllocation();
            RaiseCommandStates();
        }

        private void RaiseCommandStates()
        {
            ProcessProvisioningCommand.RaiseCanExecuteChanged();
            RemoveOutputLineCommand.RaiseCanExecuteChanged();
            (LoadYieldReportCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }

        private void CalculateTotalInputCost()
        {
            if (SelectedInputProduct == null || SelectedUOM == null || InputQty <= 0)
            {
                InputUnitCost = SelectedInputProduct != null && SelectedUOM != null
                    ? CalculateDisplayUnitCost(SelectedInputProduct.StandardCost, SelectedUOM.ConversionRate, SelectedUOM.IsMultiply)
                    : 0m;

                TotalInputCost = 0m;
                return;
            }

            var baseQty = CalculateBaseQuantity(InputQty, SelectedUOM.ConversionRate, SelectedUOM.IsMultiply);
            var baseCostPrice = SelectedInputProduct.StandardCost;

            InputUnitCost = CalculateDisplayUnitCost(baseCostPrice, SelectedUOM.ConversionRate, SelectedUOM.IsMultiply);
            TotalInputCost = Math.Round(baseQty * baseCostPrice, 2);
        }

        private static decimal CalculateBaseQuantity(decimal quantity, decimal conversionRate, bool isMultiply)
        {
            if (conversionRate <= 0m)
            {
                throw new InvalidOperationException("UOM conversion rate must be greater than zero.");
            }

            return isMultiply ? quantity * conversionRate : quantity / conversionRate;
        }

        private static decimal CalculateDisplayUnitCost(decimal baseCostPrice, decimal conversionRate, bool isMultiply)
        {
            if (conversionRate <= 0m)
            {
                return 0m;
            }

            var oneSelectedUnitInBase = CalculateBaseQuantity(1m, conversionRate, isMultiply);
            return Math.Round(baseCostPrice * oneSelectedUnitInBase, 2);
        }
    }
}
