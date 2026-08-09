using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using PointOfSale.Core.Interfaces.Repositories.Inventory;
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
        private readonly IUserSessionService _userSessionService;

        private Location _selectedLocation;
        private Product _selectedInputProduct;
        private UnitMeasure _selectedInputUnit;
        private long? _selectedInputBatchId;
        private decimal _inputQty;
        private decimal _inputUnitCost;
        private decimal _totalAllocationPercentage;
        private ProvisioningOutputModel _selectedOutputLine;

        public ProvisioningViewModel(
            IInventoryRepository inventoryRepository,
            IProductRepository productRepository,
            IUnitMeasureRepository unitMeasureRepository,
            IUserSessionService userSessionService)
        {
            _inventoryRepository = inventoryRepository ?? throw new ArgumentNullException(nameof(inventoryRepository));
            _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
            _unitMeasureRepository = unitMeasureRepository ?? throw new ArgumentNullException(nameof(unitMeasureRepository));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));

            SelectedBranchId = _userSessionService.BranchId;
            Locations = new ObservableCollection<Location>();
            Products = new ObservableCollection<Product>();
            Units = new ObservableCollection<UnitMeasure>();
            OutputLines = new ObservableCollection<ProvisioningOutputModel>();
            OutputLines.CollectionChanged += OutputLinesCollectionChanged;

            AddOutputLineCommand = new RelayCommand(_ => AddOutputLine());
            RemoveOutputLineCommand = new RelayCommand(_ => RemoveSelectedOutputLine(), _ => SelectedOutputLine != null);
            ProcessProvisioningCommand = new AsyncRelayCommand(async _ => await ProcessProvisioningAsync(), _ => CanProcessProvisioning());

            _ = LoadDependenciesAsync();
        }

        public ObservableCollection<Location> Locations { get; private set; }
        public ObservableCollection<Product> Products { get; private set; }
        public ObservableCollection<UnitMeasure> Units { get; private set; }
        public ObservableCollection<ProvisioningOutputModel> OutputLines { get; private set; }

        public RelayCommand AddOutputLineCommand { get; }
        public RelayCommand RemoveOutputLineCommand { get; }
        public AsyncRelayCommand ProcessProvisioningCommand { get; }

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

        public Product SelectedInputProduct
        {
            get => _selectedInputProduct;
            set
            {
                if (SetProperty(ref _selectedInputProduct, value))
                {
                    if (value != null)
                    {
                        SelectedInputUnit = Units.FirstOrDefault(x => x.UnitMeasureId == value.UnitMeasureId);
                        if (InputUnitCost <= 0)
                        {
                            InputUnitCost = Math.Round(value.StandardCost, 2);
                        }
                    }

                    RaiseCommandStates();
                }
            }
        }

        public UnitMeasure SelectedInputUnit
        {
            get => _selectedInputUnit;
            set
            {
                if (SetProperty(ref _selectedInputUnit, value))
                {
                    RaiseCommandStates();
                }
            }
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
                    OnPropertyChanged(nameof(TotalInputCost));
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
                    OnPropertyChanged(nameof(TotalInputCost));
                    RaiseCommandStates();
                }
            }
        }

        public decimal TotalInputCost => Math.Round(InputQty * InputUnitCost, 2);

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
                await _inventoryRepository.ProcessItemProvisioningAsync(
                    SelectedBranchId,
                    SelectedLocationId,
                    SelectedInputProduct.ProductId,
                    SelectedInputUnit.UnitMeasureId,
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

        private bool CanProcessProvisioning()
        {
            return SelectedBranchId > 0
                && SelectedLocationId > 0
                && SelectedInputProduct != null
                && SelectedInputUnit != null
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
            if (line != null && (e.PropertyName == nameof(ProvisioningOutputModel.OutputProductId) || e.PropertyName == nameof(ProvisioningOutputModel.OutputUnitId)))
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

            var unit = Units.FirstOrDefault(x => x.UnitMeasureId == line.OutputUnitId);
            line.UnitName = unit?.UnitMeasureName;
        }

        private void RecalculateAllocation()
        {
            TotalAllocationPercentage = Math.Round(OutputLines.Where(x => !x.IsWastage).Sum(x => x.CostAllocationPercentage), 2);
        }

        private void ResetForm()
        {
            SelectedInputProduct = null;
            SelectedInputUnit = null;
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
        }
    }
}
