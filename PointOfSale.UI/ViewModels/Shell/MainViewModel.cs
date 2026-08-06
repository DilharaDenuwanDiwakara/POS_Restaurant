using System;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Sales;
using PointOfSale.Core.Interfaces.Services;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;
using PointOfSale.UI.ViewModels.Accounts;
using PointOfSale.UI.ViewModels.Inventory;
using PointOfSale.UI.ViewModels.Purchasing;
using PointOfSale.UI.ViewModels.Restaurant;
using PointOfSale.UI.ViewModels.Sales;
using PointOfSale.UI.ViewModels.Security;
using PointOfSale.UI.ViewModels.Settings;
using PointOfSale.UI.Views.Sales;
using PointOfSale.UI.Views.Security;
using PointOfSale.UI.Views.Shell;

namespace PointOfSale.UI.ViewModels.Shell
{
    public class MainViewModel : BaseViewModel
    {
        #region Fields
        private readonly IServiceProvider _serviceProvider;
        private readonly IUserSessionService _userSessionService;
        private readonly IDialogService _dialogService;
        private readonly IShiftRepository _shiftRepository;
        private readonly DispatcherTimer _clockTimer;
        #endregion

        public MainViewModel(IServiceProvider serviceProvider, IUserSessionService sessionService,
            IDialogService dialogService,
            IShiftRepository shiftRepository)
        {
            _serviceProvider = serviceProvider;
            _userSessionService = sessionService;
            _dialogService = dialogService;
            _shiftRepository = shiftRepository;

            CurrentDateTime = DateTime.Now;
            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (s, e) => CurrentDateTime = DateTime.Now;
            _clockTimer.Start();

            InitializeCommands();

            CurrentViewModel = _serviceProvider.GetRequiredService<DashboardViewModel>();
        }

        #region Properties

        private string _currentPageTitle = "Dashboard"; // Default value
        public string CurrentPageTitle
        {
            get => _currentPageTitle;
            set
            {
                _currentPageTitle = value;
                OnPropertyChanged(nameof(CurrentPageTitle)); // Or SetProperty()
            }
        }

        public bool CanAccessDashboard => _userSessionService.HasPermission("ACCESS_DASHBOARD");

        // --- RESTAURANT ---
        public bool CanAccessRestaurant => _userSessionService.HasPermission("ACCESS_RESTAURANT");
        public bool CanNavMenuItemList => _userSessionService.HasPermission("NAV_MENU_ITEM_LIST");
        public bool CanNavMenuItem => _userSessionService.HasPermission("NAV_MENU_ITEM");
        public bool CanNavMenuCategory => _userSessionService.HasPermission("NAV_MENU_CATEGORY");
        public bool CanNavTable => _userSessionService.HasPermission("NAV_TABLE");
        public bool CanNavMenuProfitability => _userSessionService.HasPermission("NAV_MENU_PROFITABILITY");
        public bool CanNavRoomRegister => _userSessionService.HasPermission("NAV_ROOM_REGISTER") || CanNavTable;
        public bool CanNavRoomBooking => _userSessionService.HasPermission("NAV_ROOM_BOOKING") || CanNavTable;

        // --- SALES ---
        public bool CanAccessSales => _userSessionService.HasPermission("ACCESS_SALES");
        public bool CanNavInvoices => _userSessionService.HasPermission("NAV_INVOICE");
        public bool CanNavShiftManagement => _userSessionService.HasPermission("NAV_SHIFT_MANAGEMENT") || CanNavInvoices;
        public bool CanNavDiscount => _userSessionService.HasPermission("NAV_DISCOUNT") || CanNavInvoices;
        public bool CanNavPromotion => _userSessionService.HasPermission("NAV_PROMOTION") || CanNavInvoices;
        public bool CanNavSalesReturn => _userSessionService.HasPermission("NAV_SALES_RETURN");
        public bool CanNavCustomer => _userSessionService.HasPermission("NAV_CUSTOMER");


        // --- INVENTORY ---
        public bool CanAccessInventory => _userSessionService.HasPermission("ACCESS_INVENTORY");
        public bool CanNavProductList => _userSessionService.HasPermission("NAV_PRODUCT_LIST");
        public bool CanNavStockTransfer => _userSessionService.HasPermission("NAV_STOCK_TRANSFER");
        public bool CanNavWastage => _userSessionService.HasPermission("NAV_WASTAGE");
        public bool CanNavAdjustment => _userSessionService.HasPermission("NAV_STOCK_ADJUSTMENT");
        //public bool CanNavBarcodePrint => _userSessionService.HasPermission("NAV_BARCODE_PRINT");
        public bool CanNavProduct => _userSessionService.HasPermission("NAV_PRODUCT");
        public bool CanNavCategory => _userSessionService.HasPermission("NAV_CATEGORY");


        // --- PURCHASING ---
        public bool CanAccessPurchasing => _userSessionService.HasPermission("ACCESS_PURCHASING");
        public bool CanNavPO => _userSessionService.HasPermission("NAV_PO");
        public bool CanNavPOApproval => _userSessionService.HasPermission("NAV_PO_APPROVAL");
        public bool CanNavGRN => _userSessionService.HasPermission("NAV_GRN");
        public bool CanNavGRNApproval => _userSessionService.HasPermission("NAV_GRN_APPROVAL");
        public bool CanNavSupplierReturn => _userSessionService.HasPermission("NAV_SUPPLIER_RETURN");
        public bool CanNavSupplierReturnApproval => _userSessionService.HasPermission("NAV_SUPPLIER_RETURN_APPROVAL");
        public bool CanNavSupplier => _userSessionService.HasPermission("NAV_SUPPLIER");


        // --- ACCOUNTS ---
        public bool CanAccessAccounts => _userSessionService.HasPermission("ACCESS_ACCOUNTS");
        public bool CanNavAccountManagement => _userSessionService.HasPermission("NAV_ACCOUNT_MANAGEMENT");
        public bool CanNavAccountsPayable => _userSessionService.HasPermission("NAV_ACCOUNTS_PAYABLE");
        public bool CanNavAccountsReceivable => _userSessionService.HasPermission("NAV_ACCOUNTS_RECEIVABLE");
        public bool CanNavExpenses => _userSessionService.HasPermission("NAV_EXPENSES");
        public bool CanNavOpeningBalance => _userSessionService.HasPermission("NAV_OPENING_BALANCE");


        // --- REPORTS ---
        public bool CanAccessReports => _userSessionService.HasPermission("ACCESS_REPORTS");
        public bool CanNavInvReport => _userSessionService.HasPermission("NAV_REPORT_INVENTORY");
        public bool CanNavSalesReport => _userSessionService.HasPermission("NAV_REPORT_SALES");

        // --- SECURITY ---
        public bool CanAccessSecurity => _userSessionService.HasPermission("ACCESS_SECURITY");
        public bool CanNavUsers => _userSessionService.HasPermission("NAV_USER_MANAGEMENT");

        // --- SETTINGS ---
        public bool CanAccessSettings => _userSessionService.HasPermission("ACCESS_SETTINGS");
        public bool CanNavConfiguration => _userSessionService.HasPermission("NAV_SYSTEM_CONFIGURATION");

        // --- POS TERMINAL ---
        public bool CanAccessPosTerminal => _userSessionService.HasPermission("ACCESS_POS_TERMINAL");



        private DateTime _currentDateTime;
        public DateTime CurrentDateTime
        {
            get => _currentDateTime;
            set => SetProperty(ref _currentDateTime, value);
        }

        private BaseViewModel _currentViewModel;
        public BaseViewModel CurrentViewModel
        {
            get => _currentViewModel;
            set { _currentViewModel = value; OnPropertyChanged(); }
        }

        private bool _isSidebarExpanded = true;
        public bool IsSidebarExpanded
        {
            get => _isSidebarExpanded;
            set => SetProperty(ref _isSidebarExpanded, value);
        }

        private bool _isUserMenuOpen;
        public bool IsUserMenuOpen
        {
            get => _isUserMenuOpen;
            set => SetProperty(ref _isUserMenuOpen, value);
        }

        public IUserSessionService CurrentUser => _userSessionService;
        #endregion

        #region Commands
        public ICommand ViewProfileCommand { get; private set; }
        public ICommand ToggleSidebarCommand { get; private set; }
        public ICommand NavigateToUserCommand { get; private set; }
        public ICommand NavigateToDashboardCommand { get; private set; }

        public ICommand NavigateToTableCommand { get; private set; }
        public ICommand NavigateToRoomRegisterCommand { get; private set; }
        public ICommand NavigateToRoomBookingCommand { get; private set; }
        public ICommand NavigateToMenuItemCommand { get; private set; }
        public ICommand NavigateToMenuCategoryCommand { get; private set; }
        public ICommand NavigateToMenuItemListCommand { get; private set; }
        public ICommand NavigateToMenuProfitabilityCommand { get; private set; }

        public ICommand NavigateToCategoryCommand { get; private set; }
        public ICommand NavigateToProductCommand { get; private set; }
        public ICommand NavigateToBarcodePrintCommand { get; private set; }
        public ICommand NavigateToStockTransferCommand { get; private set; }
        public ICommand NavigateToProductListCommand { get; private set; }
        public ICommand NavigateToUnitMeasureCommand { get; private set; }

        public ICommand NavigateToWastageCommand { get; private set; }
        public ICommand NavigateToSupplierCommand { get; private set; }
        public ICommand NavigateToGRNCommand { get; private set; }
        public ICommand NavigateToGRNApprovalCommand { get; private set; }
        public ICommand NavigateToPOApprovalCommand { get; private set; }
        public ICommand NavigateToPOCommand { get; private set; }
        public ICommand NavigateToSupplierReturnCommand { get; private set; }
        public ICommand NavigateToSupplierReturnApprovalCommand { get; private set; }
        public ICommand NavigateToCustomerCommand { get; private set; }
        public ICommand NavigateToInvoicesCommand { get; private set; }
        public ICommand NavigateToShiftManagementCommand { get; private set; }
        public ICommand NavigateToDiscountCommand { get; private set; }
        public ICommand NavigateToPromotionCommand { get; private set; }
        public ICommand NavigateToSalesReturnCommand { get; private set; }
        public ICommand NavigateToAccountsPayableCommand { get; private set; }
        public ICommand NavigateToAccountsReceivableCommand { get; private set; }
        public ICommand NavigateToExpensesCommand { get; private set; }
        public ICommand NavigateToAccountManagementCommand { get; private set; }
        public ICommand NavigateToOpeningBalanceCommand { get; private set; }
        public ICommand LogoutCommand { get; private set; }
        public ICommand OpenPosTerminalCommand { get; private set; }
        public ICommand OpenSalesViewCommand { get; private set; }
        public ICommand NavigateToInventoryReportCommand { get; private set; }
        public ICommand NavigateToSalesReportCommand { get; private set; }
        public ICommand NavigateToStockAdjustmentCommand { get; private set; }
        public ICommand NavigateToSettingsCommand { get; private set; }
        #endregion

        #region Methods
        private void InitializeCommands()
        {
            // Sidebar Toggle Command
            ToggleSidebarCommand = new RelayCommand(_ => ToggleSidebar());

            ViewProfileCommand = new RelayCommand(_ => ExecuteOpenProfile());

            LogoutCommand = new RelayCommand(_ => Logout());

            OpenSalesViewCommand = new AsyncRelayCommand(
                async _ => await OpenSalesViewAsync(),
                _ => _userSessionService.HasPermission("ACCESS_POS_TERMINAL"));
            OpenPosTerminalCommand = OpenSalesViewCommand;

            NavigateToDashboardCommand = new RelayCommand(_ =>
                NavigateTo<DashboardViewModel>("Dashboard"));

            // --- Restaurant Module ---
            NavigateToTableCommand = new RelayCommand(_ =>
                NavigateTo<TableViewModel>("Restaurant  →  Tables"));

            NavigateToRoomRegisterCommand = new RelayCommand(_ =>
                NavigateTo<RoomRegisterViewModel>("Restaurant  →  Rooms"));

            NavigateToRoomBookingCommand = new RelayCommand(_ =>
                NavigateTo<RoomBookingViewModel>("Restaurant  →  Room Bookings"));

            NavigateToMenuItemCommand = new RelayCommand(_ =>
                NavigateTo<MenuItemViewModel>("Restaurant  →  Menu Item Setup"));

            NavigateToMenuItemListCommand = new RelayCommand(_ =>
                NavigateTo<MenuItemListViewModel>("Restaurant  →  Menu Items"));

            NavigateToMenuCategoryCommand = new RelayCommand(_ =>
                NavigateTo<MenuCategoryViewModel>("Restaurant  →  Menu Categories"));

            NavigateToMenuProfitabilityCommand = new RelayCommand(_ =>
                NavigateTo<MenuProfitabilityViewModel>("Restaurant  →  Menu Profitability"));

            // --- Inventory Module ---
            NavigateToProductListCommand = new RelayCommand(_ =>
                NavigateTo<ProductListViewModel>("Inventory  →  Products"));

            NavigateToStockTransferCommand = new RelayCommand(_ =>
                NavigateTo<StockTransferViewModel>("Inventory  →  Stock Transfers"));

            NavigateToStockAdjustmentCommand = new RelayCommand(_ =>
                NavigateTo<StockAdjustmentViewModel>("Inventory  →  Stock Adjustments"));

            NavigateToWastageCommand = new RelayCommand(_ =>
                NavigateTo<WastageViewModel>("Inventory  →  Wastage"));

            NavigateToProductCommand = new RelayCommand(_ =>
                NavigateTo<ProductViewModel>("Inventory  →  Product Setup"));

            NavigateToCategoryCommand = new RelayCommand(_ =>
                NavigateTo<CategoryViewModel>("Inventory  →  Categories"));

            NavigateToUnitMeasureCommand = new RelayCommand(_ =>
                NavigateTo<UnitMeasureViewModel>("Inventory  →  Units of Measure"));

            NavigateToBarcodePrintCommand = new RelayCommand(_ =>
                NavigateTo<BarcodePrintViewModel>("Inventory  →  Barcode Printing"));

            // --- Purchasing Module ---
            NavigateToPOCommand = new RelayCommand(_ =>
                NavigateTo<GoodsPurchaseNoteViewModel>("Purchasing  →  Purchase Orders"));

            NavigateToGRNCommand = new RelayCommand(_ =>
                NavigateTo<GoodsReceiveNoteViewModel>("Purchasing  →  Goods Receipt Notes"));

            NavigateToGRNApprovalCommand = new RelayCommand(_ =>
                NavigateTo<GoodsReceiveNoteApprovalViewModel>("Purchasing  →  Goods Receipt Note Approval"));

            NavigateToPOApprovalCommand = new RelayCommand(_ =>
                NavigateTo<GoodsPurchaseNoteApprovalViewModel>("Purchasing  →  Purchase Order Approval"));

            NavigateToSupplierReturnCommand = new RelayCommand(_ =>
                NavigateTo<SupplierReturnViewModel>("Purchasing  →  Return To Supplier"));

            NavigateToSupplierReturnApprovalCommand = new RelayCommand(_ =>
                NavigateTo<SupplierReturnApprovalViewModel>("Purchasing  →  Return To Supplier Approval"));

            NavigateToSupplierCommand = new RelayCommand(_ =>
                NavigateTo<SupplierViewModel>("Purchasing  →  Suppliers"));

            // --- Sales Module ---
            NavigateToInvoicesCommand = new RelayCommand(_ =>
                NavigateTo<SalesListViewModel>("Sales  →  Invoices"));

            NavigateToCustomerCommand = new RelayCommand(_ =>
                NavigateTo<CustomerViewModel>("Sales  →  Customers"));

            NavigateToShiftManagementCommand = new RelayCommand(_ =>
                NavigateTo<ShiftManagementViewModel>("Sales  →  Shift Management"));

            NavigateToDiscountCommand = new RelayCommand(_ =>
                NavigateTo<DiscountViewModel>("Sales  →  Discount Management"));

            NavigateToPromotionCommand = new RelayCommand(_ =>
                NavigateTo<PromotionViewModel>("Sales  →  Auto Promotions"));

            NavigateToSalesReturnCommand = new RelayCommand(_ =>
                NavigateTo<SalesReturnViewModel>("Sales  →  Sales Return"));

            // --- Accounts Module ---
            NavigateToAccountsPayableCommand = new RelayCommand(_ =>
                NavigateTo<SupplierPaymentTabViewModel>("Accounts  →  Accounts Payable"));

            NavigateToAccountsReceivableCommand = new RelayCommand(_ =>
                NavigateTo<CustomerPaymentTabViewModel>("Accounts  →  Accounts Receivable"));

            NavigateToAccountManagementCommand = new RelayCommand(_ =>
                NavigateTo<AccountRegisterViewModel>("Accounts  →  Chart of Accounts"));

            NavigateToExpensesCommand = new RelayCommand(_ =>
                NavigateTo<ExpensesViewModel>("Accounts  →  Expenses"));

            NavigateToOpeningBalanceCommand = new RelayCommand(_ =>
                NavigateTo<OpeningBalanceViewModel>("Accounts  →  Opening Balances"));

            // --- Reports Module ---
            NavigateToInventoryReportCommand = new RelayCommand(_ =>
                NavigateTo<InventoryReportViewModel>("Reports  →  Inventory Reports"));

            // --- Security Module ---
            NavigateToUserCommand = new RelayCommand(_ =>
                NavigateTo<UserViewModel>("Security  →  User Management"));

            // --- Settings Module ---
            NavigateToSettingsCommand = new RelayCommand(_ =>
                NavigateTo<SettingsTabViewModel>("Settings  →  System Configuration"));

        }
        private void NavigateTo<TViewModel>(string pageTitle) where TViewModel : BaseViewModel
        {
            // 1. Set the Title (The "Inventory --> Category" part)
            CurrentPageTitle = pageTitle;

            // 2. Set the View Model (This switches the view)
            CurrentViewModel = _serviceProvider.GetRequiredService<TViewModel>();
        }
        private void ToggleSidebar()
        {
            IsSidebarExpanded = !IsSidebarExpanded;
        }

        private void ExecuteOpenProfile()
        {
            try
            {
                // Close the dropdown menu first so it's not floating when the dialog opens
                IsUserMenuOpen = false;

                // Use DialogService to open the window
                // This assumes UserProfileViewModel matches the UserProfileWindow in your DI/DialogService mapping
                _dialogService.ShowDialog<UserProfileViewModel>(out var viewModel);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening profile: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    Application.Current.Windows.OfType<MainView>().FirstOrDefault()?.Close();
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

        private async System.Threading.Tasks.Task OpenSalesViewAsync()
        {
            try
            {
                var machineName = Environment.MachineName;
                var activeTillShift = await _shiftRepository.CheckActiveShiftAsync(machineName, _userSessionService.UserId);

                if (!ResolveCashierShift(activeTillShift, machineName))
                {
                    return;
                }

                var nextWindow = _serviceProvider.GetRequiredService<SalesView>();
                nextWindow.DataContext = _serviceProvider.GetRequiredService<SalesViewModel>();

                Application.Current.MainWindow = nextWindow;
                nextWindow.Show();

                Application.Current.Windows.OfType<MainView>().FirstOrDefault()?.Close();
            }
            catch (SqlException ex)
            {
                MessageBox.Show($"Unable to resolve POS shift: {ex.Message}",
                              "POS Shift",
                              MessageBoxButton.OK,
                              MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to open POS Terminal: {ex.Message}",
                              "Error",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
            }
        }

        private bool ResolveCashierShift(TillShiftStatusDto activeTillShift, string machineName)
        {
            if (activeTillShift == null)
            {
                throw new InvalidOperationException($"Unable to resolve POS register for terminal '{machineName}'.");
            }

            if (activeTillShift.RequiresFloat)
            {
                var shiftDialogResult = _dialogService.ShowDialog<OpenShiftDialogViewModel>(
                    vm => vm.InitializeTerminal(activeTillShift.TillId, activeTillShift.RegisterName),
                    out var openShiftDialogViewModel);

                if (shiftDialogResult != true || openShiftDialogViewModel?.OpenedShift == null)
                {
                    return false;
                }

                _userSessionService.SetCurrentShift(openShiftDialogViewModel.OpenedShift.Id);
                return true;
            }

            if (!activeTillShift.ShiftId.HasValue)
            {
                throw new InvalidOperationException("The terminal shift state is invalid.");
            }

            _userSessionService.SetCurrentShift(activeTillShift.ShiftId.Value);
            return true;
        }
        #endregion

    }
}

