using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using PointOfSale.Core.Enums;
using PointOfSale.Core.Interfaces.Purchasing;
using PointOfSale.Core.Interfaces.Repositories.Inventory;
using PointOfSale.Core.Interfaces.Repositories.Purchasing;
using PointOfSale.Core.Interfaces.Repositories.System;
using PointOfSale.Core.Models.Inventory;
using PointOfSale.Core.Models.Purchasing;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;
using PointOfSale.UI.DataSets;
using PointOfSale.UI.Reports;
using PointOfSale.UI.Views.Sales;

namespace PointOfSale.UI.ViewModels.Purchasing
{
    public class GoodsPurchaseNoteViewModel : BaseViewModel
    {
        private readonly ISupplierRepository _supplierRepository;
        private readonly IProductRepository _productRepository;
        private readonly IGoodsPurchaseNoteRepository _goodsPurchaseNoteRepository;
        private readonly IGoodsReceiveNoteRepository _goodsReceiveNoteRepository;
        private readonly ITaxConfigurationRepository _taxConfigurationRepository;
        private readonly IUserSessionService _userSessionService;
        private readonly DispatcherTimer _draftAutoSaveTimer;
        private bool _isResettingPurchaseOrder;
        private bool _isDraftAutoSaveInProgress;
        private bool _draftAutoSaveQueuedWhileSaving;
        private bool _isCloneFromRejectedPurchaseOrder;
        private decimal _inputTaxRate;

        private decimal _lastGrnCostPrice;
        public decimal LastGrnCostPrice
        {
            get => _lastGrnCostPrice;
            private set => SetProperty(ref _lastGrnCostPrice, value);
        }

        public GoodsPurchaseNoteViewModel(ISupplierRepository supplierRepository,
                                         IProductRepository productRepository,
                                         IGoodsPurchaseNoteRepository goodsPurchaseNoteRepository,
                                         IGoodsReceiveNoteRepository goodsReceiveNoteRepository,
                                         ITaxConfigurationRepository taxConfigurationRepository,
                                         IUserSessionService userSessionService)
        {
            _supplierRepository = supplierRepository;
            _productRepository = productRepository;
            _goodsPurchaseNoteRepository = goodsPurchaseNoteRepository;
            _goodsReceiveNoteRepository = goodsReceiveNoteRepository;
            _taxConfigurationRepository = taxConfigurationRepository;
            _userSessionService = userSessionService;

            GoodsPurchaseNoteLines = new ObservableCollection<GoodsPurchaseNoteLine>();
            GoodsPurchaseNoteLines.CollectionChanged += GoodsPurchaseNoteLines_CollectionChanged;

            _draftAutoSaveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _draftAutoSaveTimer.Tick += DraftAutoSaveTimer_Tick;

            AddLineCommand = new RelayCommand(_ => AddLineItem(), _ => CanAddItem);
            SavePOCommand = new AsyncRelayCommand(async _ => await CreateGoodsPurchaseNoteAsync(), _ => CanSavePO);
            NewPOCommand = new RelayCommand(_ => CreateNewPO());
            SearchCommand = new AsyncRelayCommand(async _ => await SearchPOsAsync());
            LoadDraftPOCommand = new AsyncRelayCommand(async parameter => await LoadDraftPurchaseOrderAsync(GetPurchaseOrder(parameter)));
            DeleteDraftPOCommand = new AsyncRelayCommand(async parameter => await DeleteDraftPurchaseOrderAsync(GetPurchaseOrder(parameter)));
            PrintPurchaseOrderCommand = new AsyncRelayCommand(async parameter => await PrintPurchaseOrderAsync(parameter));
            RemoveLineCommand = new RelayCommand<GoodsPurchaseNoteLine>(RemoveLineItem);

            this.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SelectedSupplier) ||
                    e.PropertyName == nameof(SelectedProduct) ||
                    e.PropertyName == nameof(Quantity) ||
                    e.PropertyName == nameof(UnitPrice) ||
                    e.PropertyName == nameof(BillDiscount) ||
                    e.PropertyName == nameof(HasErrors))
                {
                    (AddLineCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (SavePOCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                }

                if (IsDraftHeaderProperty(e.PropertyName))
                {
                    ScheduleDraftAutoSave();
                }
            };

            _ = LoadTaxRateAsync();
            _ = LoadSuppliers();
            _ = LoadProducts();
        }

        #region Properties
        private ObservableCollection<Supplier> _suppliers;
        public ObservableCollection<Supplier> Suppliers
        {
            get => _suppliers;
            private set => SetProperty(ref _suppliers, value);
        }

        private ObservableCollection<Product> _products;
        public ObservableCollection<Product> Products
        {
            get => _products;
            private set => SetProperty(ref _products, value);
        }

        private ObservableCollection<Supplier> _searchSuppliers;
        public ObservableCollection<Supplier> SearchSuppliers
        {
            get => _searchSuppliers;
            private set => SetProperty(ref _searchSuppliers, value);
        }

        private ObservableCollection<PurchaseOrderSummaryModel> _historyList;
        public ObservableCollection<PurchaseOrderSummaryModel> HistoryList
        {
            get => _historyList;
            set => SetProperty(ref _historyList, value);
        }

        private int _currentOrderId;
        public int CurrentOrderId
        {
            get => _currentOrderId;
            set => SetProperty(ref _currentOrderId, value);
        }

        private int _selectedTabIndex;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetProperty(ref _selectedTabIndex, value);
        }

        private int _filterSupplierId;
        public int FilterSupplierId
        {
            get => _filterSupplierId;
            set => SetProperty(ref _filterSupplierId, value);
        }

        private DateTime? _searchDateFrom = DateTime.Today.AddDays(-30); // Default to last 30 days
        public DateTime? SearchDateFrom
        {
            get => _searchDateFrom;
            set => SetProperty(ref _searchDateFrom, value);
        }

        private DateTime? _searchDateTo = DateTime.Today;
        public DateTime? SearchDateTo
        {
            get => _searchDateTo;
            set => SetProperty(ref _searchDateTo, value);
        }

        public IEnumerable<PurchaseOrderStatus?> FilterStatuses { get; } =
            new PurchaseOrderStatus?[] { null }
                .Concat(Enum.GetValues(typeof(PurchaseOrderStatus))
                    .Cast<PurchaseOrderStatus>()
                    .Select(status => (PurchaseOrderStatus?)status));

        private PurchaseOrderStatus? _filterStatus;
        public PurchaseOrderStatus? FilterStatus
        {
            get => _filterStatus;
            set => SetProperty(ref _filterStatus, value);
        }

        #region PO Header

        private Supplier _selectedSupplier;
        public Supplier SelectedSupplier
        {
            get => _selectedSupplier;
            set
            {
                SetProperty(ref _selectedSupplier, value);
                CalculateTotals();
                (SavePOCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        private DateTime _purchaseDate = DateTime.Today;
        public DateTime PurchaseDate
        {
            get => _purchaseDate;
            set => SetProperty(ref _purchaseDate, value);
        }

        private DateTime? _expectedDeliveryDate;
        public DateTime? ExpectedDeliveryDate
        {
            get => _expectedDeliveryDate;
            set => SetProperty(ref _expectedDeliveryDate, value);
        }

        private string _orderBy;
        public string OrderBy
        {
            get => _orderBy;
            set
            {
                SetProperty(ref _orderBy, value);
                ValidateOrderBy();
                (SavePOCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        private string _note;
        public string Note
        {
            get => _note;
            set => SetProperty(ref _note, value);
        }
        #endregion

        #region PO Lines

        private string _barcode;
        public string Barcode
        {
            get => _barcode;
            set
            {
                if (SetProperty(ref _barcode, value))
                    FindProductByBarcode(value);
            }
        }

        private Product _selectedProduct;
        public Product SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                SetProperty(ref _selectedProduct, value);
                ValidateSelectedProduct();

                if (_selectedProduct != null)
                {
                    UnitPrice = _selectedProduct.StandardCost;
                    SelectedUnitMeasureName = _selectedProduct.UnitMeasureCode;
                    ClearErrors(nameof(UnitPrice));

                    _ = LoadLastGrnCostPriceAsync(_selectedProduct.ProductId);
                }
                else
                {
                    LastGrnCostPrice = 0m;
                    SelectedUnitMeasureName = null;
                }

                // Only update AddLineCommand, not SaveGRN
                (AddLineCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        private ICollectionView _filteredProducts;
        public ICollectionView FilteredProducts
        {
            get => _filteredProducts;
            set => SetProperty(ref _filteredProducts, value);
        }

        private string _selectedUnitMeasureName;
        public string SelectedUnitMeasureName
        {
            get => _selectedUnitMeasureName;
            private set => SetProperty(ref _selectedUnitMeasureName, value);
        }

        private string _quantity;
        public string Quantity
        {
            get => _quantity;
            set
            {
                SetProperty(ref _quantity, value);
                ValidateQuantity();
                (AddLineCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        private decimal _unitPrice;
        public decimal UnitPrice
        {
            get => _unitPrice;
            set
            {
                SetProperty(ref _unitPrice, value);
                ValidateUnitPrice();
                (AddLineCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public ObservableCollection<GoodsPurchaseNoteLine> GoodsPurchaseNoteLines { get; set; }
        #endregion

        #region Summary

        private decimal _subTotal;
        public decimal SubTotal
        {
            get => _subTotal;
            private set => SetProperty(ref _subTotal, value);
        }

        private decimal _discountAmount;
        public decimal DiscountAmount
        {
            get => _discountAmount;
            private set => SetProperty(ref _discountAmount, value);
        }

        private decimal _billDiscount;
        public decimal BillDiscount
        {
            get => _billDiscount;
            set
            {
                if (SetProperty(ref _billDiscount, value))
                {
                    CalculateTotals();
                }
            }
        }

        private decimal _taxAmount;
        public decimal TaxAmount
        {
            get => _taxAmount;
            private set => SetProperty(ref _taxAmount, value);
        }

        private decimal _netAmount;
        public decimal NetAmount
        {
            get => _netAmount;
            private set => SetProperty(ref _netAmount, value);

        }

        #endregion

        #endregion

        #region Command
        public ICommand AddLineCommand { get; set; }
        public ICommand SavePOCommand { get; set; }
        public ICommand NewPOCommand { get; }
        public ICommand SearchCommand { get; set; }
        public ICommand LoadDraftPOCommand { get; }
        public ICommand DeleteDraftPOCommand { get; }
        public ICommand PrintPurchaseOrderCommand { get; }
        public ICommand RemoveLineCommand { get; }
        public ICommand SearchBarcodeCommand { get; }
        #endregion

        #region Methods
        private async Task LoadLastGrnCostPriceAsync(int productId)
        {
            LastGrnCostPrice = await _goodsReceiveNoteRepository.GetLastGrnCostPriceByProductIdAsync(productId);
            if (LastGrnCostPrice > 0)
            {
                UnitPrice = LastGrnCostPrice;
                ClearErrors(nameof(UnitPrice));
            }
        }

        private async Task LoadSuppliers()
        {
            var supplierList = await _supplierRepository.GetAllAsync();
            Suppliers = new ObservableCollection<Supplier>(supplierList);

            var searchList = new List<Supplier>(supplierList);

            searchList.Insert(0, new Supplier
            {
                SupplierId = -1,
                SupplierName = "ALL SUPPLIERS"
            });

            SearchSuppliers = new ObservableCollection<Supplier>(searchList);

            FilterSupplierId = -1;
        }
        private async Task LoadProducts()
        {
            var allProducts = await _productRepository.GetAllAsync();

            var purchasableProducts = allProducts
                .Where(p => p.IsPurchasable && p.IsActive)
                .OrderBy(p => p.ProductName);

            Products = new ObservableCollection<Product>(purchasableProducts);
            FilteredProducts = CollectionViewSource.GetDefaultView(Products);
        }
        private async Task SearchPOsAsync()
        {
            try
            {
                int? supplierId = (FilterSupplierId == -1) ? (int?)null : FilterSupplierId;

                var results = await _goodsPurchaseNoteRepository.GetAllAsync(_userSessionService.BranchId, supplierId, SearchDateFrom, SearchDateTo);

                if (FilterStatus.HasValue)
                {
                    results = results.Where(x =>
                        string.Equals(x.Status, FilterStatus.Value.ToString(), StringComparison.OrdinalIgnoreCase));
                }

                HistoryList = new ObservableCollection<PurchaseOrderSummaryModel>(
                    results.Select(po => new PurchaseOrderSummaryModel(
                        po,
                        _goodsPurchaseNoteRepository.GetPOLinesAsync,
                        ex => MessageBox.Show($"Unable to load PO line items: {ex.Message}", "Order History", MessageBoxButton.OK, MessageBoxImage.Error))));
                (LoadDraftPOCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                (DeleteDraftPOCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();

                if (HistoryList.Count == 0)
                {
                    MessageBox.Show("No records found for these filters.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading history: {ex.Message}");
            }
        }

        private void FindProductByBarcode(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return;
            }

            if (Products == null) return;

            var trimmedInput = input.Trim();

            // Search by Barcode OR ProductCode
            var foundProduct = Products.FirstOrDefault(p =>
                string.Equals(p.Barcode, trimmedInput, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.ProductCode, trimmedInput, StringComparison.OrdinalIgnoreCase));

            if (foundProduct != null)
            {
                SelectedProduct = foundProduct;
                FocusQuantity();
            }
        }
        private bool CanAddItem => !HasErrors &&
            SelectedProduct != null &&
            UnitPrice > 0 &&
            !string.IsNullOrEmpty(Quantity);
        private void AddLineItem()
        {
            ValidateItem();

            if (HasErrors) return;

            var quantity = Convert.ToDecimal(Quantity);
            ShowStockLimitWarning(quantity);

            var existingLine = GoodsPurchaseNoteLines.FirstOrDefault(line => line.ProductId == SelectedProduct.ProductId);

            if (existingLine != null)
            {
                existingLine.QuantityOrdered += quantity;
                existingLine.UnitPrice = UnitPrice;
                existingLine.LastGrnCostPrice = LastGrnCostPrice;
                existingLine.IsTaxApplicable = SelectedProduct.IsTaxApplicable;
            }
            else
            {
                var newLine = new GoodsPurchaseNoteLine
                {
                    ProductId = SelectedProduct.ProductId,
                    ProductName = SelectedProduct.ProductName,
                    QuantityOrdered = quantity,
                    UnitMeasure = SelectedProduct.UnitMeasureCode,
                    UnitPrice = UnitPrice,
                    LastGrnCostPrice = LastGrnCostPrice,
                    IsTaxApplicable = SelectedProduct.IsTaxApplicable
                };

                GoodsPurchaseNoteLines.Add(newLine);
            }
            CalculateTotals();
            ResetItemControls();
        }

        private void RemoveLineItem(GoodsPurchaseNoteLine lineToRemove)
        {
            if (lineToRemove != null)
            {
                GoodsPurchaseNoteLines.Remove(lineToRemove);
                lineToRemove.PropertyChanged -= GoodsPurchaseNoteLine_PropertyChanged;
                CalculateTotals();
                RaiseCanExecuteChanged();
            }
        }

        private bool CanSavePO =>
                SelectedSupplier != null &&
                GoodsPurchaseNoteLines.Count > 0 &&
                !HasErrors;
        private async Task CreateGoodsPurchaseNoteAsync()
        {
            try
            {
                _draftAutoSaveTimer.Stop();
                ValidatePurchaseOrder();

                var po = new GoodPurchaseNote
                {
                    GoodsPurchaseNoteId = CurrentOrderId,
                    BranchId = _userSessionService.BranchId,
                    SupplierId = SelectedSupplier.SupplierId,
                    Note = this.Note,
                    SubTotal = SubTotal,
                    DiscountAmount = DiscountAmount,
                    TaxAmount = TaxAmount,
                    TotalAmount = NetAmount,
                    OrderBy = OrderBy,
                    OrderDate = PurchaseDate,
                    ExpectedDeliveryDate = ExpectedDeliveryDate,
                    Status = PurchaseOrderStatus.PENDING_APPROVAL.ToString(),
                    CreatedBy = _userSessionService.UserId,
                    Lines = GoodsPurchaseNoteLines.ToList()
                };

                await SavePurchaseOrderAsync(po);

                CreateNewPO();

                MessageBox.Show("Purchase Order saved successfully. It will be available to open after approval.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error while saving GPN: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task OpenPurchaseOrderReportAsync(long purchaseNoteId)
        {
            DataTable reportData = await _goodsPurchaseNoteRepository.GetPurchaseOrderReportDataAsync(purchaseNoteId);

            if (reportData == null || reportData.Rows.Count == 0)
            {
                MessageBox.Show("No data found for this Purchase Order.", "Purchase Order Preview", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                PurchaseOrderNote reportDocument = null;

                try
                {
                    reportDocument = new PurchaseOrderNote();

                    var ds = new PurchaseOrderDS();
                    var compatibleReportData = PurchaseOrderReportDataNormalizer.Normalize(reportData, ds.PurchaseOrderReport);
                    ds.PurchaseOrderReport.Merge(compatibleReportData);

                    reportDocument.SetDataSource(ds);

                    var previewWindow = new ZReportViewerWindow(reportDocument, disposeReportOnClose: true)
                    {
                        Title = "Purchase Order Preview"
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
        #endregion

        #region HelperMethod
        private async Task SavePurchaseOrderAsync(GoodPurchaseNote purchaseOrder)
        {
            if (_isCloneFromRejectedPurchaseOrder)
            {
                purchaseOrder.GoodsPurchaseNoteId = 0;
                purchaseOrder.PONumber = null;
                await _goodsPurchaseNoteRepository.CreateAsync(purchaseOrder);
                return;
            }

            await _goodsPurchaseNoteRepository.UpsertDraftPurchaseOrderAsync(purchaseOrder, purchaseOrder.Lines);
        }

        private static GoodPurchaseNote GetPurchaseOrder(object parameter)
        {
            if (parameter is PurchaseOrderSummaryModel summary)
            {
                return summary.Source;
            }

            return parameter as GoodPurchaseNote;
        }

        private bool CanEditPurchaseOrder(GoodPurchaseNote purchaseOrder)
        {
            return purchaseOrder != null && (purchaseOrder.IsDraft || purchaseOrder.IsRejected);
        }

        private async Task PrintPurchaseOrderAsync(object parameter)
        {
            var purchaseOrder = GetPurchaseOrder(parameter);
            if (purchaseOrder == null || purchaseOrder.GoodsPurchaseNoteId <= 0)
            {
                return;
            }

            try
            {
                await OpenPurchaseOrderReportAsync(purchaseOrder.GoodsPurchaseNoteId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to open Purchase Order preview: {ex.Message}", "Purchase Order Preview", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadDraftPurchaseOrderAsync(GoodPurchaseNote draft)
        {
            if (!CanEditPurchaseOrder(draft))
            {
                return;
            }

            _isResettingPurchaseOrder = true;
            _draftAutoSaveTimer.Stop();

            try
            {
                if (Suppliers == null || Suppliers.Count == 0)
                {
                    await LoadSuppliers();
                }

                ResetItemControls();

                _isCloneFromRejectedPurchaseOrder = draft.IsRejected;
                CurrentOrderId = _isCloneFromRejectedPurchaseOrder ? 0 : Convert.ToInt32(draft.GoodsPurchaseNoteId);
                SelectedSupplier = Suppliers?.FirstOrDefault(s => s.SupplierId == draft.SupplierId);
                PurchaseDate = draft.OrderDate == default(DateTime) ? DateTime.Today : draft.OrderDate;
                ExpectedDeliveryDate = draft.ExpectedDeliveryDate;
                OrderBy = draft.OrderBy;
                Note = draft.Note;

                foreach (var line in GoodsPurchaseNoteLines)
                {
                    line.PropertyChanged -= GoodsPurchaseNoteLine_PropertyChanged;
                }

                GoodsPurchaseNoteLines.Clear();

                var lines = (await _goodsPurchaseNoteRepository.GetPOLinesAsync(draft.GoodsPurchaseNoteId)).ToList();
                foreach (var line in lines)
                {
                    GoodsPurchaseNoteLines.Add(line);
                }

                BillDiscount = Math.Max(0m, draft.DiscountAmount - lines.Sum(line => line.LineDiscount));

                CalculateTotals();
                ClearErrors(nameof(SelectedProduct));
                ClearErrors(nameof(Quantity));
                ClearErrors(nameof(UnitPrice));
                SelectedTabIndex = 0;
                FocusBarcode();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to open purchase order: {ex.Message}", "Open Purchase Order", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isResettingPurchaseOrder = false;
            }
        }

        private async Task DeleteDraftPurchaseOrderAsync(GoodPurchaseNote draft)
        {
            if (draft == null || !draft.IsDraft)
            {
                return;
            }

            var result = MessageBox.Show(
                $"Delete draft purchase order {draft.PONumber}?\n\nThis only removes the draft. Approved and pending purchase orders cannot be deleted here.",
                "Delete Draft",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                await _goodsPurchaseNoteRepository.SoftDeletePOAsync(draft.GoodsPurchaseNoteId, _userSessionService.UserId);

                if (CurrentOrderId == draft.GoodsPurchaseNoteId)
                {
                    CreateNewPO();
                }

                var historyRow = HistoryList?.FirstOrDefault(row =>
                    ReferenceEquals(row.Source, draft) ||
                    row.GoodsPurchaseNoteId == draft.GoodsPurchaseNoteId);

                if (historyRow != null)
                {
                    HistoryList.Remove(historyRow);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to delete draft PO: {ex.Message}", "Delete Draft", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateTotals()
        {
            CalculateTotals();
        }

        private void CalculateTotals()
        {
            var supplierVatRegistered = SelectedSupplier != null &&
                                        !string.IsNullOrWhiteSpace(SelectedSupplier.TaxRegistrationNumber);

            var lineNetAmounts = GoodsPurchaseNoteLines.ToDictionary(
                line => line,
                line => Math.Max(0m, (line.QuantityOrdered * line.UnitPrice) - line.LineDiscount));
            var discountableAmount = lineNetAmounts.Values.Sum();

            foreach (var line in GoodsPurchaseNoteLines)
            {
                var lineNetBeforeTax = lineNetAmounts[line];
                var billDiscountShare = discountableAmount > 0m
                    ? BillDiscount * (lineNetBeforeTax / discountableAmount)
                    : 0m;
                var taxableAmount = Math.Max(0m, lineNetBeforeTax - billDiscountShare);

                line.TaxAmount = supplierVatRegistered && line.IsTaxApplicable
                    ? TaxCalculator.CalculateExclusiveTax(taxableAmount, _inputTaxRate)
                    : 0m;
            }

            var totalLineDiscount = GoodsPurchaseNoteLines.Sum(line => line.LineDiscount);

            SubTotal = GoodsPurchaseNoteLines.Sum(line => line.QuantityOrdered * line.UnitPrice);
            TaxAmount = GoodsPurchaseNoteLines.Sum(line => line.TaxAmount);
            DiscountAmount = totalLineDiscount + BillDiscount;
            NetAmount = SubTotal - DiscountAmount + TaxAmount;
        }

        private async Task LoadTaxRateAsync()
        {
            var activeTax = (await _taxConfigurationRepository.GetAllAsync())
                .Where(tax => tax.IsActive
                           && tax.TaxCode == "VAT"
                           && tax.EffectiveDate.Date <= DateTime.Today)
                .OrderByDescending(tax => tax.EffectiveDate)
                .FirstOrDefault();

            _inputTaxRate = activeTax?.Rate ?? 0m;
            CalculateTotals();
        }

        private void GoodsPurchaseNoteLine_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GoodsPurchaseNoteLine.QuantityOrdered) ||
                e.PropertyName == nameof(GoodsPurchaseNoteLine.UnitPrice) ||
                e.PropertyName == nameof(GoodsPurchaseNoteLine.LineDiscount))
            {
                CalculateTotals();
                ScheduleDraftAutoSave();
            }
        }

        private void GoodsPurchaseNoteLines_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (GoodsPurchaseNoteLine line in e.NewItems)
                {
                    line.PropertyChanged += GoodsPurchaseNoteLine_PropertyChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (GoodsPurchaseNoteLine line in e.OldItems)
                {
                    line.PropertyChanged -= GoodsPurchaseNoteLine_PropertyChanged;
                }
            }

            (SavePOCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            CalculateTotals();
            ScheduleDraftAutoSave();
        }

        private bool IsDraftHeaderProperty(string propertyName)
        {
            return propertyName == nameof(SelectedSupplier) ||
                   propertyName == nameof(PurchaseDate) ||
                   propertyName == nameof(ExpectedDeliveryDate) ||
                   propertyName == nameof(OrderBy) ||
                   propertyName == nameof(Note) ||
                   propertyName == nameof(BillDiscount);
        }

        private void ScheduleDraftAutoSave()
        {
            if (_isResettingPurchaseOrder)
            {
                return;
            }

            if (_isCloneFromRejectedPurchaseOrder)
            {
                return;
            }

            if (_isDraftAutoSaveInProgress)
            {
                _draftAutoSaveQueuedWhileSaving = true;
                return;
            }

            _draftAutoSaveTimer.Stop();
            _draftAutoSaveTimer.Start();
        }

        private async void DraftAutoSaveTimer_Tick(object sender, EventArgs e)
        {
            _draftAutoSaveTimer.Stop();

            if (!CanAutoSaveDraft)
            {
                return;
            }

            try
            {
                _isDraftAutoSaveInProgress = true;
                ErrorMessage = null;

                var draft = BuildPurchaseOrderDraft();
                CurrentOrderId = await _goodsPurchaseNoteRepository.UpsertDraftPurchaseOrderAsync(draft, draft.Lines);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Draft auto-save failed: {ex.Message}";
                Debug.WriteLine($"Draft auto-save failed: {ex}");
            }
            finally
            {
                _isDraftAutoSaveInProgress = false;

                if (_draftAutoSaveQueuedWhileSaving)
                {
                    _draftAutoSaveQueuedWhileSaving = false;
                    ScheduleDraftAutoSave();
                }
            }
        }

        private bool CanAutoSaveDraft =>
            !_isCloneFromRejectedPurchaseOrder &&
            SelectedSupplier != null &&
            (GoodsPurchaseNoteLines.Count > 0 ||
             !string.IsNullOrWhiteSpace(OrderBy) ||
             !string.IsNullOrWhiteSpace(Note));

        private GoodPurchaseNote BuildPurchaseOrderDraft()
        {
            return new GoodPurchaseNote
            {
                GoodsPurchaseNoteId = CurrentOrderId,
                BranchId = _userSessionService.BranchId,
                SupplierId = SelectedSupplier?.SupplierId ?? 0,
                Note = Note,
                SubTotal = SubTotal,
                DiscountAmount = DiscountAmount,
                TaxAmount = TaxAmount,
                TotalAmount = NetAmount,
                OrderBy = OrderBy,
                OrderDate = PurchaseDate,
                ExpectedDeliveryDate = ExpectedDeliveryDate,
                Status = PurchaseOrderStatus.DRAFT.ToString(),
                CreatedBy = _userSessionService.UserId,
                Lines = GoodsPurchaseNoteLines.ToList()
            };
        }

        private void ResetItemControls()
        {
            SelectedProduct = null;
            Quantity = string.Empty;
            UnitPrice = 0;
            Barcode = string.Empty;
            LastGrnCostPrice = 0;
            SelectedUnitMeasureName = null;

            ClearErrors(nameof(UnitPrice));
            ClearErrors(nameof(Quantity));
            ClearErrors(nameof(SelectedProduct));

            FocusBarcode();
        }
        private void CreateNewPO()
        {
            _isResettingPurchaseOrder = true;
            _draftAutoSaveTimer.Stop();

            try
            {
                ResetItemControls();

                _isCloneFromRejectedPurchaseOrder = false;
                CurrentOrderId = 0;
                SelectedSupplier = null;
                SelectedProduct = null;

                OrderBy = string.Empty;
                Note = string.Empty;
                ExpectedDeliveryDate = null;
                SubTotal = 0;
                BillDiscount = 0;
                DiscountAmount = 0;
                TaxAmount = 0;
                NetAmount = 0;

                ClearErrors(nameof(SelectedProduct));
                ClearErrors(nameof(SelectedSupplier));
                ClearErrors(nameof(OrderBy));

                foreach (var line in GoodsPurchaseNoteLines)
                {
                    line.PropertyChanged -= GoodsPurchaseNoteLine_PropertyChanged;
                }

                GoodsPurchaseNoteLines.Clear();
            }
            finally
            {
                _isResettingPurchaseOrder = false;
            }
        }
        private void RaiseCanExecuteChanged()
        {
            (AddLineCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SavePOCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
        #endregion

        #region Validation
        private void ValidateItem()
        {
            ValidateUnitPrice();
            ValidateSelectedProduct();
            ValidateQuantity();
        }
        private void ValidatePurchaseOrder()
        {
            ValidateOrderBy();
        }
        private void ValidateOrderBy()
        {
            ClearErrors(nameof(OrderBy));
            if (string.IsNullOrWhiteSpace(OrderBy))
                AddError(nameof(OrderBy), "Order by is required.");
            else if (!Regex.IsMatch(OrderBy, @"^[a-zA-Z\s]+$"))
                AddError(nameof(OrderBy), "Cannot contain special character and numbers");
        }
        private void ValidateSelectedProduct()
        {
            ClearErrors(nameof(SelectedProduct));
            if (SelectedProduct == null)
                AddError(nameof(SelectedProduct), "Please select a product before adding");
        }
        private void ValidateQuantity()
        {
            ClearErrors(nameof(Quantity));

            if (string.IsNullOrWhiteSpace(Quantity))
            {
                AddError(nameof(Quantity), "Quantity is required.");
                return;
            }

            // 2. Check format (Regex: digits + optional decimal part with max 3 digits)
            if (!Regex.IsMatch(Quantity, @"^\d+(\.\d{1,3})?$"))
            {
                AddError(nameof(Quantity), "Quantity must be a number with up to 3 decimal places.");
                return;
            }

            // 3. Convert to decimal to check if it's greater than 0
            // (We use TryParse just to be completely safe, though the Regex guarantees it's a number)
            if (decimal.TryParse(Quantity, out decimal numericQuantity))
            {
                if (numericQuantity <= 0)
                {
                    AddError(nameof(Quantity), "Quantity must be greater than zero.");
                    return;
                }

            }
            else
            {
                AddError(nameof(Quantity), "Invalid quantity amount.");
            }

        }
        private void ShowStockLimitWarning(decimal quantity)
        {
            if (SelectedProduct == null)
            {
                return;
            }

            var maxQuantity = SelectedProduct.MaxStockQuantity;
            if (maxQuantity <= 0)
            {
                return;
            }

            var existingOrderQuantity = GoodsPurchaseNoteLines
                .Where(line => line.ProductId == SelectedProduct.ProductId)
                .Sum(line => line.QuantityOrdered);

            var projectedQuantity = SelectedProduct.AvailableQuantity + existingOrderQuantity + quantity;
            var criticalQuantity = maxQuantity + SelectedProduct.AdditionalStockQuantity;

            if (criticalQuantity > maxQuantity && projectedQuantity > criticalQuantity)
            {
                MessageBox.Show(
                    $"Critical stock limit exceeded for {SelectedProduct.ProductName}.\n\nAvailable + order quantity: {projectedQuantity:N3}\nMax + additional stock: {criticalQuantity:N3}",
                    "Critical Stock Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            else if (projectedQuantity > maxQuantity)
            {
                MessageBox.Show(
                    $"Stock limit exceeded for {SelectedProduct.ProductName}.\n\nAvailable + order quantity: {projectedQuantity:N3}\nMax stock quantity: {maxQuantity:N3}",
                    "Stock Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        public void ValidateUnitPrice()
        {
            ClearErrors(nameof(UnitPrice));
            if (UnitPrice <= 0)
                AddError(nameof(UnitPrice), "Unit price should be greater than 0");
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


