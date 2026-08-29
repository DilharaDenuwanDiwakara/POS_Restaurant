using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Inventory;
using PointOfSale.Core.Interfaces.Repositories.Restaurant;
using PointOfSale.Core.Interfaces.Repositories.System;
using PointOfSale.Core.Models.Inventory;
using PointOfSale.Core.Models.Restaurant;
using PointOfSale.Core.Models.System;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Inventory
{
    public class InternalIssueViewModel : BaseViewModel
    {
        private const string WastageIssueType = "Damage / Wastage";
        internal const string ProductItemType = "PRODUCT";
        internal const string MenuItemItemType = "MENU_ITEM";

        private readonly IInternalIssueRepository _internalIssueRepository;
        private readonly IStationRepository _stationRepository;
        private readonly IMenuItemRepository _menuItemRepository;
        private readonly IAccountMappingRepository _accountMappingRepository;
        private readonly IProductRepository _productRepository;
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IUserSessionService _userSessionService;

        private Station _selectedStation;
        private Location _selectedLocation;
        private DateTime _issueDate = DateTime.Today;
        private string _issueType = "Consumable";
        private string _remarks;
        private decimal _totalValue;
        private int _defaultWastageAccountId;
        private bool _isBusy;

        public InternalIssueViewModel(
            IInternalIssueRepository internalIssueRepository,
            IStationRepository stationRepository,
            IMenuItemRepository menuItemRepository,
            IAccountMappingRepository accountMappingRepository,
            IProductRepository productRepository,
            IInventoryRepository inventoryRepository,
            IUserSessionService userSessionService)
        {
            _internalIssueRepository = internalIssueRepository ?? throw new ArgumentNullException(nameof(internalIssueRepository));
            _stationRepository = stationRepository ?? throw new ArgumentNullException(nameof(stationRepository));
            _menuItemRepository = menuItemRepository ?? throw new ArgumentNullException(nameof(menuItemRepository));
            _accountMappingRepository = accountMappingRepository ?? throw new ArgumentNullException(nameof(accountMappingRepository));
            _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
            _inventoryRepository = inventoryRepository ?? throw new ArgumentNullException(nameof(inventoryRepository));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));

            Stations = new ObservableCollection<Station>();
            Locations = new ObservableCollection<Location>();
            Products = new ObservableCollection<Product>();
            MenuItems = new ObservableCollection<MenuVariantDto>();
            IssueTypes = new ObservableCollection<string> { "Consumable", WastageIssueType };
            IssueLines = new ObservableCollection<InternalIssueLineEntry>();
            IssueLines.CollectionChanged += IssueLines_CollectionChanged;

            SaveCommand = new AsyncRelayCommand(async _ => await SaveAsync(), _ => CanSave());
            ClearCommand = new RelayCommand(_ => ClearForm());

            _ = LoadLookupsAsync();
        }

        public ObservableCollection<Station> Stations { get; }
        public ObservableCollection<Location> Locations { get; }
        public ObservableCollection<Product> Products { get; }
        public ObservableCollection<MenuVariantDto> MenuItems { get; }
        public ObservableCollection<string> IssueTypes { get; }
        public ObservableCollection<InternalIssueLineEntry> IssueLines { get; }

        public Station SelectedStation
        {
            get => _selectedStation;
            set
            {
                if (SetProperty(ref _selectedStation, value))
                {
                    RefreshSaveCommand();
                }
            }
        }

        public Location SelectedLocation
        {
            get => _selectedLocation;
            set
            {
                if (SetProperty(ref _selectedLocation, value))
                {
                    RefreshSaveCommand();
                }
            }
        }

        public DateTime IssueDate
        {
            get => _issueDate;
            set => SetProperty(ref _issueDate, value);
        }

        public string IssueType
        {
            get => _issueType;
            set
            {
                if (SetProperty(ref _issueType, value))
                {
                    RefreshSaveCommand();
                }
            }
        }

        public string Remarks
        {
            get => _remarks;
            set => SetProperty(ref _remarks, value);
        }

        public decimal TotalValue
        {
            get => _totalValue;
            private set => SetProperty(ref _totalValue, value);
        }

        public int DefaultWastageAccountId
        {
            get => _defaultWastageAccountId;
            private set => SetProperty(ref _defaultWastageAccountId, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    RefreshSaveCommand();
                }
            }
        }

        public ICommand SaveCommand { get; }
        public ICommand ClearCommand { get; }

        private async Task LoadLookupsAsync()
        {
            try
            {
                IsBusy = true;

                var stations = await _stationRepository.GetAllAsync(_userSessionService.BranchId);
                var locations = await _inventoryRepository.GetLocationsByBranchAsync(_userSessionService.BranchId);
                var products = await _productRepository.GetAllAsync();
                var menuItems = await _menuItemRepository.GetAllVariantsForSalesAsync();
                var accountMappings = await _accountMappingRepository.GetSystemAccountMappingsAsync();

                Stations.Clear();
                foreach (var station in stations.Where(s => s != null))
                {
                    Stations.Add(station);
                }

                Locations.Clear();
                foreach (var location in locations.Where(l => l != null))
                {
                    Locations.Add(location);
                }

                Products.Clear();
                foreach (var product in products.Where(p => p != null && p.IsActive))
                {
                    Products.Add(product);
                }

                MenuItems.Clear();
                foreach (var menuItem in menuItems.Where(m => m != null))
                {
                    MenuItems.Add(menuItem);
                }

                DefaultWastageAccountId = accountMappings.TryGetValue(AccountMappingKeys.WastageExpense, out var wastageAccountId)
                    ? wastageAccountId ?? 0
                    : 0;

                SelectedStation = Stations.FirstOrDefault();
                SelectedLocation = Locations.FirstOrDefault();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load internal issue lookups: {ex.Message}", "Internal Issue", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool CanSave()
        {
            return !IsBusy
                && SelectedStation != null
                && SelectedLocation != null
                && !string.IsNullOrWhiteSpace(IssueType)
                && GetValidLines().Any();
        }

        private async Task SaveAsync()
        {
            try
            {
                var validLines = GetValidLines().ToList();
                if (!validLines.Any())
                {
                    MessageBox.Show("Add at least one product with an issue quantity greater than zero.", "Internal Issue", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                TotalValue = validLines.Sum(line => line.LineTotal);
                var explodedLines = await BuildExplodedIssueLinesAsync(validLines);
                TotalValue = explodedLines.Sum(line => line.LineTotal);

                var dto = new InternalIssueSaveDto
                {
                    IssueDate = IssueDate,
                    IssueType = IssueType,
                    StationId = SelectedStation.Id,
                    BranchId = _userSessionService.BranchId,
                    LocationId = SelectedLocation.Id,
                    TotalValue = TotalValue,
                    Remarks = string.IsNullOrWhiteSpace(Remarks) ? null : Remarks.Trim(),
                    CreatedBy = _userSessionService.UserId,
                    WastageAccountId = IssueType == WastageIssueType ? DefaultWastageAccountId : (int?)null,
                    Lines = explodedLines
                };

                IsBusy = true;
                var result = await _internalIssueRepository.CreateInternalIssueAsync(dto);

                MessageBox.Show($"Internal issue saved successfully. Issue No: {result.IssueNumber}", "Internal Issue", MessageBoxButton.OK, MessageBoxImage.Information);
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save internal issue: {ex.Message}", "Internal Issue", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ClearForm()
        {
            IssueDate = DateTime.Today;
            IssueType = IssueTypes.FirstOrDefault() ?? "Consumable";
            Remarks = string.Empty;
            IssueLines.Clear();
            TotalValue = 0;
            RefreshSaveCommand();
        }

        private IQueryable<InternalIssueLineEntry> GetValidLines()
        {
            return IssueLines
                .Where(line => line != null && line.Qty > 0 && (line.ProductId > 0 || line.VariantId > 0 || line.MenuItemId > 0))
                .AsQueryable();
        }

        private async Task<List<InternalIssueLineDto>> BuildExplodedIssueLinesAsync(IEnumerable<InternalIssueLineEntry> validLines)
        {
            var masterLines = new List<InternalIssueLineDto>();

            foreach (var line in validLines)
            {
                if (IsMenuItemLine(line))
                {
                    var ingredients = (await _internalIssueRepository
                        .GetRecipeIngredientsForInternalIssueAsync(line.MenuItemId > 0 ? (int?)line.MenuItemId : null,
                            line.VariantId > 0 ? (int?)line.VariantId : null))
                        .ToList();

                    if (!ingredients.Any())
                    {
                        var itemName = string.IsNullOrWhiteSpace(line.MenuItemName) ? "selected menu item" : line.MenuItemName;
                        throw new InvalidOperationException($"Recipe is not configured for {itemName}. Please configure its ingredients before recording wastage.");
                    }

                    foreach (var ingredient in ingredients)
                    {
                        var issueQty = ingredient.QuantityPerItem * line.Qty;
                        if (issueQty <= 0)
                        {
                            throw new InvalidOperationException($"Recipe ingredient '{ingredient.ProductName}' has an invalid quantity.");
                        }

                        masterLines.Add(new InternalIssueLineDto
                        {
                            ProductId = ingredient.ProductId,
                            Qty = issueQty,
                            UnitCost = ingredient.UnitCost,
                            LineTotal = issueQty * ingredient.UnitCost
                        });
                    }

                    continue;
                }

                masterLines.Add(new InternalIssueLineDto
                {
                    ProductId = line.ProductId,
                    Qty = line.Qty,
                    UnitCost = line.UnitCost,
                    LineTotal = line.Qty * line.UnitCost
                });
            }

            return masterLines
                .GroupBy(line => line.ProductId)
                .Select(group =>
                {
                    var qty = group.Sum(line => line.Qty);
                    var unitCost = group.First().UnitCost;
                    return new InternalIssueLineDto
                    {
                        ProductId = group.Key,
                        Qty = qty,
                        UnitCost = unitCost,
                        LineTotal = group.Sum(line => line.LineTotal)
                    };
                })
                .ToList();
        }

        private static bool IsMenuItemLine(InternalIssueLineEntry line)
        {
            return string.Equals(line.ItemType, MenuItemItemType, StringComparison.OrdinalIgnoreCase)
                || line.SelectedMenuItem != null
                || line.VariantId > 0;
        }

        private void IssueLines_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (InternalIssueLineEntry line in e.OldItems)
                {
                    line.PropertyChanged -= IssueLine_PropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (InternalIssueLineEntry line in e.NewItems)
                {
                    line.PropertyChanged += IssueLine_PropertyChanged;
                }
            }

            RecalculateTotal();
        }

        private void IssueLine_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(InternalIssueLineEntry.Qty)
                || e.PropertyName == nameof(InternalIssueLineEntry.UnitCost)
                || e.PropertyName == nameof(InternalIssueLineEntry.LineTotal)
                || e.PropertyName == nameof(InternalIssueLineEntry.SelectedProduct)
                || e.PropertyName == nameof(InternalIssueLineEntry.SelectedMenuItem)
                || e.PropertyName == nameof(InternalIssueLineEntry.ItemType))
            {
                RecalculateTotal();
            }
        }

        private void RecalculateTotal()
        {
            TotalValue = IssueLines.Where(line => line != null).Sum(line => line.LineTotal);
            RefreshSaveCommand();
        }

        private void RefreshSaveCommand()
        {
            (SaveCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }

    }

    public class InternalIssueLineEntry : INotifyPropertyChanged
    {
        private Product _selectedProduct;
        private MenuVariantDto _selectedMenuItem;
        private string _itemType = InternalIssueViewModel.ProductItemType;
        private int _productId;
        private int _menuItemId;
        private int _variantId;
        private string _productName;
        private string _menuItemName;
        private decimal _qty;
        private decimal _unitCost;

        public event PropertyChangedEventHandler PropertyChanged;

        public Product SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                if (Equals(_selectedProduct, value))
                {
                    return;
                }

                _selectedProduct = value;
                ItemType = InternalIssueViewModel.ProductItemType;
                ProductId = value?.ProductId ?? 0;
                ProductName = value?.ProductName;
                UnitCost = value?.StandardCost ?? 0;
                OnPropertyChanged(nameof(SelectedProduct));
            }
        }

        public MenuVariantDto SelectedMenuItem
        {
            get => _selectedMenuItem;
            set
            {
                if (Equals(_selectedMenuItem, value))
                {
                    return;
                }

                _selectedMenuItem = value;
                ItemType = InternalIssueViewModel.MenuItemItemType;
                VariantId = value?.VariantId ?? 0;
                MenuItemName = value?.DisplayName;
                ProductId = 0;
                ProductName = null;
                UnitCost = 0;
                OnPropertyChanged(nameof(SelectedMenuItem));
            }
        }

        public string ItemType
        {
            get => _itemType;
            set => SetProperty(ref _itemType, value);
        }

        public int ProductId
        {
            get => _productId;
            set => SetProperty(ref _productId, value);
        }

        public int MenuItemId
        {
            get => _menuItemId;
            set => SetProperty(ref _menuItemId, value);
        }

        public int VariantId
        {
            get => _variantId;
            set => SetProperty(ref _variantId, value);
        }

        public string ProductName
        {
            get => _productName;
            set => SetProperty(ref _productName, value);
        }

        public string MenuItemName
        {
            get => _menuItemName;
            set => SetProperty(ref _menuItemName, value);
        }

        public decimal Qty
        {
            get => _qty;
            set
            {
                if (SetProperty(ref _qty, value))
                {
                    OnPropertyChanged(nameof(LineTotal));
                }
            }
        }

        public decimal UnitCost
        {
            get => _unitCost;
            set
            {
                if (SetProperty(ref _unitCost, value))
                {
                    OnPropertyChanged(nameof(LineTotal));
                }
            }
        }

        public decimal LineTotal => Qty * UnitCost;

        private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value))
            {
                return false;
            }

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
