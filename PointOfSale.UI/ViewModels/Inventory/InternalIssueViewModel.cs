using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces;
using PointOfSale.Core.Interfaces.Repositories.Inventory;
using PointOfSale.Core.Interfaces.Repositories.Restaurant;
using PointOfSale.Core.Interfaces.Repositories.System;
using PointOfSale.Core.Models.Inventory;
using PointOfSale.Core.Models.Restaurant;
using PointOfSale.Core.Models.System;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;
using PointOfSale.UI.Reports;
using PointOfSale.UI.Views.Sales;

namespace PointOfSale.UI.ViewModels.Inventory
{
    public class InternalIssueViewModel : BaseViewModel
    {
        internal const string ProductItemType = "PRODUCT";
        internal const string MenuItemItemType = "MENU_ITEM";

        private const string WastageIssueType = "Wastage";
        private const string StaffRecoveryIssueType = "Staff Recovery";

        private readonly IInternalIssueRepository _internalIssueRepository;
        private readonly IStationRepository _stationRepository;
        private readonly IMenuItemRepository _menuItemRepository;
        private readonly IAccountMappingRepository _accountMappingRepository;
        private readonly IProductRepository _productRepository;
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IProductBatchRepository _productBatchRepository;
        private readonly IAccountingRepository _accountingRepository;
        private readonly IUserSessionService _userSessionService;

        public InternalIssueViewModel(
            IInternalIssueRepository internalIssueRepository,
            IStationRepository stationRepository,
            IMenuItemRepository menuItemRepository,
            IAccountMappingRepository accountMappingRepository,
            IProductRepository productRepository,
            IInventoryRepository inventoryRepository,
            IProductBatchRepository productBatchRepository,
            IAccountingRepository accountingRepository,
            IUserSessionService userSessionService)
        {
            _internalIssueRepository = internalIssueRepository ?? throw new ArgumentNullException(nameof(internalIssueRepository));
            _stationRepository = stationRepository ?? throw new ArgumentNullException(nameof(stationRepository));
            _menuItemRepository = menuItemRepository ?? throw new ArgumentNullException(nameof(menuItemRepository));
            _accountMappingRepository = accountMappingRepository ?? throw new ArgumentNullException(nameof(accountMappingRepository));
            _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
            _inventoryRepository = inventoryRepository ?? throw new ArgumentNullException(nameof(inventoryRepository));
            _productBatchRepository = productBatchRepository ?? throw new ArgumentNullException(nameof(productBatchRepository));
            _accountingRepository = accountingRepository ?? throw new ArgumentNullException(nameof(accountingRepository));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));

            Stations = new ObservableCollection<Station>();
            Locations = new ObservableCollection<Location>();
            TargetAccounts = new ObservableCollection<AccountDto>();
            SearchItems = new ObservableCollection<InternalIssueSearchItem>();
            FilteredSearchItems = new ObservableCollection<InternalIssueSearchItem>();
            IssueTypes = new ObservableCollection<string> { "Consumable", WastageIssueType, StaffRecoveryIssueType };
            IssueLines = new ObservableCollection<InternalIssueLineEntry>();
            IssueLines.CollectionChanged += (s, e) => RecalculateTotal();

            AddLineCommand = new AsyncRelayCommand(async _ => await AddLineAsync(), _ => CanAddLine);
            RemoveLineCommand = new RelayCommand<InternalIssueLineEntry>(RemoveLine);
            ProcessIssueCommand = new AsyncRelayCommand(async _ => await ProcessIssueAsync(), _ => CanProcessIssue);
            ClearCommand = new RelayCommand(_ => ClearAll());

            _ = LoadLookupsAsync();
        }

        #region Lookups

        public ObservableCollection<Station> Stations { get; }
        public ObservableCollection<Location> Locations { get; }
        public ObservableCollection<AccountDto> TargetAccounts { get; }
        public ObservableCollection<InternalIssueSearchItem> SearchItems { get; }
        public ObservableCollection<InternalIssueSearchItem> FilteredSearchItems { get; }
        public ObservableCollection<string> IssueTypes { get; }
        public ObservableCollection<InternalIssueLineEntry> IssueLines { get; }

        private int _defaultWastageAccountId;

        #endregion

        #region Header Properties

        private Station _selectedStation;
        public Station SelectedStation
        {
            get => _selectedStation;
            set
            {
                if (SetProperty(ref _selectedStation, value))
                {
                    RefreshProcessIssueCommand();
                }
            }
        }

        private Location _selectedLocation;
        public Location SelectedLocation
        {
            get => _selectedLocation;
            set
            {
                if (SetProperty(ref _selectedLocation, value))
                {
                    _ = RefreshAvailableQtyAsync();
                    RefreshProcessIssueCommand();
                }
            }
        }

        private DateTime _issueDate = DateTime.Today;
        public DateTime IssueDate
        {
            get => _issueDate;
            set => SetProperty(ref _issueDate, value);
        }

        private string _issueType = "Consumable";
        public string IssueType
        {
            get => _issueType;
            set
            {
                if (SetProperty(ref _issueType, value))
                {
                    OnPropertyChanged(nameof(IsTargetAccountRequired));
                    OnPropertyChanged(nameof(IsConsumableIssue));

                    if (IsConsumableIssue)
                    {
                        SelectedTargetAccount = null;

                        if (SelectedStation == null)
                        {
                            SelectedStation = Stations.FirstOrDefault();
                        }
                    }
                    else
                    {
                        SelectedStation = null;

                        if (string.Equals(IssueType, WastageIssueType, StringComparison.OrdinalIgnoreCase) && SelectedTargetAccount == null)
                        {
                            SelectedTargetAccount = TargetAccounts.FirstOrDefault(a => a.Id == _defaultWastageAccountId);
                        }
                    }

                    RefreshProcessIssueCommand();
                }
            }
        }

        public bool IsConsumableIssue =>
            string.Equals(IssueType, "Consumable", StringComparison.OrdinalIgnoreCase);

        public bool IsTargetAccountRequired =>
            string.Equals(IssueType, WastageIssueType, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(IssueType, StaffRecoveryIssueType, StringComparison.OrdinalIgnoreCase);

        private AccountDto _selectedTargetAccount;
        public AccountDto SelectedTargetAccount
        {
            get => _selectedTargetAccount;
            set
            {
                if (SetProperty(ref _selectedTargetAccount, value))
                {
                    RefreshProcessIssueCommand();
                }
            }
        }

        private string _remarks;
        public string Remarks
        {
            get => _remarks;
            set => SetProperty(ref _remarks, value);
        }

        private decimal _totalValue;
        public decimal TotalValue
        {
            get => _totalValue;
            private set => SetProperty(ref _totalValue, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    RefreshProcessIssueCommand();
                }
            }
        }

        #endregion

        #region Add Item Properties

        private string _barcode;
        public string Barcode
        {
            get => _barcode;
            set => SetProperty(ref _barcode, value);
        }

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set => SetProperty(ref _searchText, value);
        }

        private string _selectedItemCategory = ProductItemType;
        public string SelectedItemCategory
        {
            get => _selectedItemCategory;
            set
            {
                if (SetProperty(ref _selectedItemCategory, value))
                {
                    OnPropertyChanged(nameof(IsConsumableProductCategorySelected));
                    OnPropertyChanged(nameof(IsMenuItemCategorySelected));
                    OnPropertyChanged(nameof(SearchItemLabel));
                    ClearItemEntry();
                    RefreshFilteredSearchItems();
                    FocusBarcode();
                }
            }
        }

        public bool IsConsumableProductCategorySelected
        {
            get => string.Equals(SelectedItemCategory, ProductItemType, StringComparison.OrdinalIgnoreCase);
            set
            {
                if (value)
                {
                    SelectedItemCategory = ProductItemType;
                }
            }
        }

        public bool IsMenuItemCategorySelected
        {
            get => string.Equals(SelectedItemCategory, MenuItemItemType, StringComparison.OrdinalIgnoreCase);
            set
            {
                if (value)
                {
                    SelectedItemCategory = MenuItemItemType;
                }
            }
        }

        public string SearchItemLabel =>
            IsMenuItemCategorySelected ? "Select Menu Item:" : "Select Product:";

        private InternalIssueSearchItem _selectedSearchItem;
        public InternalIssueSearchItem SelectedSearchItem
        {
            get => _selectedSearchItem;
            set
            {
                if (SetProperty(ref _selectedSearchItem, value))
                {
                    UnitCost = value?.DefaultUnitCost ?? 0;
                    AvailableQty = null;
                    OnPropertyChanged(nameof(IsItemSelected));
                    _ = RefreshAvailableQtyAsync();
                    RefreshAddLineCommand();
                }
            }
        }

        public bool IsItemSelected => SelectedSearchItem != null;

        private decimal? _availableQty;
        public decimal? AvailableQty
        {
            get => _availableQty;
            private set
            {
                if (SetProperty(ref _availableQty, value))
                {
                    OnPropertyChanged(nameof(AvailableQtyDisplay));
                }
            }
        }

        public string AvailableQtyDisplay =>
            SelectedSearchItem?.ItemType == MenuItemItemType
                ? "N/A"
                : AvailableQty.HasValue ? AvailableQty.Value.ToString("N3") : "-";

        private decimal _unitCost;
        public decimal UnitCost
        {
            get => _unitCost;
            set => SetProperty(ref _unitCost, value);
        }

        private string _issueQty;
        public string IssueQty
        {
            get => _issueQty;
            set
            {
                if (SetProperty(ref _issueQty, value))
                {
                    ValidateQuantity();
                    RefreshAddLineCommand();
                }
            }
        }

        #endregion

        #region Grid Selection

        private InternalIssueLineEntry _selectedLine;
        public InternalIssueLineEntry SelectedLine
        {
            get => _selectedLine;
            set => SetProperty(ref _selectedLine, value);
        }

        #endregion

        #region Commands

        public ICommand AddLineCommand { get; }
        public ICommand RemoveLineCommand { get; }
        public ICommand ProcessIssueCommand { get; }
        public ICommand ClearCommand { get; }

        public bool CanAddLine => SelectedSearchItem != null && !string.IsNullOrWhiteSpace(IssueQty) && !HasErrors;

        public bool CanProcessIssue =>
            !IsBusy &&
            SelectedLocation != null &&
            (!IsConsumableIssue || SelectedStation != null) &&
            IssueLines.Any() &&
            (!IsTargetAccountRequired || SelectedTargetAccount != null);

        private void RefreshAddLineCommand() => (AddLineCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        private void RefreshProcessIssueCommand() => (ProcessIssueCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();

        #endregion

        #region Load Lookups

        private async Task LoadLookupsAsync()
        {
            try
            {
                IsBusy = true;

                var branchId = _userSessionService.BranchId;

                var stations = await _stationRepository.GetAllAsync(branchId);
                var locations = await _inventoryRepository.GetLocationsByBranchAsync(branchId);
                var products = await _productRepository.GetAllAsync();
                var menuItems = await _menuItemRepository.GetAllVariantsForSalesAsync();
                var accounts = await _accountingRepository.GetAccountsAsync();
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

                TargetAccounts.Clear();
                foreach (var account in accounts.Where(a => a != null && a.IsActive && !a.IsHeader)
                                                  .OrderBy(a => a.Code))
                {
                    TargetAccounts.Add(account);
                }

                SearchItems.Clear();
                foreach (var product in products.Where(p => p != null && p.IsActive))
                {
                    SearchItems.Add(new InternalIssueSearchItem
                    {
                        ItemType = ProductItemType,
                        ProductId = product.ProductId,
                        Code = product.ProductCode,
                        Barcode = product.Barcode,
                        DisplayName = product.ProductName,
                        UnitMeasureName = product.UnitMeasureName,
                        DefaultUnitCost = product.StandardCost
                    });
                }

                foreach (var menuItem in menuItems.Where(m => m != null))
                {
                    SearchItems.Add(new InternalIssueSearchItem
                    {
                        ItemType = MenuItemItemType,
                        VariantId = menuItem.VariantId,
                        Code = menuItem.ItemCode,
                        Barcode = menuItem.Barcode,
                        DisplayName = menuItem.DisplayName,
                        UnitMeasureName = "Unit",
                        DefaultUnitCost = 0
                    });
                }
                RefreshFilteredSearchItems();

                _defaultWastageAccountId = accountMappings.TryGetValue(AccountMappingKeys.WastageExpense, out var wastageAccountId)
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

        #endregion

        #region Item Search / Add Line

        public void SearchAndSelectItem(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                FocusSearchItem();
                return;
            }

            var trimmedCode = code.Trim();

            var found = FilteredSearchItems.FirstOrDefault(i =>
                string.Equals(i.Barcode, trimmedCode, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(i.Code, trimmedCode, StringComparison.OrdinalIgnoreCase));

            if (found != null)
            {
                SelectedSearchItem = found;
                FocusQuantity();
            }
        }

        private async Task RefreshAvailableQtyAsync()
        {
            if (SelectedSearchItem == null || SelectedLocation == null)
            {
                AvailableQty = null;
                return;
            }

            if (SelectedSearchItem.ItemType == MenuItemItemType)
            {
                // Menu Items have no direct physical stock — the backend SP explodes the recipe
                // and deducts raw ingredients dynamically, so there is nothing meaningful to show here.
                AvailableQty = null;
                return;
            }

            try
            {
                if (SelectedSearchItem.ItemType == ProductItemType && SelectedSearchItem.ProductId.HasValue)
                {
                    var batches = await _productBatchRepository.GetAvailableBatchesAsync(SelectedSearchItem.ProductId.Value, SelectedLocation.Id);
                    AvailableQty = batches?.Sum(b => b.AvailableQuantity) ?? 0;
                }
            }
            catch (Exception ex)
            {
                AvailableQty = null;
                MessageBox.Show($"Failed to load available stock: {ex.Message}", "Internal Issue", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private Task AddLineAsync()
        {
            ValidateLine();
            if (HasErrors) return Task.CompletedTask;

            var qty = decimal.Parse(IssueQty);

            var existing = IssueLines.FirstOrDefault(l =>
                l.ItemType == SelectedSearchItem.ItemType &&
                l.ProductId == SelectedSearchItem.ProductId &&
                l.VariantId == SelectedSearchItem.VariantId);

            if (existing != null)
            {
                var mergedQty = existing.Qty + qty;
                if (AvailableQty.HasValue && mergedQty > AvailableQty.Value)
                {
                    MessageBox.Show($"Cannot merge. Total quantity would exceed available stock ({AvailableQty.Value:N3}).", "Internal Issue", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return Task.CompletedTask;
                }

                IssueLines.Remove(existing);
                IssueLines.Add(new InternalIssueLineEntry
                {
                    ItemType = existing.ItemType,
                    ProductId = existing.ProductId,
                    VariantId = existing.VariantId,
                    Code = existing.Code,
                    Name = existing.Name,
                    UnitMeasureName = existing.UnitMeasureName,
                    Qty = mergedQty,
                    UnitCost = existing.UnitCost
                });
            }
            else
            {
                IssueLines.Add(new InternalIssueLineEntry
                {
                    ItemType = SelectedSearchItem.ItemType,
                    ProductId = SelectedSearchItem.ProductId,
                    VariantId = SelectedSearchItem.VariantId,
                    Code = SelectedSearchItem.Code,
                    Name = SelectedSearchItem.DisplayName,
                    UnitMeasureName = SelectedSearchItem.UnitMeasureName,
                    Qty = qty,
                    UnitCost = UnitCost
                });
            }

            ClearItemEntry();
            FocusBarcode();
            return Task.CompletedTask;
        }

        private void RemoveLine(InternalIssueLineEntry line)
        {
            if (line != null)
            {
                IssueLines.Remove(line);
            }
        }

        private void ClearItemEntry()
        {
            Barcode = string.Empty;
            SearchText = string.Empty;
            SelectedSearchItem = null;
            UnitCost = 0;
            IssueQty = string.Empty;
            AvailableQty = null;
            ClearAllErrors();
        }

        private void RefreshFilteredSearchItems()
        {
            var selectedItemType = IsMenuItemCategorySelected ? MenuItemItemType : ProductItemType;

            FilteredSearchItems.Clear();
            foreach (var item in SearchItems.Where(i => i != null &&
                                                        string.Equals(i.ItemType, selectedItemType, StringComparison.OrdinalIgnoreCase))
                                            .OrderBy(i => i.DisplayName))
            {
                FilteredSearchItems.Add(item);
            }
        }

        private void RecalculateTotal()
        {
            TotalValue = IssueLines.Where(l => l != null).Sum(l => l.LineTotal);
            RefreshProcessIssueCommand();
        }

        #endregion

        #region Save / Print

        private async Task ProcessIssueAsync()
        {
            try
            {
                if (!IssueLines.Any())
                {
                    MessageBox.Show("Add at least one item with an issue quantity greater than zero.", "Internal Issue", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (IsConsumableIssue && SelectedStation == null)
                {
                    MessageBox.Show("Select a destination station for this issue type.", "Internal Issue", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (IsTargetAccountRequired && SelectedTargetAccount == null)
                {
                    MessageBox.Show("Select a target account for this issue type.", "Internal Issue", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var dto = new InternalIssueSaveDto
                {
                    IssueDate = IssueDate,
                    IssueType = IssueType,
                    StationId = IsConsumableIssue ? SelectedStation.Id : 0,
                    BranchId = _userSessionService.BranchId,
                    LocationId = SelectedLocation.Id,
                    TotalValue = TotalValue,
                    Remarks = string.IsNullOrWhiteSpace(Remarks) ? null : Remarks.Trim(),
                    CreatedBy = _userSessionService.UserId,
                    TargetAccountId = IsTargetAccountRequired ? SelectedTargetAccount?.Id : null,
                    Lines = IssueLines.Select(l => new InternalIssueLineDto
                    {
                        ProductId = l.ItemType == ProductItemType ? l.ProductId : null,
                        VariantId = l.ItemType == MenuItemItemType ? l.VariantId : null,
                        Qty = l.Qty,
                        UnitCost = l.UnitCost,
                        LineTotal = l.LineTotal
                    }).ToList()
                };

                IsBusy = true;
                var result = await _internalIssueRepository.CreateInternalIssueAsync(dto);

                MessageBox.Show($"Internal issue saved successfully. Issue No: {result.IssueNumber}", "Internal Issue", MessageBoxButton.OK, MessageBoxImage.Information);
                ClearAll();

                try
                {
                    await OpenInternalIssueVoucherAsync(result.InternalIssueId);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Issue saved, but the voucher could not be opened: {ex.Message}", "Internal Issue Voucher", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
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

        private async Task OpenInternalIssueVoucherAsync(int internalIssueId)
        {
            if (internalIssueId <= 0)
            {
                throw new InvalidOperationException($"Invalid InternalIssueId returned from save operation: {internalIssueId}.");
            }

            var reportData = await _internalIssueRepository.GetInternalIssueVoucherAsync(internalIssueId);
            if (reportData == null || reportData.Rows.Count == 0)
            {
                throw new Exception("The voucher query returned 0 rows. Check the InternalIssueId parameter.");
            }

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                InternalIssueNote reportDocument = null;

                try
                {
                    reportData.TableName = "uspGetInternalIssueNote";
                    reportDocument = new InternalIssueNote();
                    reportDocument.SetDataSource(reportData);
                    TrySetReportParameter(reportDocument, "InternalIssueId", internalIssueId);

                    var previewWindow = new ZReportViewerWindow(reportDocument, disposeReportOnClose: true)
                    {
                        Title = "Internal Issue Note"
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

        private static void TrySetReportParameter(InternalIssueNote reportDocument, string parameterName, object value)
        {
            try
            {
                reportDocument.SetParameterValue(parameterName, value);
            }
            catch
            {
                // The report may be fully data-source bound and not expose the parameter at runtime.
            }
        }

        private void ClearAll()
        {
            IssueDate = DateTime.Today;
            IssueType = IssueTypes.FirstOrDefault() ?? "Consumable";
            SelectedItemCategory = ProductItemType;
            SelectedTargetAccount = null;
            Remarks = string.Empty;
            IssueLines.Clear();
            ClearItemEntry();
            TotalValue = 0;
            RefreshProcessIssueCommand();
        }

        #endregion

        #region Validation

        private void ValidateLine()
        {
            ClearAllErrors();

            if (SelectedSearchItem == null)
            {
                AddError(nameof(SelectedSearchItem), "Select a product or menu item.");
                return;
            }

            ValidateQuantity();
            if (HasErrors) return;

            if (SelectedSearchItem.ItemType == MenuItemItemType)
            {
                // Menu Items have no direct physical stock — the backend SP explodes the recipe and
                // deducts raw ingredients dynamically, so there is nothing to check against here.
                return;
            }

            var qty = decimal.Parse(IssueQty);
            if (AvailableQty.HasValue && qty > AvailableQty.Value)
            {
                AddError(nameof(IssueQty), $"Insufficient Stock (Requested: {qty:N3}, Available: {AvailableQty.Value:N3})");
            }
        }

        private void ValidateQuantity()
        {
            ClearErrors(nameof(IssueQty));

            if (string.IsNullOrWhiteSpace(IssueQty))
            {
                AddError(nameof(IssueQty), "Quantity is required");
                return;
            }

            if (!Regex.IsMatch(IssueQty, @"^\d*\.?\d{0,3}$"))
            {
                AddError(nameof(IssueQty), "Invalid format (max 3 decimals)");
                return;
            }

            if (decimal.TryParse(IssueQty, out decimal currentQty))
            {
                if (currentQty <= 0)
                {
                    AddError(nameof(IssueQty), "Must be > 0");
                }
            }
            else
            {
                AddError(nameof(IssueQty), "Invalid number");
            }
        }

        #endregion

        #region Focus Events

        public event Action RequestQuantityFocus;
        public event Action RequestSearchItemFocus;
        public event Action RequestBarcodeFocus;

        private void FocusQuantity() => RequestQuantityFocus?.Invoke();
        private void FocusSearchItem() => RequestSearchItemFocus?.Invoke();
        private void FocusBarcode() => RequestBarcodeFocus?.Invoke();

        #endregion
    }

    public class InternalIssueSearchItem
    {
        public string ItemType { get; set; }
        public int? ProductId { get; set; }
        public int? VariantId { get; set; }
        public string Code { get; set; }
        public string Barcode { get; set; }
        public string DisplayName { get; set; }
        public string UnitMeasureName { get; set; }
        public decimal DefaultUnitCost { get; set; }
    }

    public class InternalIssueLineEntry
    {
        public string ItemType { get; set; }
        public int? ProductId { get; set; }
        public int? VariantId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string UnitMeasureName { get; set; }
        public decimal Qty { get; set; }
        public decimal UnitCost { get; set; }
        public decimal LineTotal => Qty * UnitCost;
    }
}
