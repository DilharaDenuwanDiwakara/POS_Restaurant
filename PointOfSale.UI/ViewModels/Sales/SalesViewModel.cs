using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Enums;
using PointOfSale.Core.Interfaces.Repositories.Inventory;
using PointOfSale.Core.Interfaces.Repositories.Restaurant;
using PointOfSale.Core.Interfaces.Repositories.Sales;
using PointOfSale.Core.Interfaces.Repositories.System;
using PointOfSale.Core.Interfaces.Services;
using PointOfSale.Core.Models.Restaurant;
using PointOfSale.Core.Models.Sales;
using PointOfSale.Core.Models.System;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;
using PointOfSale.UI.Services;
using PointOfSale.UI.ViewModels.Restaurant;
using PointOfSale.UI.ViewModels.Security;
using PointOfSale.UI.ViewModels.Shell;
using PointOfSale.UI.Views.Restaurant;
using PointOfSale.UI.Views.Sales;
using PointOfSale.UI.Views.Security;
using PointOfSale.UI.Views.Shell;

namespace PointOfSale.UI.ViewModels.Sales
{
    public class SalesViewModel : BaseViewModel
    {
        private readonly ISalesRepository _salesRepository;
        private readonly IDiscountRepository _discountRepository;
        private readonly IPromotionRepository _promotionRepository;
        private readonly IOrderRepository _orderRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly IMenuCategoryRepository _menuCategoryRepository;
        private readonly IMenuItemRepository _menuItemRepository;
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IProductBatchRepository _productBatchRepository;
        private readonly IPaymentTerminalRepository _paymentTerminalRepository;
        private readonly IShiftRepository _shiftRepository;
        private readonly IServiceProvider _serviceProvider;
        private readonly IUserSessionService _userSessionService;
        private readonly IDialogService _dialogService;
        private readonly IReportService _reportService;
        private readonly ITaxConfigurationRepository _taxConfigurationRepository;
        private readonly CloudStorageService _storageService;
        private List<TaxConfiguration> _taxConfigurations = new List<TaxConfiguration>();
        private const decimal LoyaltySpendAmountPerPoint = 100m; // Rs.100 = 1 point
        private const decimal LoyaltyRedeemValuePerPoint = 1m;   // 1 point = Rs.1
        private const string CashPaymentMethod = "CASH";
        private const string CardPaymentMethod = "CARD";
        private const string CreditPaymentMethod = "CREDIT";
        private const string BankTransferPaymentMethod = "BANK_TRANSFER";
        private const string RetailCategoryIdSettingName = "RetailCategoryId";
        private const string TerminalLocationIdSettingName = "TerminalLocationId";

        // Flag to prevent infinite loops between Barcode and SelectedProduct setters
        private bool _suppressProductSelectionTrigger;
        private long? _currentRestaurantOrderId;
        private List<PromotionRule> _activePromotionRules = new List<PromotionRule>();
        private List<DiscountDefinition> _activeAutoDiscountRules = new List<DiscountDefinition>();
        private decimal _autoBillDiscountAmount;
        private decimal _appliedDiscountAmount;
        private decimal _appliedDiscountPercent;
        private string _appliedAutoBillDiscountName;
        private DiscountDefinition _appliedAutoBillDiscountRule;
        private string _appliedAutoDiscountName;
        private decimal? _activeAutoDiscountRulesSubTotal;
        private decimal? _pendingAutoDiscountRulesSubTotal;
        private bool _isRefreshingAutoDiscountRules;
        private bool _isCalculatingTotals;
        private bool _isUpdatingCart;
        private bool _isApplyingCustomerSelection;
        private bool _isDataLoaded;
        private bool _isLoadingData;
        private readonly Dictionary<string, MenuVariantDto> _productByCode =
            new Dictionary<string, MenuVariantDto>(StringComparer.OrdinalIgnoreCase);
        private readonly System.Windows.Threading.DispatcherTimer _productFilterDebounceTimer;
        private readonly System.Windows.Threading.DispatcherTimer _autoDiscountRefreshDebounceTimer;

        public SalesViewModel(ISalesRepository salesRepository,
                              IDiscountRepository discountRepository,
                              IPromotionRepository promotionRepository,
                              IOrderRepository orderRepository,
                              ICustomerRepository customerRepository,
                              IMenuCategoryRepository menuCategoryRepository,
                              IInventoryRepository inventoryRepository,
                              IProductBatchRepository productBatchRepository,
                              IPaymentTerminalRepository paymentTerminalRepository,
                              IShiftRepository shiftRepository,
                              IServiceProvider serviceProvider,
                              IUserSessionService userSessionService,
                              IDialogService dialogService,
                              IMenuItemRepository menuItemRepository,
                              IReportService reportService,
                              ITaxConfigurationRepository taxConfigurationRepository,
                              CloudStorageService storageService)
        {
            _salesRepository = salesRepository;
            _discountRepository = discountRepository;
            _promotionRepository = promotionRepository;
            _orderRepository = orderRepository;
            _customerRepository = customerRepository;
            _menuCategoryRepository = menuCategoryRepository;
            _inventoryRepository = inventoryRepository;
            _menuItemRepository = menuItemRepository;
            _productBatchRepository = productBatchRepository;
            _paymentTerminalRepository = paymentTerminalRepository;
            _shiftRepository = shiftRepository;
            _serviceProvider = serviceProvider;
            _userSessionService = userSessionService;
            _dialogService = dialogService;
            _reportService = reportService ?? throw new ArgumentNullException(nameof(reportService));
            _taxConfigurationRepository = taxConfigurationRepository;
            _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));

            InitializeCommands();
            InitializeCollections();
            GenerateInvoiceNumber();

            // Timer for Clock
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (s, e) => CurrentDateTime = DateTime.Now;
            timer.Start();

            _productFilterDebounceTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(180)
            };
            _productFilterDebounceTimer.Tick += (s, e) =>
            {
                _productFilterDebounceTimer.Stop();
                RefreshProductFilter();
            };

            _autoDiscountRefreshDebounceTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _autoDiscountRefreshDebounceTimer.Tick += (s, e) =>
            {
                _autoDiscountRefreshDebounceTimer.Stop();
                if (_pendingAutoDiscountRulesSubTotal.HasValue && !_isRefreshingAutoDiscountRules)
                    _ = RefreshPendingActiveAutoDiscountRulesAsync();
            };

            Quantity = 1;
            IsServiceChargeEnabled = false;
            IsTaxEnabled = false;

        }

        #region Properties
        public IUserSessionService CurrentUser => _userSessionService;
        public bool CanAccessAdminPanel => _userSessionService.HasPermission("ACCESS_ADMIN_PANEL");

        private DateTime _currentDateTime;
        public DateTime CurrentDateTime { get => _currentDateTime; set => SetProperty(ref _currentDateTime, value); }

        // --- Collections ---
        public ObservableCollection<Customer> Customers { get; private set; }
        public ObservableCollection<MenuCategory> Categories { get; private set; }
        public ObservableCollection<MenuVariantDto> Products { get; private set; }
        public ObservableCollection<SalesLine> CartItems { get; private set; }
        public ICollectionView FilteredCustomers { get; private set; }
        public ICollectionView FilteredProducts { get; private set; }
        public ObservableCollection<ServedOrderDto> ServedOrders { get; private set; }
        public ObservableCollection<PaymentDetail> AppliedPayments { get; } = new ObservableCollection<PaymentDetail>();

        // --- Selections ---
        private string _customerSearchText;
        public string CustomerSearchText
        {
            get => _customerSearchText;
            set
            {
                if (SetProperty(ref _customerSearchText, value))
                {
                    FilteredCustomers?.Refresh();

                    if (!_isApplyingCustomerSelection)
                        IsCustomerDropDownOpen = !string.IsNullOrWhiteSpace(value) && FilteredCustomers?.Cast<object>().Any() == true;
                }
            }
        }

        private bool _isCustomerDropDownOpen;
        public bool IsCustomerDropDownOpen
        {
            get => _isCustomerDropDownOpen;
            set => SetProperty(ref _isCustomerDropDownOpen, value);
        }

        private Customer _selectedCustomer;
        public Customer SelectedCustomer
        {
            get => _selectedCustomer;
            set
            {
                if (SetProperty(ref _selectedCustomer, value))
                {
                    _isApplyingCustomerSelection = true;
                    CustomerSearchText = value?.CustomerName ?? string.Empty;
                    IsCustomerDropDownOpen = false;
                    _isApplyingCustomerSelection = false;

                    _ = LoadSelectedCustomerLoyaltyAsync();
                    CalculateTotals();
                    ValidatePayment();
                    RaiseSaveCommandState();
                }
            }
        }

        private SalesLine _selectedCartItem;
        public SalesLine SelectedCartItem
        {
            get => _selectedCartItem;
            set
            {
                if (SetProperty(ref _selectedCartItem, value))
                    (RemoveItemCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        private MenuCategory _selectedCategory;
        public MenuCategory SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                    RefreshProductFilter();
            }
        }

        private ServedOrderDto _selectedServedOrder;
        public ServedOrderDto SelectedServedOrder
        {
            get => _selectedServedOrder;
            set
            {
                if (SetProperty(ref _selectedServedOrder, value) && value != null)
                {
                    _ = LoadRestaurantOrderIntoCart(value);
                }
            }
        }

        private MenuVariantDto _selectedProduct;
        public MenuVariantDto SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                if (SetProperty(ref _selectedProduct, value) && value != null)
                {
                    //if (!_suppressProductSelectionTrigger)
                    //{
                    //    Barcode = value.ItemCode ?? string.Empty;
                    //}

                    SellingPrice = value.DefaultPrice;
                }

                if (AddProductCommand is RelayCommand relayCommand)
                    relayCommand.RaiseCanExecuteChanged();
                else if (AddProductCommand is AsyncRelayCommand asyncRelayCommand)
                    asyncRelayCommand.RaiseCanExecuteChanged();
            }
        }

        private string _barcode;
        public string Barcode
        {
            get => _barcode;
            set
            {
                if (SetProperty(ref _barcode, value))
                    QueueProductFilterRefresh();
            }
        }

        // --- Entry Inputs ---
        private string _invoiceNumber;
        public string InvoiceNumber
        {
            get => _invoiceNumber;
            private set => SetProperty(ref _invoiceNumber, value);
        }

        private decimal _sellingPrice;
        public decimal SellingPrice { get => _sellingPrice; set => SetProperty(ref _sellingPrice, value); }

        private decimal _quantity;
        public decimal Quantity { get => _quantity; set => SetProperty(ref _quantity, value); }

        private decimal _discount;
        public decimal Discount { get => _discount; set => SetProperty(ref _discount, value); }

        // --- Payment & Calculation ---
        private string _selectedPaymentMethod;
        public string SelectedPaymentMethod
        {
            get => _selectedPaymentMethod;
            set
            {
                var normalizedValue = NormalizePaymentMethod(value);

                if (!SetProperty(ref _selectedPaymentMethod, normalizedValue))
                    return;

                CurrentPaymentAmount = IsPaymentInputVisible && RemainingBalance > 0 ? RemainingBalance : 0m;

                if (!IsPaymentTypeCard)
                    SelectedPaymentTerminal = null;
                else if (SelectedPaymentTerminal == null && PaymentTerminals.Any())
                    SelectedPaymentTerminal = PaymentTerminals.First();

                ReferenceNumber = string.Empty;

                OnPropertyChanged(nameof(IsPaymentPanelVisible));
                OnPropertyChanged(nameof(IsPaymentInputVisible));
                OnPropertyChanged(nameof(IsPaymentTypeCash));
                OnPropertyChanged(nameof(IsPaymentTypeCard));
                OnPropertyChanged(nameof(IsPaymentTypeCredit));
                OnPropertyChanged(nameof(IsPaymentTypeBankTransfer));
                OnPropertyChanged(nameof(IsNonCashPaymentType));
                OnPropertyChanged(nameof(PaymentTypeDisplay));
                OnPropertyChanged(nameof(IsPaymentValid));
                OnPropertyChanged(nameof(IsPaymentWorkflowVisible));

                ValidatePayment();

                if (IsPaymentTypeCash) RequestCashFocus?.Invoke();

                RaiseSaveCommandState();
            }
        }

        // Visibility Helpers
        private bool _isPaymentInputVisible;
        public bool IsPaymentInputVisible
        {
            get => _isPaymentInputVisible;
            set
            {
                if (SetProperty(ref _isPaymentInputVisible, value))
                {
                    OnPropertyChanged(nameof(IsPaymentPanelVisible));
                    OnPropertyChanged(nameof(IsPaymentWorkflowVisible));
                    OnPropertyChanged(nameof(IsPaymentValid));
                    RaiseSaveCommandState();
                }
            }
        }

        private bool _isPaymentGridVisible;
        public bool IsPaymentGridVisible
        {
            get => _isPaymentGridVisible;
            set
            {
                if (SetProperty(ref _isPaymentGridVisible, value))
                    OnPropertyChanged(nameof(IsPaymentWorkflowVisible));
            }
        }

        public bool IsPaymentPanelVisible => IsPaymentInputVisible && !string.IsNullOrWhiteSpace(SelectedPaymentMethod);
        public bool IsPaymentTypeCash => string.Equals(SelectedPaymentMethod, CashPaymentMethod, StringComparison.OrdinalIgnoreCase);
        public bool IsPaymentTypeCard => string.Equals(SelectedPaymentMethod, CardPaymentMethod, StringComparison.OrdinalIgnoreCase);
        public bool IsPaymentTypeCredit => string.Equals(SelectedPaymentMethod, CreditPaymentMethod, StringComparison.OrdinalIgnoreCase);
        public bool IsPaymentTypeBankTransfer => string.Equals(SelectedPaymentMethod, BankTransferPaymentMethod, StringComparison.OrdinalIgnoreCase);
        public bool IsNonCashPaymentType => IsPaymentTypeCard || IsPaymentTypeCredit || IsPaymentTypeBankTransfer;
        public string PaymentTypeDisplay => GetPaymentTypeDisplayText();

        // --- Card / Terminal ---
        public ObservableCollection<PaymentTerminal> PaymentTerminals { get; } = new ObservableCollection<PaymentTerminal>();

        private PaymentTerminal _selectedPaymentTerminal;
        public PaymentTerminal SelectedPaymentTerminal
        {
            get => _selectedPaymentTerminal;
            set
            {
                if (SetProperty(ref _selectedPaymentTerminal, value))
                {
                    ValidatePayment();
                    RaiseSaveCommandState();
                }
            }
        }

        private string _referenceNumber;
        public string ReferenceNumber
        {
            get => _referenceNumber;
            set
            {
                var normalizedValue = IsPaymentTypeCard ? KeepDigitsOnly(value) : value;
                if (IsPaymentTypeCard && normalizedValue?.Length > 4)
                    normalizedValue = normalizedValue.Substring(0, 4);

                if (SetProperty(ref _referenceNumber, normalizedValue))
                {
                    OnPropertyChanged(nameof(CardReferenceNumber));
                    ValidatePayment();
                    RaiseSaveCommandState();
                }
            }
        }

        public string CardReferenceNumber
        {
            get => ReferenceNumber;
            set => ReferenceNumber = value;
        }

        // Totals
        private decimal _subTotal;
        public decimal SubTotal { get => _subTotal; set => SetProperty(ref _subTotal, value); }

        private decimal _lineDiscountPercent;
        public decimal LineDiscountPercent
        {
            get => _lineDiscountPercent;
            private set { if (SetProperty(ref _lineDiscountPercent, 0m)) { CalculateTotals(); } }
        }

        public bool IsDiscountEnabled => false;

        private bool _isPromoLoyaltyPanelVisible;
        public bool IsPromoLoyaltyPanelVisible { get => _isPromoLoyaltyPanelVisible; set => SetProperty(ref _isPromoLoyaltyPanelVisible, value); }

        private decimal _billDiscount;
        public decimal BillDiscount
        {
            get => _billDiscount;
            private set => SetProperty(ref _billDiscount, 0m);
        }

        private int _availableLoyaltyPoints;
        public int AvailableLoyaltyPoints
        {
            get => _availableLoyaltyPoints;
            set
            {
                if (SetProperty(ref _availableLoyaltyPoints, value))
                {
                    OnPropertyChanged(nameof(MaxRedeemablePoints));
                    OnPropertyChanged(nameof(PotentialEarnPoints));
                }
            }
        }

        private int _appliedLoyaltyPoints;
        public int AppliedLoyaltyPoints
        {
            get => _appliedLoyaltyPoints;
            set
            {
                if (SetProperty(ref _appliedLoyaltyPoints, value))
                {
                    OnPropertyChanged(nameof(HasAppliedLoyaltyPoints));
                    OnPropertyChanged(nameof(AppliedDiscountDisplay));
                }
            }
        }

        private decimal _loyaltyDiscountAmount;
        public decimal LoyaltyDiscountAmount
        {
            get => _loyaltyDiscountAmount;
            set => SetProperty(ref _loyaltyDiscountAmount, value);
        }

        private string _redeemLoyaltyPointsInput;
        public string RedeemLoyaltyPointsInput
        {
            get => _redeemLoyaltyPointsInput;
            set => SetProperty(ref _redeemLoyaltyPointsInput, value);
        }

        private string _discountCodeInput;
        public string DiscountCodeInput
        {
            get => _discountCodeInput;
            set => SetProperty(ref _discountCodeInput, value);
        }

        private string _appliedDiscountCode;
        public string AppliedDiscountCode
        {
            get => _appliedDiscountCode;
            set
            {
                if (SetProperty(ref _appliedDiscountCode, value))
                {
                    OnPropertyChanged(nameof(HasAppliedDiscountCode));
                    OnPropertyChanged(nameof(AppliedDiscountDisplay));
                    OnPropertyChanged(nameof(AppliedDiscountLabel));
                    OnPropertyChanged(nameof(HasAppliedDiscount));
                }
            }
        }

        private string _appliedDiscountName;
        public string AppliedDiscountName
        {
            get => _appliedDiscountName;
            set => SetProperty(ref _appliedDiscountName, value);
        }

        public bool HasAppliedDiscountCode => !string.IsNullOrWhiteSpace(AppliedDiscountCode);

        public decimal AppliedDiscountPercent
        {
            get => _appliedDiscountPercent;
            private set
            {
                if (SetProperty(ref _appliedDiscountPercent, value))
                    OnPropertyChanged(nameof(AppliedDiscountLabel));
            }
        }

        public decimal AppliedDiscountAmount
        {
            get => _appliedDiscountAmount;
            private set
            {
                if (SetProperty(ref _appliedDiscountAmount, value))
                    OnPropertyChanged(nameof(HasAppliedDiscount));
            }
        }

        public string AppliedDiscountLabel
        {
            get
            {
                var discountCode = GetActiveAppliedDiscountCode();
                return string.IsNullOrWhiteSpace(discountCode)
                    ? string.Empty
                    : $"APPLIED DISCOUNT: {discountCode} ({AppliedDiscountPercent:0.##}%)";
            }
        }

        public bool HasAppliedDiscount =>
            AppliedDiscountAmount > 0 &&
            (!string.IsNullOrWhiteSpace(AppliedDiscountCode) ||
             !string.IsNullOrWhiteSpace(_appliedAutoBillDiscountName));

        public bool HasAppliedLoyaltyPoints => AppliedLoyaltyPoints > 0;
        public int MaxRedeemablePoints => Math.Max(0, AvailableLoyaltyPoints);
        public int PotentialEarnPoints => NetAmount <= 0 ? 0 : (int)Math.Floor(NetAmount / LoyaltySpendAmountPerPoint);

        public string AppliedAutoDiscountName
        {
            get => _appliedAutoDiscountName;
            private set
            {
                if (SetProperty(ref _appliedAutoDiscountName, value))
                {
                    OnPropertyChanged(nameof(HasAppliedAutoDiscount));
                    OnPropertyChanged(nameof(AppliedDiscountDisplay));
                }
            }
        }

        public bool HasAppliedAutoDiscount => !string.IsNullOrWhiteSpace(AppliedAutoDiscountName);

        public string AppliedDiscountDisplay =>
            HasAppliedDiscountCode ? AppliedDiscountCode :
            HasAppliedAutoDiscount ? AppliedAutoDiscountName :
            HasAppliedLoyaltyPoints ? $"LOYALTY ({AppliedLoyaltyPoints} PTS)" :
            string.Empty;

        private bool _isServiceChargeEnabled;
        public bool IsServiceChargeEnabled
        {
            get => _isServiceChargeEnabled;
            set { if (SetProperty(ref _isServiceChargeEnabled, value)) { CalculateTotals(); } }
        }

        private decimal _serviceChargeAmount;
        public decimal ServiceChargeAmount { get => _serviceChargeAmount; set => SetProperty(ref _serviceChargeAmount, value); }

        private bool _isTaxEnabled;
        public bool IsTaxEnabled
        {
            get => _isTaxEnabled;
            set { if (SetProperty(ref _isTaxEnabled, value)) { CalculateTotals(); } }
        }

        private decimal _taxAmount;
        public decimal TaxAmount { get => _taxAmount; set => SetProperty(ref _taxAmount, value); }

        private decimal _netAmount;
        public decimal NetAmount { get => _netAmount; set => SetProperty(ref _netAmount, value); }

        private decimal _currentPaymentAmount;
        public decimal CurrentPaymentAmount
        {
            get => _currentPaymentAmount;
            set
            {
                if (SetProperty(ref _currentPaymentAmount, value))
                {
                    OnPropertyChanged(nameof(CashGiven));
                    OnPropertyChanged(nameof(CustomerGaveAmount));
                    CalculateChange();
                    ValidatePayment();
                    RaiseSaveCommandState();
                }
            }
        }

        public decimal CashGiven
        {
            get => CurrentPaymentAmount;
            set => CurrentPaymentAmount = value;
        }

        public decimal CustomerGaveAmount
        {
            get => CurrentPaymentAmount;
            set => CurrentPaymentAmount = value;
        }

        private decimal _changeAmount;
        public decimal ChangeAmount { get => _changeAmount; set => SetProperty(ref _changeAmount, value); }

        private decimal _remainingBalance;
        public decimal RemainingBalance
        {
            get => _remainingBalance;
            set
            {
                if (SetProperty(ref _remainingBalance, value))
                {
                    OnPropertyChanged(nameof(HasRemainingBalance));
                    OnPropertyChanged(nameof(ChangeDueAmount));
                    CalculateChange();
                    RaiseSaveCommandState();
                }
            }
        }

        public decimal ChangeDueAmount => RemainingBalance < 0 ? Math.Abs(RemainingBalance) : 0m;
        public bool HasRemainingBalance => RemainingBalance > 0;
        public bool HasAppliedPayments => AppliedPayments.Any();
        public bool IsPaymentWorkflowVisible => IsPaymentPanelVisible || HasAppliedPayments;

        private int _currentOpenSalesId;
        public int CurrentOpenSalesId
        {
            get => _currentOpenSalesId;
            set
            {
                if (SetProperty(ref _currentOpenSalesId, value))
                    (PrintPreBillCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        // State
        private bool _isProcessing;
        public bool IsProcessing { get => _isProcessing; set => SetProperty(ref _isProcessing, value); }

        private bool _isOverlayVisible;
        public bool IsOverlayVisible
        {
            get { return _isOverlayVisible; }
            set
            {
                _isOverlayVisible = value;
                OnPropertyChanged(nameof(IsOverlayVisible));
            }
        }

        #endregion

        #region Validation Properties
        private string _paymentErrorMessage;
        public string PaymentErrorMessage
        {
            get => _paymentErrorMessage;
            set => SetProperty(ref _paymentErrorMessage, value);
        }

        // Helper to check if payment is valid
        public bool IsPaymentValid =>
            IsPaymentPanelVisible &&
            CurrentPaymentAmount > 0 &&
            (!IsPaymentTypeCard || (SelectedPaymentTerminal != null && HasFourDigitReference(ReferenceNumber))) &&
            (!IsPaymentTypeCredit || (HasSelectedStoreCreditCustomer() && !string.IsNullOrWhiteSpace(ReferenceNumber))) &&
            (!IsPaymentTypeBankTransfer || !string.IsNullOrWhiteSpace(ReferenceNumber));
        #endregion

        #region Initialization & Data Loading
        private void InitializeCollections()
        {
            CartItems = new ObservableCollection<SalesLine>();
            Categories = new ObservableCollection<MenuCategory>();
            Customers = new ObservableCollection<Customer>();
            Products = new ObservableCollection<MenuVariantDto>();
            ServedOrders = new ObservableCollection<ServedOrderDto>();

            FilteredCustomers = CollectionViewSource.GetDefaultView(Customers);
            FilteredCustomers.Filter = FilterCustomer;

            FilteredProducts = CollectionViewSource.GetDefaultView(Products);
            FilteredProducts.Filter = FilterProduct;

            // Listen for changes in the cart to recalculate totals
            CartItems.CollectionChanged += (s, e) =>
            {
                if (e.NewItems != null)
                    foreach (SalesLine item in e.NewItems) item.PropertyChanged += CartItem_PropertyChanged;

                if (e.OldItems != null)
                    foreach (SalesLine item in e.OldItems) item.PropertyChanged -= CartItem_PropertyChanged;

                if (!_isUpdatingCart)
                    CalculateTotals();
            };

            AppliedPayments.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(HasAppliedPayments));
                IsPaymentGridVisible = AppliedPayments.Any();
                OnPropertyChanged(nameof(IsPaymentWorkflowVisible));
                CalculateTotals();
            };
        }
        private void InitializeCommands()
        {
            AddProductCommand = new AsyncRelayCommand(
                async parameter => await ExecuteAddProductAsync(parameter as MenuVariantDto),
                parameter => parameter is MenuVariantDto || SelectedProduct != null);
            RemoveItemCommand = new RelayCommand(
                parameter => ExecuteRemoveItem(parameter as SalesLine),
                parameter => (parameter as SalesLine ?? SelectedCartItem) != null);
            IncreaseQuantityCommand = new RelayCommand(
                parameter => ExecuteChangeQuantity(parameter as SalesLine, 1),
                parameter => parameter is SalesLine);
            DecreaseQuantityCommand = new RelayCommand(
                parameter => ExecuteChangeQuantity(parameter as SalesLine, -1),
                parameter => parameter is SalesLine);
            CancelInvoiceCommand = new RelayCommand(_ => ExecuteCancelInvoice());

            EnableDiscountCommad = new RelayCommand(_ => MessageBox.Show("Bill discounts are handled through the manager register.", "Discount", MessageBoxButton.OK, MessageBoxImage.Information));
            AddCustomerCommand = new AsyncRelayCommand(async _ => await ExecuteAddCustomerCommand());
            OpenCashInOutCommand = new RelayCommand(ExecuteOpenCashInOut);
            OpenSalesReturnCommand = new RelayCommand(ExecuteOpenSalesReturn);
            CloseShiftCommand = new AsyncRelayCommand(async _ => await ExecuteCloseShiftAsync(), _ => CurrentUser.CurrentShiftId.HasValue);
            ClearPaymentMethodCommand = new RelayCommand(_ =>
            {
                SelectedPaymentMethod = null;
                IsPaymentInputVisible = false;
            });
            AddPaymentCommand = new RelayCommand(_ => AddCurrentPayment(), _ => CanAttemptAddPayment());
            RemovePaymentCommand = new RelayCommand<PaymentDetail>(RemoveAppliedPayment, payment => payment != null);
            SaveSaleCommand = new AsyncRelayCommand(async _ => await SaveSalesAsync(printBill: false), _ => CanSaveSale());
            SaveAndPrintCommand = new AsyncRelayCommand(async _ => await SaveSalesAsync(printBill: true), _ => CanSaveSale());
            PrintPreBillCommand = new AsyncRelayCommand(async _ => await PrintPreBillAsync(), _ => CanPrintPreBill());
            RecallHeldSaleCommand = new AsyncRelayCommand(async _ => await RecallHeldSaleAsync());
            SelectPaymentMethodCommand = new RelayCommand<SalesPaymentMethod>(SelectPaymentMethod);
            SelectCategoryCommand = new RelayCommand<MenuCategory>(SelectCategory, category => category != null);
            RefreshOrdersCommand = new AsyncRelayCommand(async _ =>
            {
                await LoadActivePricingRulesAsync();
                await LoadServedOrdersAsync();
            });
            ApplyDiscountCodeCommand = new AsyncRelayCommand(async _ => await ApplyDiscountCodeAsync());
            RemoveAppliedDiscountCommand = new RelayCommand(_ => RemoveAppliedDiscount());
            ApplyLoyaltyCommand = new RelayCommand(_ => ApplyLoyaltyRedemption());
            RemoveLoyaltyCommand = new RelayCommand(_ => RemoveLoyaltyRedemption(), _ => HasAppliedLoyaltyPoints);
            TogglePromoLoyaltyPanelCommand = new RelayCommand(_ => IsPromoLoyaltyPanelVisible = !IsPromoLoyaltyPanelVisible);
            OpenRoomBookingCommand = new RelayCommand(_ => ExecuteOpenRoomBooking());
            OpenAdminDashboardCommand = new RelayCommand(
                _ => OpenAdminDashboard(),
                _ => _userSessionService.HasPermission("ACCESS_ADMIN_PANEL"));
        }
        public async Task InitializeAsync()
        {
            if (_isDataLoaded || _isLoadingData) return;

            _isLoadingData = true;
            try
            {
                await LoadDataAsync();
                _isDataLoaded = true;
            }
            finally
            {
                _isLoadingData = false;
            }
        }

        private async Task LoadDataAsync()
        {
            IsProcessing = true;
            var loadStep = "starting";
            try
            {
                loadStep = "loading customers";
                var customers = await _customerRepository.GetAllAsync();
                foreach (var c in customers) Customers.Add(c);
                FilteredCustomers.Refresh();
                SelectedCustomer = Customers.FirstOrDefault(c => c.IsDefault);

                loadStep = "loading menu categories";
                await LoadCategoriesAsync();

                loadStep = "loading menu items";
                await LoadMenuItemsAsync();

                loadStep = "loading tax configurations";
                var taxConfigs = await _taxConfigurationRepository.GetAllAsync();
                _taxConfigurations = taxConfigs.ToList();

                loadStep = "loading active pricing rules";
                await LoadActivePricingRulesAsync();

                loadStep = "loading payment terminals";
                var terminals = await _paymentTerminalRepository.GetAllAsync();
                foreach (var t in terminals.Where(t => t.IsActive && t.BranchId == _userSessionService.BranchId))
                    PaymentTerminals.Add(t);

                // Pre-select the first terminal so the cashier doesn't have to pick one manually.
                if (SelectedPaymentTerminal == null && PaymentTerminals.Any())
                    SelectedPaymentTerminal = PaymentTerminals.First();

                loadStep = "loading served orders";
                await LoadServedOrdersAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load Sales screen data while {loadStep}:\n{GetExceptionDetail(ex)}",
                    "Sales Load Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally { IsProcessing = false; }
        }
        public async Task LoadServedOrdersAsync()
        {
            var orders = await _orderRepository.GetServedOrdersAsync(_userSessionService.BranchId);
            ServedOrders.Clear();
            foreach (var o in orders) ServedOrders.Add(o);
        }

        private async Task LoadCategoriesAsync()
        {
            try
            {
                var allCategories = (await _menuCategoryRepository.GetAllAsync()).ToList();

                var parentIds = allCategories
                    .Where(c => c.ParentId.HasValue)
                    .Select(c => c.ParentId.Value)
                    .Distinct()
                    .ToHashSet();

                var allCategory = new MenuCategory
                {
                    Id = 0,
                    Name = "All",
                    DisplayOrder = -1,
                    IsActive = true
                };

                Categories.Clear();
                Categories.Add(allCategory);

                var leafCategories = allCategories
                    .Where(c => c.IsActive && !parentIds.Contains(c.Id))
                    .OrderBy(c => c.DisplayOrder)
                    .ThenBy(c => c.Name);

                foreach (var category in leafCategories)
                {
                    Categories.Add(category);
                }

                SelectedCategory = allCategory;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load menu categories:\n{GetExceptionDetail(ex)}",
                    "Category Load Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task LoadMenuItemsAsync()
        {
            try
            {
                var products = await _menuItemRepository.GetAllVariantsForSalesAsync();

                Products.Clear();
                _productByCode.Clear();
                foreach (var p in products)
                {
                    p.ImageUrl = _storageService.GetSecureFileUrl(p.ImageUrl);
                    Products.Add(p);
                    AddProductLookup(p.ItemCode, p);
                    AddProductLookup(p.Barcode, p);
                }

                RefreshProductFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load menu items:\n{GetExceptionDetail(ex)}",
                    "Menu Item Load Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static string GetExceptionDetail(Exception ex)
        {
            return ex.InnerException == null
                ? ex.Message
                : $"{ex.Message}\n\nInner: {ex.InnerException.Message}";
        }
        #endregion

        #region Cart Logic
        private void CartItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // If Quantity changes in the Grid, recalculate the whole invoice
            if (e.PropertyName == nameof(SalesLine.Quantity) || e.PropertyName == nameof(SalesLine.ManualDiscount))
            {
                if (!_isUpdatingCart)
                    CalculateTotals();
            }
        }
        private async Task ExecuteAddProductAsync()
        {
            await ExecuteAddProductAsync(SelectedProduct);
        }

        private async Task ExecuteAddProductAsync(MenuVariantDto product)
        {
            var productToAdd = product ?? SelectedProduct;
            if (productToAdd == null) return;

            if (!ReferenceEquals(SelectedProduct, productToAdd))
                SelectedProduct = productToAdd;

            decimal qtyToAdd = Quantity > 0 ? Quantity : 1;

            if (IsRetailItem(productToAdd))
            {
                var terminalLocationId = GetPositiveAppSetting(TerminalLocationIdSettingName);
                if (terminalLocationId <= 0)
                {
                    ShowWarning("Retail stock location is not configured for this terminal.");
                    return;
                }

                var availableStock = await _inventoryRepository.GetRetailItemStockAsync(productToAdd.VariantId, terminalLocationId);
                var currentCartQty = CartItems
                    .Where(x =>
                        !x.IsAutoGeneratedPromotionLine &&
                        !x.IsImportedOrderLine &&
                        x.ProductId == productToAdd.VariantId)
                    .Sum(x => x.Quantity);

                if ((currentCartQty + qtyToAdd) > availableStock)
                {
                    ShowWarning($"Insufficient Stock. Only {availableStock} items available at this location.");
                    return;
                }
            }

            // 1. Check if Item exists in Cart (Merge)
            var existing = CartItems.FirstOrDefault(x =>
                !x.IsAutoGeneratedPromotionLine &&
                !x.IsImportedOrderLine &&
                x.ProductId == productToAdd.VariantId);

            if (existing != null)
            {
                _isUpdatingCart = true;
                try
                {
                    existing.Quantity += qtyToAdd;
                    var currentIndex = CartItems.IndexOf(existing);
                    if (currentIndex > 0)
                        CartItems.Move(currentIndex, 0);
                }
                finally
                {
                    _isUpdatingCart = false;
                }
            }
            else
            {
                decimal maxStock = decimal.MaxValue;

                var line = new SalesLine
                {
                    Number = CartItems.Count + 1,
                    ProductId = productToAdd.VariantId,
                    MenuCategoryId = productToAdd.MenuCategoryId,
                    ProductName = productToAdd.DisplayName,
                    Note = "",
                    UnitPrice = SellingPrice,
                    ManualDiscount = Discount,
                    BaseDiscountPerUnit = Math.Max(0, productToAdd.DiscountAmount ?? 0),
                    BaseDiscountLabel = (productToAdd.DiscountAmount ?? 0) > 0 ? "Item off" : null,
                    AvailableQuantity = maxStock,
                    Quantity = qtyToAdd,
                    TaxIds = productToAdd.TaxIds.ToList()
                };

                _isUpdatingCart = true;
                try
                {
                    CartItems.Insert(0, line);
                }
                finally
                {
                    _isUpdatingCart = false;
                }
            }

            CalculateTotals();
            ClearProductEntryInputs();
        }

        private bool IsRetailItem(MenuVariantDto product)
        {
            var retailCategoryId = GetPositiveAppSetting(RetailCategoryIdSettingName);
            return product != null &&
                   retailCategoryId > 0 &&
                   product.MenuCategoryId == retailCategoryId;
        }

        private static int GetPositiveAppSetting(string key)
        {
            var rawValue = ConfigurationManager.AppSettings[key];
            return int.TryParse(rawValue, out var value) && value > 0 ? value : 0;
        }

        private static void ShowWarning(string message)
        {
            SystemSounds.Hand.Play();
            MessageBox.Show(message, "Stock Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void ExecuteRemoveItem(SalesLine item = null)
        {
            var lineToRemove = item ?? SelectedCartItem;
            if (lineToRemove != null)
            {
                if (lineToRemove.IsAutoGeneratedPromotionLine)
                {
                    MessageBox.Show(
                        "This line was added automatically by a promotion. Change the qualifying item to remove it.",
                        "Promotion",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                if (lineToRemove.IsImportedOrderLine)
                {
                    MessageBox.Show(
                        "This line came from a served restaurant order and cannot be removed here.",
                        "Restaurant Order",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                _isUpdatingCart = true;
                try
                {
                    CartItems.Remove(lineToRemove);
                    RenumberCartItems();
                }
                finally
                {
                    _isUpdatingCart = false;
                }

                CalculateTotals();
            }
        }

        private void ExecuteChangeQuantity(SalesLine line, int delta)
        {
            if (line == null || line.IsQuantityLocked)
                return;

            var newQuantity = line.Quantity + delta;
            if (newQuantity < 1)
                return;

            // Setting Quantity raises PropertyChanged, which CartItem_PropertyChanged
            // already routes into CalculateTotals() - no explicit call needed here.
            line.Quantity = newQuantity;
        }

        private bool CanAddPayment()
        {
            if (CartItems?.Any() != true) return false;
            if (!IsPaymentPanelVisible) return false;
            if (CurrentPaymentAmount <= 0) return false;
            if (!IsPaymentTypeCash && CurrentPaymentAmount > RemainingBalance) return false;

            if (IsPaymentTypeCard &&
                (SelectedPaymentTerminal == null || !HasFourDigitReference(ReferenceNumber)))
                return false;

            if (IsPaymentTypeCredit &&
                (!HasSelectedStoreCreditCustomer() || string.IsNullOrWhiteSpace(ReferenceNumber)))
                return false;

            if (IsPaymentTypeBankTransfer && string.IsNullOrWhiteSpace(ReferenceNumber))
                return false;

            return true;
        }

        private bool CanAttemptAddPayment()
        {
            return CartItems?.Any() == true && IsPaymentPanelVisible;
        }

        private void AddCurrentPayment()
        {
            if (!CanAddPayment())
            {
                ValidatePayment();
                var message = string.IsNullOrWhiteSpace(PaymentErrorMessage)
                    ? "Complete the payment details before adding this payment."
                    : PaymentErrorMessage;

                MessageBox.Show(message, "Payment Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            AppliedPayments.Add(new PaymentDetail
            {
                PaymentTerminalId = IsPaymentTypeCard ? SelectedPaymentTerminal?.Id : null,
                PaymentMethod = SelectedPaymentMethod,
                Amount = CurrentPaymentAmount,
                ReferenceNumber = RequiresReferenceNumber() ? ReferenceNumber?.Trim() : null
            });

            ClearPaymentEntryInputs();
            IsPaymentInputVisible = false;
            IsPaymentGridVisible = true;
            PaymentErrorMessage = string.Empty;
            OnPropertyChanged(nameof(IsPaymentValid));
            RaiseSaveCommandState();
        }

        private void RemoveAppliedPayment(PaymentDetail payment)
        {
            if (payment == null) return;

            AppliedPayments.Remove(payment);
            IsPaymentGridVisible = AppliedPayments.Any();
            ValidatePayment();
            RaiseSaveCommandState();
        }

        private void ClearPaymentEntryInputs()
        {
            CurrentPaymentAmount = 0m;
            ReferenceNumber = string.Empty;
            PaymentErrorMessage = string.Empty;
            OnPropertyChanged(nameof(IsPaymentValid));
        }

        private async Task LoadRestaurantOrderIntoCart(ServedOrderDto order)
        {
            _currentRestaurantOrderId = order?.OrderId;
            ResetImportedOrderPricingState();
            CartItems.Clear();
            var items = await _orderRepository.GetOrderItemsAsync(order.OrderId);

            int sequence = 1;

            foreach (var item in items)
            {
                var productRef = Products.FirstOrDefault(x => x.VariantId == item.VariantId);

                var line = new SalesLine
                {
                    Number = sequence++,
                    ProductId = item.VariantId,
                    MenuCategoryId = productRef?.MenuCategoryId,
                    ProductName = item.DisplayName,
                    UnitPrice = item.UnitPrice,
                    AvailableQuantity = decimal.MaxValue,
                    Quantity = item.Quantity,
                    BaseDiscountPerUnit = item.DiscountAmount,
                    BaseDiscountLabel = item.DiscountAmount > 0
                        ? (!string.IsNullOrWhiteSpace(item.OfferName) ? item.OfferName : "Order item discount")
                        : null,
                    IsImportedOrderLine = true,
                    Note = item.Note,
                    TaxIds = productRef?.TaxIds?.ToList() ?? new List<int>()
                };

                CartItems.Insert(0, line);
            }
            CalculateTotals();
        }
        private void ResetImportedOrderPricingState()
        {
            _autoBillDiscountAmount = 0;
            AppliedAutoDiscountName = string.Empty;

            _appliedDiscountValidation = null;
            AppliedDiscountCode = string.Empty;
            AppliedDiscountName = string.Empty;
            DiscountCodeInput = string.Empty;

            AppliedLoyaltyPoints = 0;
            LoyaltyDiscountAmount = 0;
            RedeemLoyaltyPointsInput = string.Empty;

            LineDiscountPercent = 0;
            IsPromoLoyaltyPanelVisible = false;
        }
        private void ClearProductEntryInputs()
        {
            SelectedProduct = null;
            Barcode = string.Empty;
            SellingPrice = 0;
            Quantity = 1; // RESET TO DEFAULT
            Discount = 0;
            RefreshProductFilter();
        }

        private void SelectCategory(MenuCategory category)
        {
            if (category == null) return;
            SelectedCategory = category;
        }

        private void RefreshProductFilter()
        {
            if (FilteredProducts == null) return;

            _productFilterDebounceTimer?.Stop();
            FilteredProducts.Filter = FilterProduct;
            FilteredProducts.Refresh();
        }

        private void QueueProductFilterRefresh()
        {
            if (FilteredProducts == null || _productFilterDebounceTimer == null)
                return;

            _productFilterDebounceTimer.Stop();
            _productFilterDebounceTimer.Start();
        }

        private void AddProductLookup(string code, MenuVariantDto product)
        {
            var key = (code ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(key) || product == null)
                return;

            if (!_productByCode.ContainsKey(key))
                _productByCode.Add(key, product);
        }

        private bool FilterProduct(object item)
        {
            var product = item as MenuVariantDto;
            if (product == null) return false;

            if (SelectedCategory != null &&
                SelectedCategory.Id > 0 &&
                product.MenuCategoryId != SelectedCategory.Id)
            {
                return false;
            }

            var term = (Barcode ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(term))
                return true;

            return (!string.IsNullOrWhiteSpace(product.ItemCode) &&
                    product.ItemCode.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0) ||
                   (!string.IsNullOrWhiteSpace(product.Barcode) &&
                    product.Barcode.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void ApplyProductFilterByCode(string code)
        {
            if (FilteredProducts == null) return;

            var term = (code ?? string.Empty).Trim();

            _productFilterDebounceTimer?.Stop();
            RefreshProductFilter();

            // Physical barcode/item-code scans must resolve across all products, independent of the selected category chip.
            _productByCode.TryGetValue(term, out var exactMatch);

            _suppressProductSelectionTrigger = true;
            SelectedProduct = exactMatch;
            _suppressProductSelectionTrigger = false;
        }

        public bool SubmitBarcodeEntry(string code)
        {
            var term = (code ?? string.Empty).Trim();
            Barcode = term;
            ApplyProductFilterByCode(term);

            if (SelectedProduct == null || AddProductCommand?.CanExecute(null) != true)
                return false;

            AddProductCommand.Execute(null);
            return true;
        }
        #endregion

        #region Calculation Logic
        private void CalculateTotals()
        {
            if (_isCalculatingTotals)
                return;

            _isCalculatingTotals = true;
            try
            {
                var hasImportedRestaurantOrder = CartItems.Any(x => x.IsImportedOrderLine);
                if (!hasImportedRestaurantOrder)
                    QueueActiveAutoDiscountRulesRefresh(CalculateAutoDiscountRuleSubTotal());

                ApplyAutoPromotions();

                if (hasImportedRestaurantOrder)
                {
                    _autoBillDiscountAmount = 0;
                    _appliedAutoBillDiscountRule = null;
                    _appliedAutoBillDiscountName = string.Empty;
                    AppliedAutoDiscountName = string.Empty;
                }

                SubTotal = CartItems.Sum(x => x.Amount);

                decimal baseBillDiscount;
                decimal appliedDiscountAmount = 0;
                decimal appliedDiscountPercent = 0;
                if (hasImportedRestaurantOrder)
                {
                    baseBillDiscount = 0;
                }
                else if (_appliedDiscountValidation != null)
                {
                    baseBillDiscount = CalculateDiscountAmount(SubTotal, _appliedDiscountValidation);
                    appliedDiscountAmount = baseBillDiscount;
                    appliedDiscountPercent = CalculateDisplayDiscountPercent(
                        SubTotal,
                        _appliedDiscountValidation.DiscountType,
                        _appliedDiscountValidation.DiscountValue,
                        appliedDiscountAmount);
                }
                else if (_autoBillDiscountAmount > 0)
                {
                    baseBillDiscount = _autoBillDiscountAmount;
                    appliedDiscountAmount = baseBillDiscount;
                    appliedDiscountPercent = _appliedAutoBillDiscountRule == null
                        ? CalculateDisplayDiscountPercent(SubTotal, null, 0, appliedDiscountAmount)
                        : CalculateDisplayDiscountPercent(
                            SubTotal,
                            _appliedAutoBillDiscountRule.DiscountType,
                            _appliedAutoBillDiscountRule.DiscountValue,
                            appliedDiscountAmount);
                }
                else
                {
                    baseBillDiscount = (SubTotal * LineDiscountPercent) / 100m;
                }

                AppliedDiscountAmount = appliedDiscountAmount;
                AppliedDiscountPercent = appliedDiscountPercent;
                NotifyAppliedDiscountProperties();

                var maxLoyaltyDiscount = Math.Max(0, SubTotal - baseBillDiscount);
                if (!hasImportedRestaurantOrder && AppliedLoyaltyPoints > 0)
                {
                    LoyaltyDiscountAmount = Math.Min(maxLoyaltyDiscount, AppliedLoyaltyPoints * LoyaltyRedeemValuePerPoint);
                }
                else
                {
                    LoyaltyDiscountAmount = 0;
                }

                BillDiscount = baseBillDiscount + LoyaltyDiscountAmount;

                decimal taxableValue = SubTotal - BillDiscount;

                ServiceChargeAmount = IsServiceChargeEnabled ? (taxableValue * 0.10m) : 0;

                decimal taxBase = taxableValue + ServiceChargeAmount;
                TaxAmount = IsTaxEnabled ? (taxBase * 0.18m) : 0;

                NetAmount = taxableValue + ServiceChargeAmount + TaxAmount;
                RemainingBalance = NetAmount - AppliedPayments.Sum(x => x.Amount);

                if (IsPaymentTypeCash)
                {
                    CalculateChange();
                    ValidatePayment();
                }

                RaiseSaveCommandState();
            }
            finally
            {
                _isCalculatingTotals = false;
            }
        }
        private void CalculateChange()
        {
            ChangeAmount = CurrentPaymentAmount - Math.Max(RemainingBalance, 0m);
        }
        private void ExecuteCancelInvoice()
        {
            _currentRestaurantOrderId = null;
            CartItems.Clear();
            AppliedPayments.Clear();
            CurrentOpenSalesId = 0;
            SelectedPaymentMethod = null;
            IsPaymentInputVisible = false;
            IsPaymentGridVisible = false;
            SelectedServedOrder = null;
            GenerateInvoiceNumber();
            LineDiscountPercent = 0;
            IsPromoLoyaltyPanelVisible = false;
            RemoveAppliedDiscount(true);
            RemoveLoyaltyRedemption(true);
            Quantity = 1;
            SelectedPaymentTerminal = null;
            ReferenceNumber = string.Empty;
            CurrentPaymentAmount = 0m;
            CalculateTotals();
        }
        #endregion

        #region Commands
        public ICommand EnableDiscountCommad { get; private set; }
        public ICommand AddProductCommand { get; private set; }
        public ICommand AddCustomerCommand { get; private set; }
        public ICommand OpenCashInOutCommand { get; private set; }
        public ICommand OpenSalesReturnCommand { get; private set; }
        public ICommand RemoveItemCommand { get; private set; }
        public ICommand IncreaseQuantityCommand { get; private set; }
        public ICommand DecreaseQuantityCommand { get; private set; }
        public ICommand SaveSaleCommand { get; private set; }
        public ICommand SaveAndPrintCommand { get; private set; }
        public ICommand PrintPreBillCommand { get; private set; }
        public ICommand RecallHeldSaleCommand { get; private set; }
        public ICommand CancelInvoiceCommand { get; private set; }
        public ICommand SelectPaymentMethodCommand { get; private set; }
        public ICommand SelectCategoryCommand { get; private set; }
        public ICommand RefreshOrdersCommand { get; private set; }
        public ICommand ClearPaymentMethodCommand { get; private set; }
        public ICommand AddPaymentCommand { get; private set; }
        public ICommand RemovePaymentCommand { get; private set; }
        public ICommand ApplyDiscountCodeCommand { get; private set; }
        public ICommand RemoveAppliedDiscountCommand { get; private set; }
        public ICommand ApplyLoyaltyCommand { get; private set; }
        public ICommand RemoveLoyaltyCommand { get; private set; }
        public ICommand TogglePromoLoyaltyPanelCommand { get; private set; }
        public ICommand OpenRoomBookingCommand { get; private set; }
        public ICommand CloseShiftCommand { get; private set; }
        public ICommand LogoutCommand => new RelayCommand(_ => Logout());
        public ICommand OpenAdminDashboardCommand { get; private set; }
        #endregion

        #region Saving
        private bool CanSaveSale()
        {
            // 1. Must have items
            if (CartItems?.Any() != true) return false;

            // 2. Must have at least one applied payment
            if (!AppliedPayments.Any()) return false;

            // 3. Payment total must cover the bill
            if (RemainingBalance > 0) return false;

            return AppliedPayments.All(payment =>
                payment.Amount > 0 &&
                !string.IsNullOrWhiteSpace(payment.PaymentMethod));
        }

        private bool CanPrintPreBill()
        {
            return CartItems?.Any() == true && CurrentOpenSalesId <= 0;
        }

        private async Task SaveSalesAsync(bool printBill = false)
        {
            if (!CanSaveSale()) return;

            try
            {
                IsProcessing = true;

                if (HasAppliedDiscountCode)
                {
                    var latestValidation = await _discountRepository.ValidateBillDiscountCodeAsync(AppliedDiscountCode, CurrentUser.BranchId, SubTotal);
                    if (!latestValidation.IsValid)
                        throw new InvalidOperationException(latestValidation.Message);

                    _appliedDiscountValidation = latestValidation;
                    CalculateTotals();
                }

                if (HasAppliedLoyaltyPoints)
                {
                    if ((SelectedCustomer?.Id ?? 0) <= 0)
                        throw new InvalidOperationException("Select a customer before redeeming loyalty points.");

                    var latestPoints = await _customerRepository.GetLoyaltyPointsAsync(SelectedCustomer.Id);
                    if (latestPoints < AppliedLoyaltyPoints)
                        throw new InvalidOperationException("Customer loyalty points are insufficient for this redemption.");
                }

                if (!CanSaveSale())
                    throw new InvalidOperationException("Apply payments until the remaining balance is settled.");

                decimal balanceToPay = NetAmount; // Use your ViewModel's NetAmount property
                var cappedPayments = new List<PaymentDetail>();

                foreach (var payment in AppliedPayments)
                {
                    // Capps the payment so it never exceeds what is owed
                    decimal amountToApply = Math.Min(payment.Amount, balanceToPay);

                    if (amountToApply > 0)
                    {
                        cappedPayments.Add(new PaymentDetail
                        {
                            PaymentMethod = payment.PaymentMethod,
                            PaymentTerminalId = payment.PaymentTerminalId,
                            ReferenceNumber = payment.ReferenceNumber,
                            Amount = amountToApply // The capped amount!
                        });

                        balanceToPay -= amountToApply;
                    }
                }

                // Per-line tax extraction — must run before Sale.Lines is captured
                if (HasStaffCreditPayment(cappedPayments) && !HasSelectedStoreCreditCustomer())
                    throw new InvalidOperationException("Select a customer before settling a staff credit payment.");

                RefreshLineTaxAmounts();

                var isRecalledBill = CurrentOpenSalesId > 0;
                var salesId = isRecalledBill
                    ? CurrentOpenSalesId
                    : await _salesRepository.HoldSaleAsync(BuildHoldSaleRequest());

                var finalized = await _salesRepository.FinalizeSaleAsync(BuildFinalizeSaleRequest(salesId, cappedPayments));
                if (!finalized)
                    throw new InvalidOperationException("The sale could not be finalized.");

                if (HasAppliedDiscountCode && _appliedDiscountValidation != null && BillDiscount > 0)
                {
                    await _discountRepository.RedeemDiscountForSaleAsync(
                        salesId,
                        _appliedDiscountValidation.DiscountId,
                        AppliedDiscountCode,
                        CurrentUser.BranchId,
                        SubTotal,
                        BillDiscount,
                        CurrentUser.UserId);
                }

                if ((SelectedCustomer?.Id ?? 0) > 0)
                {
                    if (AppliedLoyaltyPoints > 0)
                    {
                        AvailableLoyaltyPoints = await _customerRepository.AdjustLoyaltyPointsAsync(
                            SelectedCustomer.Id,
                            -AppliedLoyaltyPoints,
                            "LOYALTY_REDEEM",
                            salesId,
                            CurrentUser.UserId,
                            $"Redeemed at sale {salesId}");
                    }

                    var earnedPoints = NetAmount <= 0 ? 0 : (int)Math.Floor(NetAmount / LoyaltySpendAmountPerPoint);
                    if (earnedPoints > 0)
                    {
                        AvailableLoyaltyPoints = await _customerRepository.AdjustLoyaltyPointsAsync(
                            SelectedCustomer.Id,
                            earnedPoints,
                            "LOYALTY_EARN",
                            salesId,
                            CurrentUser.UserId,
                            $"Earned from sale {salesId}");
                    }
                }

                if (printBill)
                    PrintFinalizedSale(salesId, isRecalledBill);

                // In-memory removal: drop the just-billed order straight out of the ComboBox's
                // source collection so the cashier sees it disappear immediately, with no extra
                // DB round-trip. SelectedServedOrder is the exact instance WPF selected from
                // ServedOrders, so reference-based Remove finds it directly.
                if (SelectedServedOrder != null)
                    ServedOrders.Remove(SelectedServedOrder);

                ExecuteCancelInvoice();
                RequestBarcodeFocus?.Invoke();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Save Failed: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task PrintPreBillAsync()
        {
            if (!CanPrintPreBill())
                return;

            try
            {
                IsProcessing = true;
                RefreshLineTaxAmounts();

                var salesId = await _salesRepository.HoldSaleAsync(BuildHoldSaleRequest());
                CurrentOpenSalesId = salesId;

                _reportService.PrintSalesInvoice(salesId);

                ExecuteCancelInvoice();
                RequestBarcodeFocus?.Invoke();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Pre-bill Failed: {ex.Message}", "Pre-Bill", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task RecallHeldSaleAsync()
        {
            try
            {
                IsProcessing = true;

                var unpaidSales = await _salesRepository.GetUnpaidSalesAsync(CurrentUser.BranchId);
                if (unpaidSales == null || unpaidSales.Count == 0)
                {
                    MessageBox.Show("No unpaid bills found.", "Recall Bills", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var selectedSale = ShowRecallBillPicker(unpaidSales);

                if (selectedSale == null)
                    return;

                var recalledSale = await _salesRepository.GetSaleForRecallAsync(selectedSale.SalesId);
                if (recalledSale == null)
                {
                    MessageBox.Show("The selected bill is no longer available for recall.", "Recall Bills", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                LoadRecalledSaleIntoCart(recalledSale);
                RequestBarcodeFocus?.Invoke();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Recall Failed: {ex.Message}", "Recall Bills", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private HoldSaleRequestDto BuildHoldSaleRequest()
        {
            return new HoldSaleRequestDto
            {
                BranchId = CurrentUser.BranchId,
                CustomerId = SelectedCustomer?.Id > 0 ? (int?)SelectedCustomer.Id : null,
                TotalAmount = SubTotal,
                Discount = BillDiscount,
                IsTaxInvoice = false,
                TaxInvoiceNumber = null,
                CreatedBy = CurrentUser.UserId,
                OrderId = SelectedServedOrder?.OrderId,
                TaxAmount = TaxAmount,
                ServiceChargeAmount = ServiceChargeAmount,
                ShiftId = CurrentUser.CurrentShiftId,
                Lines = CartItems.Select(line => new SaleLineRequestDto
                {
                    ProductId = line.ProductId,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    DiscountAmount = line.LineDiscount,
                    TaxAmount = line.TaxAmount
                }).ToList()
            };
        }

        private FinalizeSaleRequestDto BuildFinalizeSaleRequest(long salesId, IEnumerable<PaymentDetail> payments)
        {
            return new FinalizeSaleRequestDto
            {
                SalesId = salesId,
                CustomerId = SelectedCustomer?.Id > 0 ? (int?)SelectedCustomer.Id : null,
                CreatedBy = CurrentUser.UserId,
                CashGiven = payments
                    .Where(payment => string.Equals(payment.PaymentMethod, CashPaymentMethod, StringComparison.OrdinalIgnoreCase))
                    .Sum(payment => payment.Amount),
                Payments = payments.Select(payment => new SalePaymentRequestDto
                {
                    PaymentTerminalId = payment.PaymentTerminalId,
                    PaymentMethod = payment.PaymentMethod,
                    Amount = payment.Amount,
                    ReferenceNumber = payment.ReferenceNumber
                }).ToList()
            };
        }

        private void RefreshLineTaxAmounts()
        {
            foreach (var line in CartItems)
            {
                if (!IsTaxEnabled)
                {
                    line.TaxAmount = 0m;
                    continue;
                }

                var applicableTaxes = _taxConfigurations.Where(t => line.TaxIds.Contains(t.Id));
                var unitTax = TaxCalculator.ExtractUnitTax(line.UnitPrice, applicableTaxes);
                line.TaxAmount = Math.Round(unitTax * line.Quantity, 2, MidpointRounding.AwayFromZero);
            }
        }

        private void LoadRecalledSaleIntoCart(RecalledSaleDto sale)
        {
            ExecuteCancelInvoice();

            CurrentOpenSalesId = Convert.ToInt32(sale.SalesId);
            InvoiceNumber = sale.InvoiceNumber;
            SelectedCustomer = sale.CustomerId.HasValue
                ? Customers.FirstOrDefault(customer => customer.Id == sale.CustomerId.Value)
                : Customers.FirstOrDefault(customer => customer.Id == 0);
            IsTaxEnabled = sale.TaxAmount > 0;
            IsServiceChargeEnabled = sale.ServiceChargeAmount > 0;

            var lineNumber = 1;
            foreach (var line in sale.Lines)
            {
                var product = Products.FirstOrDefault(x => x.VariantId == line.ProductId);
                CartItems.Add(new SalesLine
                {
                    Number = lineNumber++,
                    ProductId = line.ProductId,
                    MenuCategoryId = product?.MenuCategoryId,
                    ProductName = product?.DisplayName ?? MenuVariantDto.RemoveStandardVariantSuffix(line.ProductName),
                    UnitPrice = line.UnitPrice,
                    Quantity = line.Quantity,
                    ManualDiscount = line.DiscountAmount,
                    TaxAmount = line.TaxAmount,
                    TaxIds = product?.TaxIds?.ToList() ?? new List<int>(),
                    AvailableQuantity = decimal.MaxValue,
                    Note = string.Empty
                });
            }

            CalculateTotals();
            RaiseSaveCommandState();
        }

        private SalesListDto ShowRecallBillPicker(IList<SalesListDto> unpaidSales)
        {
            var grid = new System.Windows.Controls.DataGrid
            {
                ItemsSource = unpaidSales,
                IsReadOnly = true,
                Margin = new Thickness(18, 14, 18, 0),
                MinHeight = 260,
                MaxHeight = 320,
                HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto
            };
            ApplyStyle(grid, "SalesDataGridStyle");
            grid.SelectedIndex = 0;

            grid.Columns.Add(new System.Windows.Controls.DataGridTextColumn { Header = "Invoice Number", Binding = new System.Windows.Data.Binding("InvoiceNumber"), Width = 170 });
            grid.Columns.Add(new System.Windows.Controls.DataGridTextColumn { Header = "Date", Binding = new System.Windows.Data.Binding("SalesDate") { StringFormat = "dd/MM/yyyy HH:mm" }, Width = 150 });
            grid.Columns.Add(new System.Windows.Controls.DataGridTextColumn { Header = "Customer", Binding = new System.Windows.Data.Binding("CustomerName"), Width = new System.Windows.Controls.DataGridLength(1, System.Windows.Controls.DataGridLengthUnitType.Star) });
            grid.Columns.Add(new System.Windows.Controls.DataGridTextColumn
            {
                Header = "Amount",
                Binding = new System.Windows.Data.Binding("NetAmount") { StringFormat = "Rs. {0:N2}" },
                Width = 130,
                HeaderStyle = TryFindStyle("RightAlignedDataGridColumnHeaderStyle"),
                ElementStyle = TryFindStyle("RightAlignedTextStyle")
            });

            var recallButton = new System.Windows.Controls.Button
            {
                Content = "Recall Bill",
                MinWidth = 120,
                Margin = new Thickness(0, 0, 8, 0)
            };
            ApplyStyle(recallButton, "PlaceOrderButtonStyle");

            var cancelButton = new System.Windows.Controls.Button
            {
                Content = "Cancel",
                MinWidth = 90
            };
            ApplyStyle(cancelButton, "SaveSaleSecondaryButtonStyle");

            var buttons = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(18, 16, 18, 18)
            };
            buttons.Children.Add(recallButton);
            buttons.Children.Add(cancelButton);

            var title = new System.Windows.Controls.TextBlock
            {
                Text = "Recall Bills",
                Margin = new Thickness(18, 16, 18, 0)
            };
            ApplyStyle(title, "OrderDetailsTitleStyle");

            var layout = new System.Windows.Controls.DockPanel
            {
                Background = TryFindBrush("SalesCardBrush") ?? Brushes.White,
                LastChildFill = true
            };
            System.Windows.Controls.DockPanel.SetDock(title, System.Windows.Controls.Dock.Top);
            System.Windows.Controls.DockPanel.SetDock(buttons, System.Windows.Controls.Dock.Bottom);
            layout.Children.Add(title);
            layout.Children.Add(buttons);
            layout.Children.Add(grid);

            var window = new Window
            {
                Title = "Recall Bills",
                Width = 720,
                Height = 455,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive),
                Content = layout,
                Background = TryFindBrush("SalesBackgroundBrush") ?? Brushes.White
            };

            SalesListDto selected = null;
            recallButton.Click += (s, e) =>
            {
                selected = grid.SelectedItem as SalesListDto;
                if (selected == null)
                {
                    MessageBox.Show("Select a bill to recall.", "Recall Bills", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                window.DialogResult = true;
            };
            cancelButton.Click += (s, e) => window.DialogResult = false;
            grid.MouseDoubleClick += (s, e) =>
            {
                selected = grid.SelectedItem as SalesListDto;
                if (selected != null)
                    window.DialogResult = true;
            };

            return window.ShowDialog() == true ? selected : null;
        }

        private static void ApplyStyle(FrameworkElement element, string resourceKey)
        {
            if (element == null || string.IsNullOrWhiteSpace(resourceKey))
                return;

            if (Application.Current.TryFindResource(resourceKey) is Style style)
                element.Style = style;
        }

        private static Style TryFindStyle(string resourceKey)
        {
            return string.IsNullOrWhiteSpace(resourceKey)
                ? null
                : Application.Current.TryFindResource(resourceKey) as Style;
        }

        private static Brush TryFindBrush(string resourceKey)
        {
            return string.IsNullOrWhiteSpace(resourceKey)
                ? null
                : Application.Current.TryFindResource(resourceKey) as Brush;
        }

        private void PrintFinalizedSale(long salesId, bool isRecalledBill)
        {
            try
            {
                if (isRecalledBill)
                    _reportService.PrintSettlementReceipt(salesId);
                else
                    _reportService.PrintSalesInvoice(salesId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Print Failed: {ex.Message}", "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Discount Code
        private DiscountValidationResult _appliedDiscountValidation;

        private async Task ApplyDiscountCodeAsync()
        {
            try
            {
                if (CartItems.Any(x => x.IsImportedOrderLine))
                {
                    MessageBox.Show("This served restaurant order already includes its pricing. Extra promo codes are disabled here.", "Discount", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (SubTotal <= 0)
                {
                    MessageBox.Show("Add items before applying a discount code.", "Discount", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var code = (DiscountCodeInput ?? string.Empty).Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(code))
                {
                    MessageBox.Show("Enter a promo/coupon code.", "Discount", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (HasAppliedDiscountCode)
                {
                    MessageBox.Show("Only one discount can be applied per bill.", "Discount", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (HasAppliedLoyaltyPoints)
                {
                    MessageBox.Show("Loyalty redemption is already applied. Promo/coupon code cannot be combined.", "Discount", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (CartItems.Any(x => x.PromoDiscount > 0) || _autoBillDiscountAmount > 0)
                {
                    MessageBox.Show("Automatic discount/promotion is already applied. Promo/coupon code cannot be combined.", "Discount", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = await _discountRepository.ValidateBillDiscountCodeAsync(code, CurrentUser.BranchId, SubTotal);
                if (!result.IsValid)
                {
                    MessageBox.Show(result.Message, "Discount", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _appliedDiscountValidation = result;
                AppliedDiscountCode = result.Code;
                AppliedDiscountName = result.Name;

                // Prevent stacking with manual bill discount
                LineDiscountPercent = 0;

                CalculateTotals();
                OnPropertyChanged(nameof(HasAppliedDiscountCode));
                MessageBox.Show("Discount code applied successfully.", "Discount", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to apply discount code: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyLoyaltyRedemption()
        {
            if (CartItems.Any(x => x.IsImportedOrderLine))
            {
                MessageBox.Show("This served restaurant order already includes its pricing. Loyalty redemption is disabled here.", "Loyalty", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (SubTotal <= 0)
            {
                MessageBox.Show("Add items before applying loyalty redemption.", "Loyalty", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if ((SelectedCustomer?.Id ?? 0) <= 0)
            {
                MessageBox.Show("Select a customer to redeem loyalty points.", "Loyalty", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (HasAppliedDiscountCode || LineDiscountPercent > 0 || CartItems.Any(x => x.PromoDiscount > 0) || _autoBillDiscountAmount > 0)
            {
                MessageBox.Show("Loyalty redemption cannot be combined with existing discount/promotion.", "Loyalty", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(RedeemLoyaltyPointsInput) || !int.TryParse(RedeemLoyaltyPointsInput.Trim(), out var pointsToRedeem))
            {
                MessageBox.Show("Enter a valid loyalty point amount.", "Loyalty", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (pointsToRedeem <= 0)
            {
                MessageBox.Show("Redeem points must be greater than zero.", "Loyalty", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (pointsToRedeem > AvailableLoyaltyPoints)
            {
                MessageBox.Show("Redeem points exceed available loyalty points.", "Loyalty", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            AppliedLoyaltyPoints = pointsToRedeem;
            RedeemLoyaltyPointsInput = pointsToRedeem.ToString();
            CalculateTotals();
            (RemoveLoyaltyCommand as RelayCommand)?.RaiseCanExecuteChanged();
            MessageBox.Show("Loyalty points applied successfully.", "Loyalty", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void RemoveLoyaltyRedemption(bool silent = false)
        {
            AppliedLoyaltyPoints = 0;
            LoyaltyDiscountAmount = 0;
            RedeemLoyaltyPointsInput = string.Empty;

            if (!silent)
                MessageBox.Show("Applied loyalty redemption removed.", "Loyalty", MessageBoxButton.OK, MessageBoxImage.Information);

            CalculateTotals();
            (RemoveLoyaltyCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private void RemoveAppliedDiscount(bool silent = false)
        {
            _appliedDiscountValidation = null;
            AppliedDiscountCode = string.Empty;
            AppliedDiscountName = string.Empty;
            DiscountCodeInput = string.Empty;
            OnPropertyChanged(nameof(HasAppliedDiscountCode));

            if (!silent)
                MessageBox.Show("Applied discount removed.", "Discount", MessageBoxButton.OK, MessageBoxImage.Information);

            CalculateTotals();
        }

        private decimal CalculateDiscountAmount(decimal subtotal, DiscountValidationResult discount)
        {
            if (discount == null || subtotal <= 0)
                return 0;

            decimal amount;
            if (string.Equals(discount.DiscountType, "PERCENT", StringComparison.OrdinalIgnoreCase))
            {
                amount = (subtotal * discount.DiscountValue) / 100m;
            }
            else
            {
                amount = discount.DiscountValue;
            }

            if (discount.MaximumDiscountAmount.HasValue && amount > discount.MaximumDiscountAmount.Value)
                amount = discount.MaximumDiscountAmount.Value;

            if (amount > subtotal)
                amount = subtotal;

            if (amount < 0)
                amount = 0;

            return decimal.Round(amount, 2);
        }

        private decimal CalculateDiscountAmount(decimal baseAmount, DiscountDefinition discount)
        {
            if (discount == null || baseAmount <= 0)
                return 0;

            decimal amount;
            if (string.Equals(discount.DiscountType, "PERCENT", StringComparison.OrdinalIgnoreCase))
            {
                amount = (baseAmount * discount.DiscountValue) / 100m;
            }
            else
            {
                amount = discount.DiscountValue;
            }

            if (discount.MaximumDiscountAmount.HasValue && amount > discount.MaximumDiscountAmount.Value)
                amount = discount.MaximumDiscountAmount.Value;

            if (amount > baseAmount)
                amount = baseAmount;

            if (amount < 0)
                amount = 0;

            return decimal.Round(amount, 2);
        }

        private string GetActiveAppliedDiscountCode()
        {
            if (!string.IsNullOrWhiteSpace(AppliedDiscountCode))
                return AppliedDiscountCode;

            return _appliedAutoBillDiscountName;
        }

        private void NotifyAppliedDiscountProperties()
        {
            OnPropertyChanged(nameof(AppliedDiscountLabel));
            OnPropertyChanged(nameof(AppliedDiscountAmount));
            OnPropertyChanged(nameof(AppliedDiscountPercent));
            OnPropertyChanged(nameof(HasAppliedDiscount));
        }

        private static decimal CalculateDisplayDiscountPercent(decimal baseAmount, string discountType, decimal discountValue, decimal discountAmount)
        {
            if (string.Equals(discountType, "PERCENT", StringComparison.OrdinalIgnoreCase))
                return decimal.Round(discountValue, 2);

            if (baseAmount <= 0 || discountAmount <= 0)
                return 0;

            return decimal.Round((discountAmount / baseAmount) * 100m, 2);
        }

        private static string GetDiscountDisplayCode(DiscountDefinition discount)
        {
            if (discount == null)
                return string.Empty;

            return string.IsNullOrWhiteSpace(discount.Code)
                ? discount.Name
                : discount.Code;
        }

        private decimal CalculateAutoDiscountRuleSubTotal()
        {
            if (CartItems == null || !CartItems.Any())
                return 0m;

            var subTotal = CartItems
                .Where(x => !x.IsAutoGeneratedPromotionLine)
                .Sum(x =>
                {
                    var lineAmountBeforeAutoDiscount = (x.UnitPrice * x.Quantity) - x.BaseDiscountAmount - x.ManualDiscount;
                    return Math.Max(0m, lineAmountBeforeAutoDiscount);
                });

            return NormalizeMoney(subTotal);
        }

        private void QueueActiveAutoDiscountRulesRefresh(decimal subTotal)
        {
            var normalizedSubTotal = NormalizeMoney(subTotal);
            if (_activeAutoDiscountRulesSubTotal.HasValue &&
                _activeAutoDiscountRulesSubTotal.Value == normalizedSubTotal)
                return;

            _pendingAutoDiscountRulesSubTotal = normalizedSubTotal;
            if (_isRefreshingAutoDiscountRules)
                return;

            _autoDiscountRefreshDebounceTimer?.Stop();
            _autoDiscountRefreshDebounceTimer?.Start();
        }

        private async Task RefreshPendingActiveAutoDiscountRulesAsync()
        {
            if (_isRefreshingAutoDiscountRules)
                return;

            _isRefreshingAutoDiscountRules = true;
            var shouldRecalculate = false;

            try
            {
                while (_pendingAutoDiscountRulesSubTotal.HasValue)
                {
                    var subTotal = _pendingAutoDiscountRulesSubTotal.Value;
                    _pendingAutoDiscountRulesSubTotal = null;

                    if (_activeAutoDiscountRulesSubTotal.HasValue &&
                        _activeAutoDiscountRulesSubTotal.Value == subTotal)
                        continue;

                    await LoadActiveAutoDiscountRulesAsync(subTotal);
                    shouldRecalculate = true;
                }

                if (shouldRecalculate)
                {
                    RefreshProductOfferHints();
                    CalculateTotals();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to refresh automatic discounts: {ex.Message}";
            }
            finally
            {
                _isRefreshingAutoDiscountRules = false;
            }

            if (_pendingAutoDiscountRulesSubTotal.HasValue)
                _ = RefreshPendingActiveAutoDiscountRulesAsync();
        }

        private async Task LoadActiveAutoDiscountRulesAsync(decimal subTotal)
        {
            var normalizedSubTotal = NormalizeMoney(subTotal);
            _activeAutoDiscountRules = (await _discountRepository
                .GetActiveAutoDiscountsAsync(_userSessionService.BranchId, normalizedSubTotal))
                .ToList();
            _activeAutoDiscountRulesSubTotal = normalizedSubTotal;
        }

        private static decimal NormalizeMoney(decimal amount)
        {
            return decimal.Round(Math.Max(0m, amount), 2);
        }

        private async Task LoadActivePricingRulesAsync()
        {
            _activePromotionRules = (await _promotionRepository.GetActiveAsync(_userSessionService.BranchId, DateTime.Now)).ToList();
            await LoadActiveAutoDiscountRulesAsync(CalculateAutoDiscountRuleSubTotal());
            RefreshProductOfferHints();
        }

        private void RefreshProductOfferHints()
        {
            if (Products == null || Products.Count == 0)
                return;

            var now = DateTime.Now;
            // GroupBy + First guards against duplicate VariantIds (e.g. a variant returned more
            // than once by a join) instead of throwing "An item with the same key has already been added".
            var productLookup = Products
                .GroupBy(x => x.VariantId)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var product in Products)
            {
                var hints = new List<string>();

                if ((product.DiscountAmount ?? 0) > 0)
                    hints.Add($"Item off Rs. {(product.DiscountAmount ?? 0):N2}");

                foreach (var rule in _activePromotionRules)
                {
                    if (!IsRuleValidForNow(rule, now))
                        continue;

                    var hint = BuildProductPromotionHint(product, rule, productLookup);
                    if (!string.IsNullOrWhiteSpace(hint))
                        hints.Add(hint);
                }

                foreach (var discount in _activeAutoDiscountRules)
                {
                    if (!string.Equals(discount.ApplyScope, "CATEGORY", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!discount.TargetMenuCategoryId.HasValue || discount.TargetMenuCategoryId.Value != product.MenuCategoryId)
                        continue;

                    hints.Add(BuildProductDiscountHint(discount));
                }

                product.OfferSummary = string.Join(
                    " | ",
                    hints
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase));
            }

            OnPropertyChanged(nameof(SelectedProduct));
        }

        private string BuildProductPromotionHint(
            MenuVariantDto product,
            PromotionRule rule,
            IDictionary<int, MenuVariantDto> productLookup)
        {
            if (product == null || rule == null)
                return string.Empty;

            var type = (rule.PromotionType ?? string.Empty).Trim().ToUpperInvariant();
            int buyQty = rule.BuyQuantity <= 0 ? 1 : rule.BuyQuantity;
            int getQty = rule.GetQuantity <= 0 ? 1 : rule.GetQuantity;

            if ((type == "BOGO_SAME_FREE" || type == "BOGO_SAME_PERCENT") && product.VariantId == rule.BuyProductId)
            {
                if (type == "BOGO_SAME_FREE")
                    return $"Buy {buyQty}, get {getQty} free";

                return $"Buy {buyQty}, get {getQty} at {rule.DiscountPercent:N0}% off";
            }

            if (type == "BUY_A_GET_B_FREE")
            {
                if (product.VariantId == rule.BuyProductId)
                {
                    if (!rule.GetProductId.HasValue || !productLookup.TryGetValue(rule.GetProductId.Value, out var getProduct))
                        return string.Empty;

                    if (rule.DiscountPercent <= 0 || rule.DiscountPercent >= 100)
                        return $"Buy {buyQty}, get {getProduct.DisplayName} x{getQty} free";

                    return $"Buy {buyQty}, get {getProduct.DisplayName} x{getQty} at {rule.DiscountPercent:N0}% off";
                }

                if (rule.GetProductId.HasValue && product.VariantId == rule.GetProductId.Value)
                {
                    if (!productLookup.TryGetValue(rule.BuyProductId, out var buyProduct))
                        return string.Empty;

                    if (rule.DiscountPercent <= 0 || rule.DiscountPercent >= 100)
                        return $"Free with {buyProduct.DisplayName}";

                    return $"{rule.DiscountPercent:N0}% off with {buyProduct.DisplayName}";
                }
            }

            return string.Empty;
        }

        private string BuildProductDiscountHint(DiscountDefinition discount)
        {
            if (discount == null)
                return string.Empty;

            if (string.Equals(discount.DiscountType, "PERCENT", StringComparison.OrdinalIgnoreCase))
                return $"{discount.Name} ({discount.DiscountValue:N0}% off)";

            return $"{discount.Name} (Rs. {discount.DiscountValue:N2} off)";
        }

        private void RenumberCartItems()
        {
            int number = 1;
            foreach (var line in CartItems)
            {
                line.Number = number++;
            }
        }

        private void RemoveGeneratedPromotionLines()
        {
            var generatedLines = CartItems
                .Where(x => x.IsAutoGeneratedPromotionLine)
                .ToList();

            foreach (var line in generatedLines)
            {
                CartItems.Remove(line);
            }

            if (generatedLines.Any())
                RenumberCartItems();
        }

        private void ResetAutoPricingState()
        {
            foreach (var line in CartItems)
            {
                line.SetPromoDiscount(0);
                line.ClearAutoRuleName();
            }
        }

        private SalesLine AddGeneratedPromotionLine(
            int productId,
            int? menuCategoryId,
            string productName,
            decimal unitPrice,
            decimal quantity,
            decimal percent,
            string ruleName,
            List<int> taxIds = null)
        {
            if (quantity <= 0 || unitPrice < 0 || percent <= 0)
                return null;

            var line = new SalesLine
            {
                Number = CartItems.Any() ? CartItems.Max(x => x.Number) + 1 : 1,
                ProductId = productId,
                MenuCategoryId = menuCategoryId,
                ProductName = productName,
                Note = string.Empty,
                UnitPrice = unitPrice,
                AvailableQuantity = decimal.MaxValue,
                Quantity = quantity,
                IsAutoGeneratedPromotionLine = true,
                AutoRuleName = null,
                TaxIds = taxIds?.ToList() ?? new List<int>()
            };

            var promoDiscount = decimal.Round(unitPrice * quantity * (percent / 100m), 2);
            line.SetPromoDiscount(promoDiscount);
            line.AddAutoRuleName(ruleName);
            CartItems.Add(line);
            return line;
        }

        private void ApplyDistributedDiscount(List<SalesLine> targetLines, decimal totalDiscount, string ruleName)
        {
            if (targetLines == null || !targetLines.Any() || totalDiscount <= 0)
                return;

            var eligibleLines = targetLines
                .Select(x => new
                {
                    Line = x,
                    Base = Math.Max(0, x.Amount)
                })
                .Where(x => x.Base > 0)
                .ToList();

            if (!eligibleLines.Any())
                return;

            var eligibleBase = eligibleLines.Sum(x => x.Base);
            if (eligibleBase <= 0)
                return;

            decimal distributed = 0;
            for (int i = 0; i < eligibleLines.Count; i++)
            {
                var item = eligibleLines[i];
                decimal lineDiscount;

                if (i == eligibleLines.Count - 1)
                {
                    lineDiscount = totalDiscount - distributed;
                }
                else
                {
                    lineDiscount = decimal.Round(totalDiscount * (item.Base / eligibleBase), 2);
                    distributed += lineDiscount;
                }

                if (lineDiscount <= 0)
                    continue;

                item.Line.AddPromoDiscount(lineDiscount);
                item.Line.AddAutoRuleName(ruleName);
            }
        }

        private void ApplyAutoPromotions()
        {
            _autoBillDiscountAmount = 0;
            _appliedAutoBillDiscountRule = null;
            _appliedAutoBillDiscountName = string.Empty;
            AppliedAutoDiscountName = string.Empty;

            if (!CartItems.Any())
                return;

            RemoveGeneratedPromotionLines();
            ResetAutoPricingState();

            if (CartItems.Any(x => x.IsImportedOrderLine))
                return;

            if (HasAppliedDiscountCode || HasAppliedLoyaltyPoints || LineDiscountPercent > 0)
                return;

            var now = DateTime.Now;
            var appliedAutoRuleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var requestedLines = CartItems
                .Where(x => !x.IsAutoGeneratedPromotionLine && !x.IsImportedOrderLine)
                .ToList();

            foreach (var rule in _activePromotionRules)
            {
                if (!IsRuleValidForNow(rule, now))
                    continue;

                var promotionType = (rule.PromotionType ?? string.Empty).Trim().ToUpperInvariant();
                if (promotionType == "BOGO_SAME_FREE" || promotionType == "BOGO_SAME_PERCENT")
                {
                    var line = requestedLines.FirstOrDefault(x => x.ProductId == rule.BuyProductId);
                    if (line == null || line.Quantity <= 0 || line.UnitPrice <= 0)
                        continue;

                    int buyQty = rule.BuyQuantity <= 0 ? 1 : rule.BuyQuantity;
                    int getQty = rule.GetQuantity <= 0 ? 1 : rule.GetQuantity;
                    int qty = (int)Math.Floor(line.Quantity);
                    int eligibleGroups = qty / buyQty;
                    int generatedQty = eligibleGroups * getQty;
                    if (generatedQty <= 0)
                        continue;

                    var percent = promotionType == "BOGO_SAME_FREE"
                        ? 100m
                        : rule.DiscountPercent;
                    if (percent <= 0)
                        continue;

                    AddGeneratedPromotionLine(
                        line.ProductId,
                        line.MenuCategoryId,
                        line.ProductName,
                        line.UnitPrice,
                        generatedQty,
                        percent,
                        rule.RuleName,
                        line.TaxIds);

                    appliedAutoRuleNames.Add(rule.RuleName);
                }
                else if (promotionType == "BUY_A_GET_B_FREE")
                {
                    var buyLine = requestedLines.FirstOrDefault(x => x.ProductId == rule.BuyProductId);
                    if (buyLine == null || buyLine.Quantity <= 0)
                        continue;

                    var getProductId = rule.GetProductId ?? 0;
                    if (getProductId <= 0)
                        continue;

                    var getLine = requestedLines.FirstOrDefault(x => x.ProductId == getProductId);
                    int buyQty = rule.BuyQuantity <= 0 ? 1 : rule.BuyQuantity;
                    int getQty = rule.GetQuantity <= 0 ? 1 : rule.GetQuantity;
                    int buyBlocks = (int)Math.Floor(buyLine.Quantity) / buyQty;
                    int eligibleGetQty = buyBlocks * getQty;
                    var percent = rule.DiscountPercent <= 0 ? 100m : rule.DiscountPercent;
                    if (eligibleGetQty <= 0 || percent <= 0)
                        continue;

                    int actualGetQty = 0;
                    if (getLine != null && getLine.Quantity > 0 && getLine.UnitPrice > 0)
                    {
                        actualGetQty = Math.Min((int)Math.Floor(getLine.Quantity), eligibleGetQty);
                        if (actualGetQty > 0)
                        {
                            var promo = decimal.Round(
                                getLine.UnitPrice * actualGetQty * (percent / 100m),
                                2);
                            getLine.AddPromoDiscount(promo);
                            getLine.AddAutoRuleName(rule.RuleName);
                        }
                    }

                    int shortfallQty = eligibleGetQty - actualGetQty;
                    if (shortfallQty > 0)
                    {
                        var variant = Products.FirstOrDefault(x => x.VariantId == getProductId);
                        if (variant == null)
                            continue;

                        AddGeneratedPromotionLine(
                            variant.VariantId,
                            variant.MenuCategoryId,
                            variant.DisplayName,
                            variant.DefaultPrice,
                            shortfallQty,
                            percent,
                            rule.RuleName,
                            variant.TaxIds);
                    }

                    appliedAutoRuleNames.Add(rule.RuleName);
                }
            }

            foreach (var discount in _activeAutoDiscountRules)
            {
                if (string.Equals(discount.ApplyScope, "CATEGORY", StringComparison.OrdinalIgnoreCase))
                {
                    if (!discount.TargetMenuCategoryId.HasValue)
                        continue;

                    var targetLines = CartItems
                        .Where(x => x.MenuCategoryId.HasValue && x.MenuCategoryId.Value == discount.TargetMenuCategoryId.Value)
                        .ToList();

                    if (!targetLines.Any())
                        continue;

                    var eligibleBase = targetLines.Sum(x => Math.Max(0, x.Amount));
                    if (eligibleBase <= 0)
                        continue;

                    var categoryDiscount = CalculateDiscountAmount(eligibleBase, discount);
                    if (categoryDiscount <= 0)
                        continue;

                    ApplyDistributedDiscount(targetLines, categoryDiscount, discount.Name);
                    appliedAutoRuleNames.Add(discount.Name);
                }
            }

            var discountedSubTotal = CartItems.Sum(x => x.Amount);
            var bestAutoBillRule = _activeAutoDiscountRules
                .Where(x => string.Equals(x.ApplyScope, "BILL", StringComparison.OrdinalIgnoreCase))
                .Select(x => new { Rule = x, Amount = CalculateDiscountAmount(discountedSubTotal, x) })
                .OrderByDescending(x => x.Amount)
                .FirstOrDefault();

            if (bestAutoBillRule != null && bestAutoBillRule.Amount > 0)
            {
                _autoBillDiscountAmount = bestAutoBillRule.Amount;
                _appliedAutoBillDiscountRule = bestAutoBillRule.Rule;
                _appliedAutoBillDiscountName = GetDiscountDisplayCode(bestAutoBillRule.Rule);
                appliedAutoRuleNames.Add(bestAutoBillRule.Rule.Name);
            }

            AppliedAutoDiscountName = appliedAutoRuleNames.Any()
                ? string.Join(", ", appliedAutoRuleNames.OrderBy(x => x))
                : string.Empty;
        }

        private static bool IsTimeWindowMatch(TimeSpan current, TimeSpan? start, TimeSpan? end)
        {
            if (!start.HasValue && !end.HasValue)
                return true;

            if (start.HasValue && !end.HasValue)
                return current >= start.Value;

            if (!start.HasValue && end.HasValue)
                return current <= end.Value;

            if (start.Value <= end.Value)
                return current >= start.Value && current <= end.Value;

            return current >= start.Value || current <= end.Value;
        }
        private static bool IsRuleValidForNow(PromotionRule rule, DateTime now)
        {
            if (!rule.IsActive)
                return false;

            if (rule.ValidFrom.HasValue && now < rule.ValidFrom.Value)
                return false;

            if (rule.ValidTo.HasValue && now > rule.ValidTo.Value)
                return false;

            if (rule.DayOfWeekMask.HasValue && rule.DayOfWeekMask.Value > 0)
            {
                int todayMask = ToDayMask(now.DayOfWeek);
                if ((rule.DayOfWeekMask.Value & todayMask) == 0)
                    return false;
            }

            return true;
        }

        private static int ToDayMask(DayOfWeek day)
        {
            switch (day)
            {
                case DayOfWeek.Sunday: return 1;
                case DayOfWeek.Monday: return 2;
                case DayOfWeek.Tuesday: return 4;
                case DayOfWeek.Wednesday: return 8;
                case DayOfWeek.Thursday: return 16;
                case DayOfWeek.Friday: return 32;
                case DayOfWeek.Saturday: return 64;
                default: return 0;
            }
        }
        #endregion

        #region Helper Methods
        private async Task LoadSelectedCustomerLoyaltyAsync()
        {
            try
            {
                if ((SelectedCustomer?.Id ?? 0) <= 0)
                {
                    AvailableLoyaltyPoints = 0;
                    RemoveLoyaltyRedemption(true);
                    return;
                }

                AvailableLoyaltyPoints = await _customerRepository.GetLoyaltyPointsAsync(SelectedCustomer.Id);

                if (AppliedLoyaltyPoints > AvailableLoyaltyPoints)
                    RemoveLoyaltyRedemption(true);
            }
            catch (Exception ex)
            {
                AvailableLoyaltyPoints = 0;
                MessageBox.Show($"Failed to load loyalty points: {ex.Message}", "Loyalty", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Logout()
        {
            try
            {
                // Show confirmation dialog
                var result = MessageBox.Show(
                    "Are you sure you want to logout?",
                    "Confirm Logout",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Clear the user session
                    _userSessionService.ClearSession();

                    // Create and show Login window
                    var loginWindow = _serviceProvider.GetRequiredService<LoginView>();
                    loginWindow.DataContext = _serviceProvider.GetRequiredService<LoginViewModel>();
                    loginWindow.Show();

                    // Close the current Main window
                    Application.Current.Windows.OfType<SalesView>().FirstOrDefault()?.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred during logout: {ex.Message}",
                              "Logout Error",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
            }
        }

        private void OpenAdminDashboard()
        {
            try
            {
                var nextWindow = _serviceProvider.GetRequiredService<MainView>();
                nextWindow.DataContext = _serviceProvider.GetRequiredService<MainViewModel>();

                Application.Current.MainWindow = nextWindow;
                nextWindow.Show();

                Application.Current.Windows.OfType<SalesView>().FirstOrDefault()?.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to open Admin Dashboard: {ex.Message}",
                              "Error",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
            }
        }
        private async Task ExecuteAddCustomerCommand()
        {
            var customerViewModel = new CustomerViewModel(_customerRepository, _userSessionService);
            var customerView = new CustomerWindow();

            customerView.DataContext = customerViewModel;

            var result = customerView.ShowDialog();

            if (result == true)
            {

                await LoadCustomers();


                var newCustomer = await _customerRepository.GetByIdAsync(customerViewModel.CustomerId);
                if (newCustomer != null)
                {
                    SelectedCustomer = newCustomer;
                }
            }
        }
        private void ExecuteOpenRoomBooking()
        {
            try
            {
                var vm = _serviceProvider.GetRequiredService<RoomBookingViewModel>();
                var view = new RoomBookingView { DataContext = vm };

                view.Margin = new Thickness(15);

                var window = new Window
                {
                    Title = "Room Booking",
                    Content = view,

                    Width = 1230,
                    Height = 690,

                    ResizeMode = ResizeMode.NoResize,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive),

                    Background = (Brush)new BrushConverter().ConvertFrom("#F3F4F6")
                };
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open Room Booking: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void ExecuteOpenCashInOut(object obj)
        {
            try
            {
                IsOverlayVisible = true;

                // The DialogService handles DI resolution, Window creation, and Owner setting automatically!
                _dialogService.ShowDialog<CashInOutViewModel>(out var viewModel);

                // Note: If you need to do something AFTER the window closes (like refresh a report),
                // you can do it here. The code pauses here until the dialog closes.
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening Cash Window: {ex.Message}");
            }
            finally
            {
                IsOverlayVisible = false;
            }
        }

        private void ExecuteOpenSalesReturn(object obj)
        {
            try
            {
                IsOverlayVisible = true;

                // Returns true only if a return was actually processed inside the modal
                // (SalesReturnViewModel.ReturnProcessed -> SalesReturnWindow sets DialogResult = true).
                var dialogResult = _dialogService.ShowDialog<SalesReturnViewModel>(out var salesReturnViewModel);

                if (dialogResult == true)
                {
                    // No cash-drawer/shift summary is currently displayed on this screen;
                    // add a refresh call here if one is added later.
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening Sales Return: {ex.Message}", "Sales Return", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsOverlayVisible = false;
            }
        }

        private async Task ExecuteCloseShiftAsync()
        {
            try
            {
                if (!CurrentUser.CurrentShiftId.HasValue)
                {
                    MessageBox.Show("No open shift was found for the current session.", "Close Shift", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (CartItems.Any())
                {
                    MessageBox.Show("Complete or cancel the current invoice before closing the shift.", "Close Shift", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                IsOverlayVisible = true;

                var dialogResult = _dialogService.ShowDialog<CloseShiftDialogViewModel>(out var closeShiftDialogViewModel);
                if (dialogResult != true || closeShiftDialogViewModel == null)
                {
                    return;
                }

                var reconciliation = await _shiftRepository.CloseShiftAsync(
                    CurrentUser.CurrentShiftId.Value,
                    CurrentUser.UserId,
                    closeShiftDialogViewModel.PhysicalCash);

                MessageBox.Show(
                    FormatCloseShiftSuccessMessage(reconciliation),
                    "Close Shift",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                ClosePosAfterShiftClosed();
            }
            catch (SqlException ex) when (ex.Number == 50000)
            {
                MessageBox.Show(ex.Message, "Close Shift", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (SqlException ex)
            {
                MessageBox.Show($"Failed to close shift: {ex.Message}", "Close Shift", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to close shift: {ex.Message}", "Close Shift", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsOverlayVisible = false;
            }
        }

        private string FormatCloseShiftSuccessMessage(ShiftReconciliationDto reconciliation)
        {
            if (reconciliation == null)
            {
                return "Shift closed successfully.";
            }

            return string.Format(
                "Shift closed successfully.{0}{0}System Cash: Rs. {1:N2}{0}Physical Cash: Rs. {2:N2}{0}Variance: Rs. {3:N2}",
                Environment.NewLine,
                reconciliation.SystemCash,
                reconciliation.PhysicalCash,
                reconciliation.Variance);
        }

        private void ClosePosAfterShiftClosed()
        {
            _userSessionService.ClearSession();

            var loginWindow = _serviceProvider.GetRequiredService<LoginView>();
            loginWindow.DataContext = _serviceProvider.GetRequiredService<LoginViewModel>();
            Application.Current.MainWindow = loginWindow;
            loginWindow.Show();

            Application.Current.Windows.OfType<SalesView>().FirstOrDefault()?.Close();
        }

        private void GenerateInvoiceNumber()
        {
            InvoiceNumber = $"AUTO-{DateTime.Now:yyyyMMddHHmmss}";
        }

        private bool FilterCustomer(object item)
        {
            if (!(item is Customer customer))
                return false;

            var query = (CustomerSearchText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(query))
                return true;

            return ContainsCustomerText(customer.CustomerName, query) ||
                   ContainsCustomerText(customer.ContactNumber, query);
        }

        private static bool ContainsCustomerText(string value, string query)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public async Task LoadCustomers()
        {
            var customerEnumerable = await _customerRepository.GetAllAsync();
            var customerList = customerEnumerable.ToList();

            var placeholder = new Customer { Id = 0, CustomerName = "-- Select Customer --", IsDefault = true, LoyaltyPoints = 0 };
            customerList.Insert(0, placeholder);

            if (Customers == null)
            {
                Customers = new ObservableCollection<Customer>();
            }
            else
            {
                Customers.Clear();
            }

            foreach (var customer in customerList)
            {
                Customers.Add(customer);
            }

            FilteredCustomers.Refresh();
        }
        #endregion

        #region Validation
        private void ValidatePayment()
        {
            if (!IsPaymentPanelVisible)
            {
                PaymentErrorMessage = string.Empty;
            }
            else if (CurrentPaymentAmount <= 0)
            {
                PaymentErrorMessage = string.Empty;
            }
            else if (!IsPaymentTypeCash && CurrentPaymentAmount > RemainingBalance)
            {
                PaymentErrorMessage = "Amount cannot exceed remaining balance.";
            }
            else if (IsPaymentTypeCard && SelectedPaymentTerminal == null)
            {
                PaymentErrorMessage = "Select a payment terminal.";
            }
            else if (IsPaymentTypeCard && string.IsNullOrWhiteSpace(ReferenceNumber))
            {
                PaymentErrorMessage = "Enter the 4 digit reference or auth code.";
            }
            else if (IsPaymentTypeCard && !HasFourDigitReference(ReferenceNumber))
            {
                PaymentErrorMessage = "Reference number must be exactly 4 digits.";
            }
            else if (IsPaymentTypeCredit && !HasSelectedStoreCreditCustomer())
            {
                PaymentErrorMessage = "Select a customer for staff credit.";
            }
            else if (IsPaymentTypeCredit && string.IsNullOrWhiteSpace(ReferenceNumber))
            {
                PaymentErrorMessage = "Enter the staff credit reference.";
            }
            else if (IsPaymentTypeBankTransfer && string.IsNullOrWhiteSpace(ReferenceNumber))
            {
                PaymentErrorMessage = "Enter the transaction reference or slip number.";
            }
            else
            {
                PaymentErrorMessage = string.Empty;
            }

            OnPropertyChanged(nameof(IsPaymentValid));
            RaiseSaveCommandState();
        }
        #endregion

        #region Helpers
        private void SelectPaymentMethod(SalesPaymentMethod method)
        {
            var paymentMethod = ToPaymentMethodCode(method);

            IsPaymentInputVisible = true;
            SelectedPaymentMethod = paymentMethod;
        }

        private static string ToPaymentMethodCode(SalesPaymentMethod method)
        {
            switch (method)
            {
                case SalesPaymentMethod.CASH:
                    return CashPaymentMethod;
                case SalesPaymentMethod.CARD:
                    return CardPaymentMethod;
                case SalesPaymentMethod.CREDIT:
                    return CreditPaymentMethod;
                case SalesPaymentMethod.BANK_TRANSFER:
                    return BankTransferPaymentMethod;
                default:
                    return method.ToString();
            }
        }

        private static string NormalizePaymentMethod(string paymentMethod)
        {
            if (string.IsNullOrWhiteSpace(paymentMethod))
                return null;

            var normalized = paymentMethod.Trim().ToUpperInvariant().Replace(" ", "_");

            switch (normalized)
            {
                case "CASH":
                    return CashPaymentMethod;
                case "CARD":
                case "CREDIT_CARD":
                    return CardPaymentMethod;
                case "CREDIT":
                case "STORE_CREDIT":
                    return CreditPaymentMethod;
                case "BANK_TRANSFER":
                case "BANKTRANSFER":
                case "BANK":
                    return BankTransferPaymentMethod;
                default:
                    return normalized;
            }
        }

        private bool HasSelectedStoreCreditCustomer()
        {
            return SelectedCustomer != null && SelectedCustomer.Id > 0;
        }

        private static bool HasStaffCreditPayment(IEnumerable<PaymentDetail> payments)
        {
            return payments != null && payments.Any(payment =>
                string.Equals(payment.PaymentMethod, CreditPaymentMethod, StringComparison.OrdinalIgnoreCase));
        }

        private bool RequiresReferenceNumber()
        {
            return IsPaymentTypeCard || IsPaymentTypeCredit || IsPaymentTypeBankTransfer;
        }

        private static string KeepDigitsOnly(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            return new string(value.Where(IsAsciiDigit).ToArray());
        }

        private static bool HasDigitsOnlyReference(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.All(IsAsciiDigit);
        }

        private static bool HasFourDigitReference(string value)
        {
            return HasDigitsOnlyReference(value) && value.Length == 4;
        }

        private static bool IsAsciiDigit(char value)
        {
            return value >= '0' && value <= '9';
        }

        private void RaiseSaveCommandState()
        {
            (AddPaymentCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SaveSaleCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (SaveAndPrintCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (PrintPreBillCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }

        private string GetPaymentTypeDisplayText()
        {
            switch (SelectedPaymentMethod)
            {
                case CashPaymentMethod:
                    return "CASH";
                case CardPaymentMethod:
                    return "CARD PAYMENT";
                case CreditPaymentMethod:
                    return "STORE CREDIT";
                case BankTransferPaymentMethod:
                    return "BANK TRANSFER";
                default:
                    return string.Empty;
            }
        }
        #endregion

        #region Events
        public event Action RequestCashFocus;
        public event Action RequestBillDiscountFocus;
        public event Action RequestBarcodeFocus;
        #endregion

    }
}






























