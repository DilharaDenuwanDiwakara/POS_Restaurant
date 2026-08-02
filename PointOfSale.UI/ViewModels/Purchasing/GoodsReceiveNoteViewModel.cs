using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using PointOfSale.Core.Enums;
using PointOfSale.Core.Interfaces.Purchasing;
using PointOfSale.Core.Interfaces.Repositories.Purchasing;
using PointOfSale.Core.Interfaces.Repositories.System;
using PointOfSale.Core.Models.Purchasing;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Purchasing
{
    public class GoodsReceiveNoteViewModel : BaseViewModel
    {
        private readonly ISupplierRepository _supplierRepository;
        private readonly IGoodsPurchaseNoteRepository _poRepository;
        private readonly IGoodsReceiveNoteRepository _goodsReceiveNoteRepository;
        private readonly ITaxConfigurationRepository _taxConfigurationRepository;
        private readonly IUserSessionService _userSessionService;
        private readonly Task _taxRateLoadTask;
        private readonly Task _suppliersLoadTask;
        private decimal _inputTaxRate;
        private long _editingRejectedGrnId;
        private bool _isLoadingRejectedGrn;

        public GoodsReceiveNoteViewModel(ISupplierRepository supplierRepository,
                                         IGoodsReceiveNoteRepository goodsReceiveNoteRepository,
                                         IGoodsPurchaseNoteRepository poRepository,
                                         ITaxConfigurationRepository taxConfigurationRepository,
                                         IUserSessionService userSessionService)
        {
            _supplierRepository = supplierRepository;
            _goodsReceiveNoteRepository = goodsReceiveNoteRepository;
            _poRepository = poRepository;
            _taxConfigurationRepository = taxConfigurationRepository;
            _userSessionService = userSessionService;

            GoodsReceiveNoteLines = new ObservableCollection<GoodsReceiveNoteLine>();

            SaveGRNCommand = new AsyncRelayCommand(async _ => await SaveGoodsReceiveNoteAsync(), _ => CanSaveGRN);
            NewGRNCommand = new RelayCommand(_ => CreateNewGRN());
            SearchCommand = new AsyncRelayCommand(async _ => await SearchGRNsAsync());
            RemoveLineCommand = new RelayCommand<GoodsReceiveNoteLine>(RemoveLineItem);
            LoadRejectedGRNCommand = new AsyncRelayCommand(
                async grn => await LoadRejectedGRNForEditAsync(grn as GoodsReceiveNote));

            this.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SelectedPO) ||
                    e.PropertyName == nameof(InvoiceNumber) ||
                    e.PropertyName == nameof(HasErrors))
                {
                    (SaveGRNCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                }
            };

            GoodsReceiveNoteLines.CollectionChanged += (s, e) =>
            {
                (SaveGRNCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                CalculateTotals();
            };

            _taxRateLoadTask = LoadTaxRateAsync();
            _suppliersLoadTask = LoadSuppliers();
            _ = LoadPendingPOsAsync();
        }

        #region Properties
        private ObservableCollection<Supplier> _suppliers;
        public ObservableCollection<Supplier> Suppliers
        {
            get => _suppliers;
            private set => SetProperty(ref _suppliers, value);
        }

        private ObservableCollection<GoodPurchaseNote> _openPurchaseOrders;
        public ObservableCollection<GoodPurchaseNote> OpenPurchaseOrders
        {
            get => _openPurchaseOrders;
            private set => SetProperty(ref _openPurchaseOrders, value);
        }

        public ObservableCollection<GoodsReceiveNoteLine> GoodsReceiveNoteLines { get; }

        private ObservableCollection<GoodsReceiveNote> _historyList;
        public ObservableCollection<GoodsReceiveNote> HistoryList
        {
            get => _historyList;
            set => SetProperty(ref _historyList, value);
        }

        private GoodPurchaseNote _selectedPO;
        public GoodPurchaseNote SelectedPO
        {
            get => _selectedPO;
            set
            {
                if (SetProperty(ref _selectedPO, value) && value != null)
                {
                    if (!_isLoadingRejectedGrn)
                    {
                        _ = LoadPODetailsAsync();
                    }
                    RaiseCanExecuteChanged();
                }
            }
        }

        private int _selectedTabIndex;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetProperty(ref _selectedTabIndex, value);
        }

        public bool IsEditingRejectedGRN => _editingRejectedGrnId > 0;

        private int? _selectedSupplierId;
        public int? SelectedSupplierId
        {
            get => _selectedSupplierId;
            set
            {
                if (SetProperty(ref _selectedSupplierId, value))
                {
                    if (!_isLoadingRejectedGrn)
                    {
                        ApplySupplierCreditPeriod();
                    }

                    CalculateTotals();
                    RaiseCanExecuteChanged();
                }
            }
        }

        private bool _isSupplierLocked;
        public bool IsSupplierLocked
        {
            get => _isSupplierLocked;
            set => SetProperty(ref _isSupplierLocked, value);
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

        private ObservableCollection<Supplier> _searchSuppliers;
        public ObservableCollection<Supplier> SearchSuppliers
        {
            get => _searchSuppliers;
            private set => SetProperty(ref _searchSuppliers, value);
        }

        public IEnumerable<GoodsReceiveNoteStatus?> FilterStatuses { get; } =
            new GoodsReceiveNoteStatus?[] { null }
                .Concat(Enum.GetValues(typeof(GoodsReceiveNoteStatus))
                    .Cast<GoodsReceiveNoteStatus>()
                    .Select(status => (GoodsReceiveNoteStatus?)status));

        private GoodsReceiveNoteStatus? _filterStatus;
        public GoodsReceiveNoteStatus? FilterStatus
        {
            get => _filterStatus;
            set => SetProperty(ref _filterStatus, value);
        }

        #region GRN Header
        private string _supplierName;
        public string SupplierName // Read-only, populated from PO
        {
            get => _supplierName;
            private set => SetProperty(ref _supplierName, value);
        }

        private int _supplierId;

        private DateTime _receivedDate = DateTime.Today;
        public DateTime ReceivedDate
        {
            get => _receivedDate;
            set
            {
                if (SetProperty(ref _receivedDate, value))
                {
                    UpdateDueDate();
                }
            }
        }

        private int _creditDays;
        public int CreditDays
        {
            get => _creditDays;
            set
            {
                var normalizedValue = Math.Max(0, value);
                if (SetProperty(ref _creditDays, normalizedValue))
                {
                    UpdateDueDate();
                }
            }
        }

        private DateTime _dueDate = DateTime.Today;
        public DateTime DueDate
        {
            get => _dueDate;
            private set => SetProperty(ref _dueDate, value);
        }

        private string _invoiceNumber;
        public string InvoiceNumber
        {
            get => _invoiceNumber;
            set { SetProperty(ref _invoiceNumber, value); ValidateInvoiceNumber(); RaiseCanExecuteChanged(); }
        }

        private string _receivedBy;
        public string ReceivedBy
        {
            get => _receivedBy;
            set { SetProperty(ref _receivedBy, value); ValidateReceivedBy(); RaiseCanExecuteChanged(); }
        }

        private string _note;
        public string Note
        {
            get => _note;
            set => SetProperty(ref _note, value);
        }
        #endregion

        #region Summary
        private decimal _subTotal;
        public decimal SubTotal
        {
            get => _subTotal;
            private set
            {
                SetProperty(ref _subTotal, value);
            }
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

        public decimal TotalDiscount
        {
            get => BillDiscount;
            set => BillDiscount = value;
        }

        private decimal _discountAmount;
        public decimal DiscountAmount
        {
            get => _discountAmount;
            private set => SetProperty(ref _discountAmount, value);
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
            private set { SetProperty(ref _netAmount, value); }
        }
        #endregion

        #endregion

        #region Command
        public ICommand SaveGRNCommand { get; set; }
        public ICommand NewGRNCommand { get; }
        public ICommand SearchCommand { get; set; }
        public ICommand RemoveLineCommand { get; }
        public ICommand LoadRejectedGRNCommand { get; }
        #endregion

        #region HelperMethod
        public void UpdateTotals()
        {
            CalculateTotals();
        }

        private void ApplySupplierCreditPeriod()
        {
            var supplier = Suppliers?.FirstOrDefault(s => s.SupplierId == SelectedSupplierId);
            CreditDays = supplier?.CreditPeriodDays ?? 0;
        }

        private void UpdateDueDate()
        {
            DueDate = ReceivedDate.Date.AddDays(CreditDays);
        }

        private void CalculateTotals()
        {
            var supplier = Suppliers?.FirstOrDefault(s => s.SupplierId == SelectedSupplierId);
            var supplierVatRegistered = supplier != null &&
                                        !string.IsNullOrWhiteSpace(supplier.TaxRegistrationNumber);

            var lineNetAmounts = GoodsReceiveNoteLines.ToDictionary(
                line => line,
                line => Math.Max(0m, (line.QuantityReceived * line.UnitPrice) - line.LineDiscount));
            var discountableAmount = lineNetAmounts.Values.Sum();

            foreach (var line in GoodsReceiveNoteLines)
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

            SubTotal = GoodsReceiveNoteLines.Sum(l => l.QuantityReceived * l.UnitPrice);
            TaxAmount = GoodsReceiveNoteLines.Sum(l => l.TaxAmount);
            DiscountAmount = GoodsReceiveNoteLines.Sum(l => l.LineDiscount) + BillDiscount;
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

        private void CreateNewGRN()
        {
            _editingRejectedGrnId = 0;
            OnPropertyChanged(nameof(IsEditingRejectedGRN));
            SelectedPO = null;
            SelectedSupplierId = -1;
            IsSupplierLocked = false;
            ReceivedDate = DateTime.Today;
            CreditDays = 0;
            UpdateDueDate();
            ReceivedBy = string.Empty;
            InvoiceNumber = string.Empty;
            Note = string.Empty;
            SubTotal = 0;
            BillDiscount = 0;
            DiscountAmount = 0;
            TaxAmount = 0;
            NetAmount = 0;
            ClearAllErrors();
            foreach (var line in GoodsReceiveNoteLines)
            {
                line.PropertyChanged -= GoodsReceiveNoteLine_PropertyChanged;
            }
            GoodsReceiveNoteLines.Clear();
            _ = LoadPendingPOsAsync();
            SelectedTabIndex = 0;

        }

        private void RaiseCanExecuteChanged()
        {
            (SaveGRNCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
        #endregion

        #region Methods
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
        private async Task LoadPendingPOsAsync()
        {
            try
            {
                var pos = await _poRepository.GetPendingReceiptPOsAsync();
                OpenPurchaseOrders = new ObservableCollection<GoodPurchaseNote>(pos);
                OnPropertyChanged(nameof(OpenPurchaseOrders)); // Force UI refresh
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load POs: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async Task LoadPODetailsAsync()
        {
            if (SelectedPO == null) return;

            try
            {
                await _taxRateLoadTask;
                await _suppliersLoadTask;

                SelectedSupplierId = SelectedPO.SupplierId;
                IsSupplierLocked = true;

                GoodsReceiveNoteLines.Clear();
                var poLines = await _poRepository.GetPOLinesAsync(SelectedPO.GoodsPurchaseNoteId);

                foreach (var line in poLines)
                {
                    decimal remainingQty = line.QuantityOrdered - line.AlreadyReceivedQuantity;

                    if (remainingQty > 0)
                    {
                        var grnLine = new GoodsReceiveNoteLine
                        {
                            GoodsPurchaseNoteLineId = line.GoodsPurchaseNoteLineId,
                            ProductId = line.ProductId,
                            ProductName = line.ProductName,
                            QuantityOrdered = line.QuantityOrdered,
                            QuantityReceived = remainingQty,
                            UnitMeasure = line.UnitMeasure,
                            OrderedPrice = line.UnitPrice,
                            UnitPrice = line.UnitPrice,
                            LineDiscount = line.LineDiscount,
                            TaxAmount = line.TaxAmount,
                            TrackExpiry = line.TrackExpiry,
                            IsTaxApplicable = line.IsTaxApplicable
                        };

                        grnLine.PropertyChanged += GoodsReceiveNoteLine_PropertyChanged;

                        GoodsReceiveNoteLines.Add(grnLine);
                    }
                }

                UpdateTotals();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load PO details: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GoodsReceiveNoteLine_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GoodsReceiveNoteLine.QuantityReceived) ||
                e.PropertyName == nameof(GoodsReceiveNoteLine.UnitPrice) ||
                e.PropertyName == nameof(GoodsReceiveNoteLine.LineDiscount))
            {
                CalculateTotals();
            }
        }

        private async Task SearchGRNsAsync()
        {
            try
            {
                int? supplierId = (FilterSupplierId == -1) ? (int?)null : FilterSupplierId;

                var results = await _goodsReceiveNoteRepository.GetAllAsync(supplierId, SearchDateFrom, SearchDateTo);

                if (FilterStatus.HasValue)
                {
                    results = results.Where(x =>
                        string.Equals(x.Status, FilterStatus.Value.ToString(), StringComparison.OrdinalIgnoreCase));
                }

                HistoryList = new ObservableCollection<GoodsReceiveNote>(results);

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

        private bool CanLoadRejectedGRN(GoodsReceiveNote grn)
        {
            return grn != null && grn.IsRejected;
        }

        private async Task LoadRejectedGRNForEditAsync(GoodsReceiveNote grn)
        {
            if (!CanLoadRejectedGRN(grn))
                return;

            try
            {
                await _taxRateLoadTask;
                await _suppliersLoadTask;

                _editingRejectedGrnId = grn.GoodsReceiveNoteId;
                OnPropertyChanged(nameof(IsEditingRejectedGRN));

                _isLoadingRejectedGrn = true;
                try
                {
                    SelectedPO = OpenPurchaseOrders?.FirstOrDefault(po => po.GoodsPurchaseNoteId == grn.PurchaseOrderId)
                                 ?? new GoodPurchaseNote
                                 {
                                     GoodsPurchaseNoteId = grn.PurchaseOrderId,
                                     PONumber = grn.PONumber,
                                     SupplierId = grn.SupplierId
                                 };
                    SelectedSupplierId = grn.SupplierId;
                    IsSupplierLocked = true;
                    ReceivedDate = grn.GoodsReceiveNoteDate;
                    CreditDays = grn.CreditDays;
                    DueDate = grn.DueDate == default(DateTime)
                        ? grn.GoodsReceiveNoteDate.Date.AddDays(grn.CreditDays)
                        : grn.DueDate;
                }
                finally
                {
                    _isLoadingRejectedGrn = false;
                }

                InvoiceNumber = grn.InvoiceNumber;
                ReceivedBy = grn.ReceivedBy;
                Note = grn.Notes;

                foreach (var line in GoodsReceiveNoteLines)
                {
                    line.PropertyChanged -= GoodsReceiveNoteLine_PropertyChanged;
                }
                GoodsReceiveNoteLines.Clear();

                var lines = (await _goodsReceiveNoteRepository.GetLinesByGRNIdAsync(grn.GoodsReceiveNoteId)).ToList();
                var poLines = grn.PurchaseOrderId > 0
                    ? (await _poRepository.GetPOLinesAsync(grn.PurchaseOrderId)).ToList()
                    : new List<GoodsPurchaseNoteLine>();

                foreach (var line in lines)
                {
                    var poLine = poLines.FirstOrDefault(source =>
                        source.GoodsPurchaseNoteLineId == line.GoodsPurchaseNoteLineId);

                    if (poLine != null)
                    {
                        line.QuantityOrdered = poLine.QuantityOrdered;
                        line.UnitMeasure = poLine.UnitMeasure;
                        line.OrderedPrice = poLine.UnitPrice;
                        line.TrackExpiry = poLine.TrackExpiry;
                        line.IsTaxApplicable = poLine.IsTaxApplicable;
                    }
                    else if (line.QuantityOrdered < line.QuantityReceived)
                    {
                        line.QuantityOrdered = line.QuantityReceived;
                    }

                    line.PropertyChanged += GoodsReceiveNoteLine_PropertyChanged;
                    GoodsReceiveNoteLines.Add(line);
                }

                BillDiscount = Math.Max(0m, grn.DiscountAmount - GoodsReceiveNoteLines.Sum(line => line.LineDiscount));
                CalculateTotals();
                SelectedTabIndex = 0;
                RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load rejected GRN for edit: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RemoveLineItem(GoodsReceiveNoteLine lineToRemove)
        {
            if (lineToRemove == null)
                return;

            var result = MessageBox.Show(
                $"Remove {lineToRemove.ProductName} from this GRN?",
                "Confirm Remove",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            GoodsReceiveNoteLines.Remove(lineToRemove);
            lineToRemove.PropertyChanged -= GoodsReceiveNoteLine_PropertyChanged;
            UpdateTotals();
            RaiseCanExecuteChanged();
        }

        private bool CanSaveGRN =>
                (SelectedPO != null || IsEditingRejectedGRN) &&
                !string.IsNullOrWhiteSpace(InvoiceNumber) &&
                 GoodsReceiveNoteLines.Count > 0 &&
                !HasErrors;
        private async Task SaveGoodsReceiveNoteAsync()
        {
            ValidateGRN();
            if (HasErrors) return;

            try
            {
                var grn = new GoodsReceiveNote
                {
                    BranchId = _userSessionService.BranchId,
                    SupplierId = SelectedSupplierId.Value,
                    PurchaseOrderId = SelectedPO.GoodsPurchaseNoteId,
                    InvoiceNumber = InvoiceNumber,
                    Notes = Note,
                    SubTotal = SubTotal,
                    DiscountAmount = DiscountAmount,
                    TaxAmount = TaxAmount,
                    TotalAmount = NetAmount,
                    ReceivedBy = ReceivedBy,
                    GoodsReceiveNoteDate = ReceivedDate,
                    CreditDays = CreditDays,
                    DueDate = DueDate,
                    CreatedBy = _userSessionService.UserId,
                    Lines = GoodsReceiveNoteLines.ToList()
                };

                if (IsEditingRejectedGRN)
                {
                    grn.GoodsReceiveNoteId = _editingRejectedGrnId;
                    await _goodsReceiveNoteRepository.ResubmitRejectedAsync(grn);
                    MessageBox.Show("Rejected GRN updated and sent for approval.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    await SearchGRNsAsync();
                }
                else
                {
                    var newId = await _goodsReceiveNoteRepository.CreateAsync(grn);
                    MessageBox.Show($"GRN {newId} Created Successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                CreateNewGRN();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving GRN: {ex.Message}");
            }
        }
        #endregion

        #region Validation
        private void ValidateGRN()
        {
            ValidateInvoiceNumber();
            ValidateReceivedBy();
        }
        private void ValidateInvoiceNumber()
        {
            ClearErrors(nameof(InvoiceNumber));
            if (string.IsNullOrWhiteSpace(InvoiceNumber))
                AddError(nameof(InvoiceNumber), "Invoice number is required");
            else if (!Regex.IsMatch(InvoiceNumber.Trim(), @"^[a-zA-Z0-9_/-]+$"))
                AddError(nameof(InvoiceNumber), "Invoice number can contain only letters, numbers, underscores, slashes, and hyphens.");
        }
        private void ValidateReceivedBy()
        {
            ClearErrors(nameof(ReceivedBy));
            if (string.IsNullOrWhiteSpace(ReceivedBy))
                AddError(nameof(ReceivedBy), "Received by is required.");
            else if (!Regex.IsMatch(ReceivedBy, @"^[a-zA-Z\s]+$"))
                AddError(nameof(ReceivedBy), "Cannot contain special character and numbers");
        }
        private void ValidateLine(GoodsReceiveNoteLine line)
        {
            // Industry Standard: You cannot receive more than what was ordered
            if (line.QuantityReceived > line.QuantityOrdered)
            {
                // Trigger UI Warning
            }
        }
        #endregion
    }
}
