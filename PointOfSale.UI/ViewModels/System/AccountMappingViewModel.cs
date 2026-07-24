using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using global::System.Windows;
using global::System.Windows.Input;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces;
using PointOfSale.Core.Interfaces.Repositories.System;
using PointOfSale.Core.Models.System;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Settings
{
    public class AccountMappingViewModel : BaseViewModel
    {
        private readonly IAccountMappingRepository _accountMappingRepository;
        private readonly IAccountingRepository _accountingRepository;
        private readonly IUserSessionService _userSessionService;

        public AccountMappingViewModel(
            IAccountMappingRepository accountMappingRepository,
            IAccountingRepository accountingRepository,
            IUserSessionService userSessionService)
        {
            _accountMappingRepository = accountMappingRepository ?? throw new ArgumentNullException(nameof(accountMappingRepository));
            _accountingRepository = accountingRepository ?? throw new ArgumentNullException(nameof(accountingRepository));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));

            UpdateAccountMappingsCommand = new AsyncRelayCommand(async _ => await UpdateAccountMappingsAsync());

            _ = LoadInitialDataAsync();
        }

        public ObservableCollection<AccountDto> LedgerAccounts { get; } = new ObservableCollection<AccountDto>();

        private int? _selectedCashAccountId;
        public int? SelectedCashAccountId
        {
            get => _selectedCashAccountId;
            set => SetProperty(ref _selectedCashAccountId, value);
        }

        private int? _selectedCardAccountId;
        public int? SelectedCardAccountId
        {
            get => _selectedCardAccountId;
            set => SetProperty(ref _selectedCardAccountId, value);
        }

        private int? _selectedArAccountId;
        public int? SelectedArAccountId
        {
            get => _selectedArAccountId;
            set => SetProperty(ref _selectedArAccountId, value);
        }

        private int? _selectedVarianceAccountId;
        public int? SelectedVarianceAccountId
        {
            get => _selectedVarianceAccountId;
            set => SetProperty(ref _selectedVarianceAccountId, value);
        }

        private int? _selectedCustomerAdvanceAccountId;
        public int? SelectedCustomerAdvanceAccountId
        {
            get => _selectedCustomerAdvanceAccountId;
            set => SetProperty(ref _selectedCustomerAdvanceAccountId, value);
        }

        private int? _selectedApAccountId;
        public int? SelectedApAccountId
        {
            get => _selectedApAccountId;
            set => SetProperty(ref _selectedApAccountId, value);
        }

        private int? _selectedLoyaltyPayableAccountId;
        public int? SelectedLoyaltyPayableAccountId
        {
            get => _selectedLoyaltyPayableAccountId;
            set => SetProperty(ref _selectedLoyaltyPayableAccountId, value);
        }

        private int? _selectedLoyaltyExpenseAccountId;
        public int? SelectedLoyaltyExpenseAccountId
        {
            get => _selectedLoyaltyExpenseAccountId;
            set => SetProperty(ref _selectedLoyaltyExpenseAccountId, value);
        }

        private int? _selectedFreeIssueAccountId;
        public int? SelectedFreeIssueAccountId
        {
            get => _selectedFreeIssueAccountId;
            set => SetProperty(ref _selectedFreeIssueAccountId, value);
        }

        public ICommand UpdateAccountMappingsCommand { get; }

        private async Task LoadInitialDataAsync()
        {
            await LoadLedgerAccountsAsync();
            await LoadAccountMappingsAsync();
        }

        private async Task LoadLedgerAccountsAsync()
        {
            try
            {
                var accounts = await _accountingRepository.GetAccountsAsync();

                LedgerAccounts.Clear();
                foreach (var account in accounts.Where(a => !a.IsHeader && a.IsActive))
                {
                    LedgerAccounts.Add(account);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load ledger accounts: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadAccountMappingsAsync()
        {
            try
            {
                var mappings = await _accountMappingRepository.GetSystemAccountMappingsAsync();

                SelectedCashAccountId = mappings.TryGetValue(AccountMappingKeys.CashAccount, out var cashId) ? cashId : null;
                SelectedCardAccountId = mappings.TryGetValue(AccountMappingKeys.CardAccount, out var cardId) ? cardId : null;
                SelectedArAccountId = mappings.TryGetValue(AccountMappingKeys.ArAccount, out var arId) ? arId : null;
                SelectedVarianceAccountId = mappings.TryGetValue(AccountMappingKeys.CashVariance, out var varianceId) ? varianceId : null;
                SelectedCustomerAdvanceAccountId = mappings.TryGetValue(AccountMappingKeys.CustomerAdvance, out var customerAdvanceId) ? customerAdvanceId : null;
                SelectedApAccountId = mappings.TryGetValue(AccountMappingKeys.ApAccount, out var apId) ? apId : null;
                SelectedLoyaltyPayableAccountId = mappings.TryGetValue(AccountMappingKeys.LoyaltyPayable, out var loyaltyPayableId) ? loyaltyPayableId : null;
                SelectedLoyaltyExpenseAccountId = mappings.TryGetValue(AccountMappingKeys.LoyaltyExpense, out var loyaltyExpenseId) ? loyaltyExpenseId : null;
                SelectedFreeIssueAccountId = mappings.TryGetValue(AccountMappingKeys.FreeIssueExpense, out var freeIssueId) ? freeIssueId : null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load account mappings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task UpdateAccountMappingsAsync()
        {
            try
            {
                await _accountMappingRepository.UpdateSystemAccountMappingsAsync(
                    SelectedCashAccountId,
                    SelectedCardAccountId,
                    SelectedArAccountId,
                    SelectedVarianceAccountId,
                    SelectedCustomerAdvanceAccountId,
                    SelectedApAccountId,
                    SelectedLoyaltyPayableAccountId,
                    SelectedLoyaltyExpenseAccountId,
                    SelectedFreeIssueAccountId,
                    _userSessionService.CurrentUser.UserId);

                MessageBox.Show("Account mappings updated successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating account mappings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
