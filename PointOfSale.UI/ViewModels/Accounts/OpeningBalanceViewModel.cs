using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces;
using PointOfSale.Core.Interfaces.Repositories.Accounts;
using PointOfSale.Core.Models.Accounts.Entities;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Accounts
{
    public class OpeningBalanceViewModel : BaseViewModel
    {
        private readonly IOpeningBalanceRepository _openingBalanceRepository;
        private readonly IAccountingRepository _accountingRepository;
        private readonly IUserSessionService _userSessionService;

        private DateTime _asOfDate = DateTime.Today;
        private AccountDto _selectedEquityAccount;

        public OpeningBalanceViewModel(
            IOpeningBalanceRepository openingBalanceRepository,
            IAccountingRepository accountingRepository,
            IUserSessionService userSessionService)
        {
            _openingBalanceRepository = openingBalanceRepository ?? throw new ArgumentNullException(nameof(openingBalanceRepository));
            _accountingRepository = accountingRepository ?? throw new ArgumentNullException(nameof(accountingRepository));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));

            Lines = new ObservableCollection<OpeningBalanceLine>();
            EquityAccounts = new ObservableCollection<AccountDto>();

            LoadCommand = new AsyncRelayCommand(async _ => await LoadAsync());
            SaveCommand = new AsyncRelayCommand(async _ => await SaveAsync(), _ => CanSave());
            ClearCommand = new RelayCommand(_ => ClearLines());

            _ = LoadAsync();
        }

        public ObservableCollection<OpeningBalanceLine> Lines { get; }
        public ObservableCollection<AccountDto> EquityAccounts { get; }

        public DateTime AsOfDate
        {
            get => _asOfDate;
            set => SetProperty(ref _asOfDate, value);
        }

        public AccountDto SelectedEquityAccount
        {
            get => _selectedEquityAccount;
            set
            {
                if (SetProperty(ref _selectedEquityAccount, value))
                {
                    RaiseCanExecuteChanged();
                }
            }
        }

        // Display-only preview. The server (uspPostOpeningBalance) is the
        // authoritative source for the posted contra amount.
        public decimal TotalDebit => Lines.Sum(l => l.DebitAmount);
        public decimal TotalCredit => Lines.Sum(l => l.CreditAmount);
        public decimal Difference => TotalDebit - TotalCredit;

        public string ContraPreview =>
            Difference == 0m
                ? "Balanced — no contra needed"
                : Difference > 0m
                    ? $"Opening Balance Equity will be credited {Difference:N2}"
                    : $"Opening Balance Equity will be debited {-Difference:N2}";

        public ICommand LoadCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand ClearCommand { get; }

        private async Task LoadAsync()
        {
            try
            {
                var accounts = await _accountingRepository.GetAccountsAsync();
                var accountTypes = await _accountingRepository.GetAccountTypesAsync();

                var validBalanceSheetTypeIds = accountTypes
                    .Where(t =>
                        (t.Name ?? string.Empty).ToUpperInvariant().Contains("ASSET") ||
                        (t.Name ?? string.Empty).ToUpperInvariant().Contains("LIABILIT") ||
                        (t.Name ?? string.Empty).ToUpperInvariant().Contains("EQUIT") ||
                        (t.Name ?? string.Empty).ToUpperInvariant().Contains("CAPITAL")) // In case equity is named 'Capital'
                    .Select(t => t.AccountTypeId) // If your DTO uses AccountTypeId instead of Id, change this here
                    .ToList();

                var equityType = accountTypes.FirstOrDefault(t =>
                    (t.Code ?? string.Empty).Trim().ToUpperInvariant().StartsWith("EQU") ||
                    (t.Name ?? string.Empty).Trim().ToUpperInvariant().StartsWith("EQUITY"));

                var postableAccounts = accounts
                    .Where(a => !a.IsHeader &&
                                a.IsActive &&
                                validBalanceSheetTypeIds.Contains(a.AccountTypeId)) // <-- New Filter Applied
                    .OrderBy(a => a.Code)
                    .ToList();

                foreach (var line in Lines)
                {
                    line.PropertyChanged -= Line_PropertyChanged;
                }
                Lines.Clear();

                foreach (var account in postableAccounts)
                {
                    var line = new OpeningBalanceLine
                    {
                        AccountId = account.Id,
                        AccountCode = account.Code,
                        AccountName = account.Name
                    };
                    line.PropertyChanged += Line_PropertyChanged;
                    Lines.Add(line);
                }

                EquityAccounts.Clear();
                if (equityType != null)
                {
                    foreach (var account in postableAccounts.Where(a => a.AccountTypeId == equityType.AccountTypeId))
                    {
                        account.DisplayName = $"{account.Code} - {account.Name}";
                        EquityAccounts.Add(account);
                    }
                }
                SelectedEquityAccount = EquityAccounts.FirstOrDefault();

                RaiseTotalsChanged();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load accounts: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SaveAsync()
        {
            if (SelectedEquityAccount == null)
            {
                MessageBox.Show("Select the Opening Balance Equity account first.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var activeLines = Lines.Where(l => l.DebitAmount > 0m || l.CreditAmount > 0m).ToList();

            if (!activeLines.Any())
            {
                MessageBox.Show("Enter at least one account balance before saving.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (activeLines.Any(l => l.HasConflict))
            {
                MessageBox.Show("A line cannot have both a Debit and a Credit amount.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var locationId = _userSessionService.BranchId;

                if (await _openingBalanceRepository.OpeningBalanceExistsAsync(locationId))
                {
                    var confirm = MessageBox.Show(
                        "An opening balance has already been posted for this location. Post another one?",
                        "Confirm Re-post",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (confirm != MessageBoxResult.Yes) return;
                }

                var headerId = await _openingBalanceRepository.PostOpeningBalanceAsync(
                    locationId,
                    AsOfDate,
                    SelectedEquityAccount.Id,
                    _userSessionService.UserId,
                    activeLines);

                MessageBox.Show($"Opening balance posted successfully (Journal #{headerId}).", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                ClearLines();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error posting opening balance: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanSave()
        {
            return SelectedEquityAccount != null &&
                   Lines.Any(l => l.DebitAmount > 0m || l.CreditAmount > 0m) &&
                   Lines.All(l => !l.HasConflict);
        }

        private void ClearLines()
        {
            foreach (var line in Lines)
            {
                line.DebitAmount = 0m;
                line.CreditAmount = 0m;
            }

            AsOfDate = DateTime.Today;
            RaiseTotalsChanged();
        }

        private void Line_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(OpeningBalanceLine.DebitAmount) ||
                e.PropertyName == nameof(OpeningBalanceLine.CreditAmount))
            {
                RaiseTotalsChanged();
                RaiseCanExecuteChanged();
            }
        }

        private void RaiseTotalsChanged()
        {
            OnPropertyChanged(nameof(TotalDebit));
            OnPropertyChanged(nameof(TotalCredit));
            OnPropertyChanged(nameof(Difference));
            OnPropertyChanged(nameof(ContraPreview));
        }

        private void RaiseCanExecuteChanged()
        {
            (SaveCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
    }
}
