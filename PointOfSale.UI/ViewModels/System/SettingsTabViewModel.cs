using PointOfSale.Core.Services;

namespace PointOfSale.UI.ViewModels.Settings
{
    public class SettingsTabViewModel : BaseViewModel
    {
        private readonly IUserSessionService _userSessionService;

        public SettingsTabViewModel(
            CompanySettingsViewModel companySettingsViewModel,
            TaxConfigurationViewModel taxConfigurationViewModel,
            BankViewModel bankViewModel,
            PaymentTerminalViewModel paymentTerminalViewModel,
            AccountMappingViewModel accountMappingViewModel,
            IUserSessionService sessionService)
        {
            CompanySettingsViewModel = companySettingsViewModel;
            TaxConfigurationViewModel = taxConfigurationViewModel;
            BankViewModel = bankViewModel;
            PaymentTerminalViewModel = paymentTerminalViewModel;
            AccountMappingViewModel = accountMappingViewModel;
            _userSessionService = sessionService;

            SetFirstAvailableTab();
        }

        #region Permissions
        public bool CanNavCompany => _userSessionService.HasPermission("SETTINGS_CONFIG_COMPANY");
        public bool CanNavTax => _userSessionService.HasPermission("SETTINGS_CONFIG_TAX");
        public bool CanNavBank => _userSessionService.HasPermission("SETTINGS_CONFIG_BANK");
        public bool CanNavPaymentTerminal => _userSessionService.HasPermission("SETTINGS_CONFIG_TERMINAL");
        public bool CanNavAccountMapping => _userSessionService.HasPermission("SETTINGS_CONFIG_ACCOUNT_MAPPING");
        #endregion

        public CompanySettingsViewModel CompanySettingsViewModel { get; }
        public TaxConfigurationViewModel TaxConfigurationViewModel { get; }
        public BankViewModel BankViewModel { get; }
        public PaymentTerminalViewModel PaymentTerminalViewModel { get; }
        public AccountMappingViewModel AccountMappingViewModel { get; }

        private int _selectedTabIndex;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetProperty(ref _selectedTabIndex, value);
        }

        /// <summary>
        /// Evaluates permissions and selects the first visible tab to prevent a blank screen.
        /// </summary>
        private void SetFirstAvailableTab()
        {
            if (CanNavCompany) SelectedTabIndex = 0;
            else if (CanNavTax) SelectedTabIndex = 1;
            else if (CanNavBank) SelectedTabIndex = 2;
            else if (CanNavPaymentTerminal) SelectedTabIndex = 3;
            else if (CanNavAccountMapping) SelectedTabIndex = 4;
            else SelectedTabIndex = -1; // Fallback if they somehow have zero permissions here
        }
    }
}
