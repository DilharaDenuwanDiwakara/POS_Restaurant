using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
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
        private readonly DispatcherTimer _draftAutoSaveTimer;
        private decimal _inputTaxRate;
        private long _currentGoodsReceiveNoteId;
        private long _editingRejectedGrnId;
        private bool _isLoadingRejectedGrn;
        private bool _isResettingGoodsReceiveNote;
        private bool _isDraftAutoSaveInProgress;
        private bool _draftAutoSaveQueuedWhileSaving;

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
            GoodsReceiveNoteLines.CollectionChanged += GoodsReceiveNoteLines_CollectionChanged;

            _draftAutoSaveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _draftAutoSaveTimer.Tick += DraftAutoSaveTimer_Tick;

            SaveGRNCommand = new AsyncRelayCommand(async _ => await SaveGoodsReceiveNoteAsync(), _ => CanSaveGRN);
            NewGRNCommand = new RelayCommand(_ => CreateNewGRN());
            SearchCommand = new AsyncRelayCommand(async _ => await SearchGRNsAsync());
            RemoveLineCommand = new RelayCommand<GoodsReceiveNoteLine>(RemoveLineItem);
            LoadEditableGRNCommand = new AsyncRelayCommand(
                async grn => await LoadEditableGRNAsync(grn as GoodsReceiveNote));
            DeleteDraftGRNCommand = new AsyncRelayCommand(
                async grn => await DeleteDraftGRNAsync(grn as GoodsReceiveNote));

            this.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SelectedPO) ||
                    e.PropertyName == nameof(InvoiceNumber) ||
                    e.PropertyName == nameof(HasErrors))
                {
                    (SaveGRNCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                }

                if (IsDraftHeaderProperty(e.PropertyName))
                {
                    ScheduleDraftAutoSave();
                }
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
        public ICommand LoadEditableGRNCommand { get; }
        public ICommand DeleteDraftGRNCommand { get; }
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
            _isResettingGoodsReceiveNote = true;
            _draftAutoSaveTimer.Stop();

            try
            {
                _currentGoodsReceiveNoteId = 0;
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
                GoodsReceiveNoteLines.Clear();
                _ = LoadPendingPOsAsync();
                SelectedTabIndex = 0;
            }
            finally
            {
                _isResettingGoodsReceiveNote = false;
            }

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
                            UnitMeasureId = line.UnitMeasureId,
                            UnitMeasure = line.UnitMeasure,
                            OrderedPrice = line.UnitPrice,
                            UnitPrice = line.UnitPrice,
                            LineDiscount = line.LineDiscount,
                            TaxAmount = line.TaxAmount,
                            TrackExpiry = line.TrackExpiry,
                            IsTaxApplicable = line.IsTaxApplicable
                        };

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
                ScheduleDraftAutoSave();
            }
        }

        private void GoodsReceiveNoteLines_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (GoodsReceiveNoteLine line in e.NewItems)
                {
                    line.PropertyChanged += GoodsReceiveNoteLine_PropertyChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (GoodsReceiveNoteLine line in e.OldItems)
                {
                    line.PropertyChanged -= GoodsReceiveNoteLine_PropertyChanged;
                }
            }

            (SaveGRNCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            CalculateTotals();
            ScheduleDraftAutoSave();
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
                (LoadEditableGRNCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                (DeleteDraftGRNCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();

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

        private bool CanEditGoodsReceiveNote(GoodsReceiveNote grn)
        {
            return grn != null && (grn.IsDraft || grn.IsRejected);
        }

        private async Task LoadEditableGRNAsync(GoodsReceiveNote grn)
        {
            if (!CanEditGoodsReceiveNote(grn))
                return;

            try
            {
                await _taxRateLoadTask;
                await _suppliersLoadTask;

                _currentGoodsReceiveNoteId = grn.IsDraft ? grn.GoodsReceiveNoteId : 0;
                _editingRejectedGrnId = grn.IsRejected ? grn.GoodsReceiveNoteId : 0;
                OnPropertyChanged(nameof(IsEditingRejectedGRN));

                _isResettingGoodsReceiveNote = true;
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
                        line.UnitMeasureId = poLine.UnitMeasureId;
                        line.UnitMeasure = poLine.UnitMeasure;
                        line.OrderedPrice = poLine.UnitPrice;
                        line.TrackExpiry = poLine.TrackExpiry;
                        line.IsTaxApplicable = poLine.IsTaxApplicable;
                    }
                    else if (line.QuantityOrdered < line.QuantityReceived)
                    {
                        line.QuantityOrdered = line.QuantityReceived;
                    }

                    GoodsReceiveNoteLines.Add(line);
                }

                BillDiscount = Math.Max(0m, grn.DiscountAmount - GoodsReceiveNoteLines.Sum(line => line.LineDiscount));
                CalculateTotals();
                SelectedTabIndex = 0;
                RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load GRN for edit: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isResettingGoodsReceiveNote = false;
            }
        }

        private async Task DeleteDraftGRNAsync(GoodsReceiveNote draft)
        {
            if (draft == null || !draft.IsDraft)
            {
                return;
            }

            var result = MessageBox.Show(
                $"Delete draft GRN {draft.GoodsReceiveNoteNumber}?\n\nThis only removes the draft. Approved and pending GRNs cannot be deleted here.",
                "Delete Draft",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                await _goodsReceiveNoteRepository.SoftDeleteGRNAsync(draft.GoodsReceiveNoteId, _userSessionService.UserId);

                if (_currentGoodsReceiveNoteId == draft.GoodsReceiveNoteId)
                {
                    CreateNewGRN();
                }

                var historyRow = HistoryList?.FirstOrDefault(row => row.GoodsReceiveNoteId == draft.GoodsReceiveNoteId);
                if (historyRow != null)
                {
                    HistoryList.Remove(historyRow);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to delete draft GRN: {ex.Message}", "Delete Draft", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool IsDraftHeaderProperty(string propertyName)
        {
            return propertyName == nameof(SelectedPO) ||
                   propertyName == nameof(SelectedSupplierId) ||
                   propertyName == nameof(ReceivedDate) ||
                   propertyName == nameof(CreditDays) ||
                   propertyName == nameof(InvoiceNumber) ||
                   propertyName == nameof(ReceivedBy) ||
                   propertyName == nameof(Note) ||
                   propertyName == nameof(BillDiscount);
        }

        private void ScheduleDraftAutoSave()
        {
            if (_isResettingGoodsReceiveNote || IsEditingRejectedGRN)
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

                var draft = BuildGoodsReceiveNoteDraft();
                _currentGoodsReceiveNoteId = await _goodsReceiveNoteRepository.UpsertDraftGoodsReceiveNoteAsync(draft, draft.Lines);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"GRN draft auto-save failed: {ex.Message}";
                Debug.WriteLine($"GRN draft auto-save failed: {ex}");
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
            !IsEditingRejectedGRN &&
            SelectedPO != null &&
            SelectedSupplierId.HasValue &&
            SelectedSupplierId.Value > 0 &&
            (GoodsReceiveNoteLines.Count > 0 ||
             !string.IsNullOrWhiteSpace(InvoiceNumber) ||
             !string.IsNullOrWhiteSpace(ReceivedBy) ||
             !string.IsNullOrWhiteSpace(Note));

        private GoodsReceiveNote BuildGoodsReceiveNoteDraft()
        {
            return new GoodsReceiveNote
            {
                GoodsReceiveNoteId = _currentGoodsReceiveNoteId,
                BranchId = _userSessionService.BranchId,
                SupplierId = SelectedSupplierId ?? 0,
                PurchaseOrderId = SelectedPO?.GoodsPurchaseNoteId ?? 0,
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
                Status = GoodsReceiveNoteStatus.DRAFT.ToString(),
                CreatedBy = _userSessionService.UserId,
                Lines = GoodsReceiveNoteLines.ToList()
            };
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
            _draftAutoSaveTimer.Stop();
            ValidateGRN();
            if (HasErrors) return;

            try
            {
                var grn = new GoodsReceiveNote
                {
                    BranchId = _userSessionService.BranchId,
                    SupplierId = SelectedSupplierId.Value,
                    PurchaseOrderId = SelectedPO.GoodsPurchaseNoteId,
                    GoodsReceiveNoteNumber = HistoryList?
                        .FirstOrDefault(row => row.GoodsReceiveNoteId == _currentGoodsReceiveNoteId)?
                        .GoodsReceiveNoteNumber,
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
                    grn.GoodsReceiveNoteId = _currentGoodsReceiveNoteId;
                    grn.Status = GoodsReceiveNoteStatus.PENDING_APPROVAL.ToString();
                    var newId = await _goodsReceiveNoteRepository.UpsertDraftGoodsReceiveNoteAsync(grn, grn.Lines);
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
