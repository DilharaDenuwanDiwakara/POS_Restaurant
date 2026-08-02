using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.Threading;
using System.Windows.Markup;
using PointOfSale.Core.Interfaces;
using PointOfSale.Core.Interfaces.Purchasing;
using PointOfSale.Core.Interfaces.Repositories.Accounts;
using PointOfSale.Core.Interfaces.Repositories.Inventory;
using PointOfSale.Core.Interfaces.Repositories.Purchasing;
using PointOfSale.Core.Interfaces.Repositories.Restaurant;
using PointOfSale.Core.Interfaces.Repositories.Sales;
using PointOfSale.Core.Interfaces.Repositories.System;
using PointOfSale.Core.Interfaces.Security;
using PointOfSale.Core.Interfaces.Services;
using PointOfSale.Core.Services;
using PointOfSale.Infrastructure;
using PointOfSale.Infrastructure.Repositories;
using PointOfSale.Infrastructure.Repositories.Accounts;
using PointOfSale.Infrastructure.Repositories.Inventory;
using PointOfSale.Infrastructure.Repositories.Purchasing;
using PointOfSale.Infrastructure.Repositories.Restaurant;
using PointOfSale.Infrastructure.Repositories.Sales;
using PointOfSale.Infrastructure.Repositories.Security;
using PointOfSale.Infrastructure.Repositories.System;
using PointOfSale.Infrastructure.Service;
using PointOfSale.UI.Services;
using PointOfSale.UI.ViewModels.Accounts;
using PointOfSale.UI.ViewModels.Inventory;
using PointOfSale.UI.ViewModels.Purchasing;
using PointOfSale.UI.ViewModels.Restaurant;
using PointOfSale.UI.ViewModels.Sales;
using PointOfSale.UI.ViewModels.Security;
using PointOfSale.UI.ViewModels.Settings;
using PointOfSale.UI.ViewModels.Shell;
using PointOfSale.UI.Views.Accounts;
using PointOfSale.UI.Views.Sales;
using PointOfSale.UI.Views.Sales.Dialogs;
using PointOfSale.UI.Views.Security;
using PointOfSale.UI.Views.Security.Dialogs;
using PointOfSale.UI.Views.Shell;

namespace PointOfSale.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private readonly ServiceProvider _serviceProvider;

        public App()
        {
            ConfigureApplicationCulture();

            var services = new ServiceCollection();
            ConfigureServices(services);

            _serviceProvider = services.BuildServiceProvider();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Register Database 
            services.AddSingleton<DatabaseConnection>();
            services.AddSingleton<CentralAccountConnection>();

            // --- Register Repositories ---
            services.AddSingleton<IUserRepository, UserRepository>();
            services.AddSingleton<ISupplierRepository, SupplierRepository>();

            services.AddSingleton<IBrandRepository, BrandRepository>();
            services.AddSingleton<ICategoryRepository, CategoryRepository>();
            services.AddSingleton<IUnitMeasureRepository, UnitMeasureRepository>();
            services.AddSingleton<IProductRepository, ProductRepository>();
            services.AddSingleton<IProductBatchRepository, ProductBatchRepository>();
            services.AddSingleton<IInventoryRepository, InventoryRepository>();
            services.AddSingleton<IStockAdjustmentRepository, StockAdjustmentRepository>();
            services.AddSingleton<IGoodsReceiveNoteRepository, GoodsReceiveNoteRepository>();
            services.AddSingleton<IGoodsPurchaseNoteRepository, GoodsPurchaseNoteRepository>();
            services.AddSingleton<ISupplierReturnRepository, SupplierReturnRepository>();
            services.AddSingleton<ICustomerRepository, CustomerRepository>();
            services.AddSingleton<IDashboardRepository, DashboardRepository>();
            services.AddSingleton<IDiscountRepository, DiscountRepository>();
            services.AddSingleton<IPromotionRepository, PromotionRepository>();
            services.AddSingleton<IRegisterRepository, RegisterRepository>();
            services.AddSingleton<ISalesRepository, SalesRepository>();
            services.AddSingleton<ISalesHoldRepository, SalesHoldRepository>();
            services.AddSingleton<ICashInOutRepository, CashInOutRepository>();
            services.AddSingleton<IShiftRepository, ShiftRepository>();
            services.AddSingleton<IWastageRepository, WastageRepository>();
            services.AddSingleton<IWastageReasonRepository, WastageReasonRepository>();

            // Restaurant
            services.AddSingleton<ITableRepository, TableRepository>();
            services.AddSingleton<IRoomBookingRepository, RoomBookingRepository>();
            services.AddSingleton<IMenuItemRepository, MenuItemRepository>();
            services.AddSingleton<IMenuCategoryRepository, MenuCategoryRepository>();
            services.AddSingleton<IMealPeriodRepository, MealPeriodRepository>();
            services.AddSingleton<IStationRepository, StationRepository>();
            services.AddSingleton<IOrderRepository, OrderRepository>();

            services.AddSingleton<ISupplierPaymentRepository, SupplierPaymentRepository>();
            services.AddSingleton<ISupplierAdvanceRepository, SupplierAdvanceRepository>();
            services.AddSingleton<ICustomerAdvanceRepository, CustomerAdvanceRepository>();
            services.AddSingleton<ICustomerPaymentRepository, CustomerPaymentRepository>();
            services.AddSingleton<ISupplierCreditRepository, SupplierCreditRepository>();
            services.AddSingleton<IExpensesRepository, ExpensesRepository>();
            services.AddSingleton<IExpensesCategoryRepository, ExpensesCategoryRepository>();
            services.AddSingleton<IAccountingRepository, AccountingRepository>();
            services.AddSingleton<IOpeningBalanceRepository, OpeningBalanceRepository>();
            services.AddSingleton<IInventoryReportRepository, InventoryReportRepository>();
            services.AddSingleton<ILocationRepository, LocationRepository>();
            services.AddSingleton<IBranchRepository, BranchRepository>();
            services.AddSingleton<ICompanyRepository, CompanyRepository>();
            services.AddSingleton<ITaxConfigurationRepository, TaxConfigurationRepository>();
            services.AddSingleton<IBankRepository, BankRepository>();
            services.AddSingleton<IPaymentTerminalRepository, PaymentTerminalRepository>();
            services.AddSingleton<IAccountMappingRepository, AccountMappingRepository>();

            // Register Services
            services.AddSingleton<IPasswordHasher, PasswordHasher>();
            services.AddSingleton<IAuthService, AuthService>();
            services.AddSingleton<IConfigurationService, ConfigurationService>();
            services.AddSingleton<IUserSessionService, UserSessionService>();
            services.AddSingleton<IDialogService, DialogService>();
#if NO_CRYSTAL_REPORTS
            services.AddSingleton<ISalesInvoiceReportPreviewService, UnavailableSalesInvoiceReportPreviewService>();
#else
            services.AddSingleton<ISalesInvoiceReportPreviewService, SalesInvoiceReportPreviewService>();
#endif
            services.AddSingleton<IExcelService, ExcelService>();
            services.AddSingleton<IBarcodeService, BarcodeService>();
            services.AddSingleton<CloudStorageService>();

            // Other ViewModels are Transient: a new one is created each time you navigate to it.
            services.AddTransient<LoginViewModel>();
            services.AddTransient<LoginView>();
            services.AddTransient<DeviceRegistrationViewModel>();
            services.AddTransient<DeviceRegistrationWindow>();

            services.AddTransient<MainViewModel>();
            services.AddTransient<MainView>();
            services.AddTransient<DashboardViewModel>();

            services.AddTransient<MenuItemViewModel>();
            services.AddTransient<RoomRegisterViewModel>();
            services.AddTransient<RoomBookingViewModel>();
            services.AddTransient<TableViewModel>();
            services.AddTransient<MenuCategoryViewModel>();
            services.AddTransient<MealPeriodViewModel>();
            services.AddTransient<StationViewModel>();
            services.AddTransient<MenuItemListViewModel>();
            services.AddTransient<SalesViewModel>();
            services.AddTransient<SalesView>();

            services.AddTransient<CashInOutViewModel>();
            services.AddTransient<OpenShiftDialogViewModel>();
            services.AddTransient<CloseShiftDialogViewModel>();
            services.AddTransient<ShiftManagementViewModel>();
            services.AddTransient<SalesListViewModel>();
            services.AddTransient<DiscountViewModel>();
            services.AddTransient<PromotionViewModel>();
            services.AddTransient<OpenShiftDialogView>();
            services.AddTransient<CloseShiftDialogView>();

            services.AddTransient<UserViewModel>();
            services.AddTransient<UserProfileViewModel>();
            services.AddTransient<SupplierViewModel>();

            services.AddTransient<ProductViewModel>();
            services.AddTransient<StockTransferViewModel>();
            services.AddTransient<StockAdjustmentViewModel>();
            services.AddTransient<BarcodePrintViewModel>();
            services.AddTransient<CategoryViewModel>();
            services.AddTransient<UnitMeasureViewModel>();
            services.AddTransient<GoodsReceiveNoteViewModel>();
            services.AddTransient<GoodsReceiveNoteApprovalViewModel>();
            services.AddTransient<GoodsPurchaseNoteViewModel>();
            services.AddTransient<GoodsPurchaseNoteApprovalViewModel>();
            services.AddTransient<CustomerViewModel>();
            services.AddTransient<SupplierReturnViewModel>();
            services.AddTransient<SupplierReturnApprovalViewModel>();
            services.AddTransient<BatchSelectionViewModel>();
            services.AddTransient<ProductListViewModel>();
            services.AddTransient<WastageViewModel>();
            services.AddTransient<WastageReasonViewModel>();

            services.AddTransient<PriceUpdateViewModel>();

            services.AddTransient<SupplierPaymentTabViewModel>();
            services.AddTransient<SupplierPaymentViewModel>();
            services.AddTransient<SupplierAdvanceViewModel>();
            services.AddTransient<SupplierSettlementViewModel>();
            services.AddTransient<InventoryReportViewModel>();

            services.AddTransient<CustomerPaymentTabViewModel>();
            services.AddTransient<CustomerPaymentViewModel>();
            services.AddTransient<CustomerAdvanceViewModel>();
            services.AddTransient<CustomerSettlementViewModel>();

            services.AddTransient<ExpensesViewModel>();
            services.AddTransient<ExpensesCategoryViewModel>();

            services.AddTransient<AccountRegisterViewModel>();
            services.AddTransient<AccountRegisterView>();
            services.AddTransient<AccountTypeRegisterViewModel>();
            services.AddTransient<AccountTypeRegisterWindow>();
            services.AddTransient<OpeningBalanceViewModel>();
            services.AddTransient<OpeningBalanceView>();

            services.AddTransient<SettingsTabViewModel>();
            services.AddTransient<CompanySettingsViewModel>();
            services.AddTransient<TaxConfigurationViewModel>();
            services.AddTransient<BankViewModel>();
            services.AddTransient<PaymentTerminalViewModel>();
            services.AddTransient<AccountMappingViewModel>();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            System.Net.ServicePointManager.SecurityProtocol =
                System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls13;

            var loginView = new LoginView()
            {
                DataContext = _serviceProvider.GetRequiredService<LoginViewModel>()
            };

            loginView.ShowDialog();
        }

        private static void ConfigureApplicationCulture()
        {
            var culture = (CultureInfo)CultureInfo.CurrentCulture.Clone();
            culture.DateTimeFormat.ShortDatePattern = "dd/MM/yyyy";
            culture.DateTimeFormat.LongDatePattern = "dd/MM/yyyy";

            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

            // WPF bindings and DatePicker text use FrameworkElement.Language for culture.
            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(XmlLanguage.GetLanguage("en-GB")));
        }
    }
}


