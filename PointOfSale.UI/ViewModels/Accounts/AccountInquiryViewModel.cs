using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Exceptions;
using PointOfSale.Core.Interfaces;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Accounts
{
    public sealed class AccountInquiryViewModel : BaseViewModel
    {
        private readonly IAccountReportRepository _repository;
        private readonly IAccountingRepository _accountingRepository;
        private readonly AsyncRelayCommand _loadReportCommand;
        private readonly AsyncRelayCommand _reloadAccountsCommand;
        private AccountInquiryAccountOption _selectedAccount;
        private bool _isLoadingAccounts;
        private bool _accountsLoaded;
        private string _accountsErrorMessage;
        private string _reportGeneralAccountCode;
        private DateTime _fromDate;
        private DateTime _toDate;
        private AccountInquirySummaryDto _summary;
        private ObservableCollection<AccountInquiryLineDto> _transactionLines =
            new ObservableCollection<AccountInquiryLineDto>();
        private bool _isLoading;
        private int _loadedAccountId;
        private DateTime _loadedFromDate;
        private DateTime _loadedToDate;

        public AccountInquiryViewModel(IAccountReportRepository repository, IAccountingRepository accountingRepository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _accountingRepository = accountingRepository ?? throw new ArgumentNullException(nameof(accountingRepository));
            var today = DateTime.Today;
            _fromDate = new DateTime(today.Year, today.Month, 1);
            _toDate = today;
            _loadReportCommand = new AsyncRelayCommand(_ => LoadReportAsync(),
                _ => !IsLoading && !IsLoadingAccounts && SelectedAccount != null);
            _reloadAccountsCommand = new AsyncRelayCommand(_ => LoadAccountsAsync(true),
                _ => !IsLoading && !IsLoadingAccounts);
        }

        public ObservableCollection<AccountInquiryAccountOption> Accounts { get; } =
            new ObservableCollection<AccountInquiryAccountOption>();

        public AccountInquiryAccountOption SelectedAccount
        {
            get => _selectedAccount;
            set
            {
                if (value != null && (value.IsGroupHeading || !Accounts.Contains(value)))
                    return;
                if (SetProperty(ref _selectedAccount, value))
                {
                    OnPropertyChanged(nameof(SelectedAccountId));
                    OnPropertyChanged(nameof(HasFilterChanges));
                    _loadReportCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public int SelectedAccountId
        {
            get => SelectedAccount?.Id ?? 0;
            set => SelectedAccount = Accounts.FirstOrDefault(a => !a.IsGroupHeading && a.Id == value);
        }

        public bool IsLoadingAccounts
        {
            get => _isLoadingAccounts;
            private set
            {
                if (SetProperty(ref _isLoadingAccounts, value))
                {
                    _loadReportCommand.RaiseCanExecuteChanged();
                    _reloadAccountsCommand.RaiseCanExecuteChanged();
                    OnPropertyChanged(nameof(AccountsHint));
                }
            }
        }

        public string AccountsErrorMessage
        {
            get => _accountsErrorMessage;
            private set
            {
                if (SetProperty(ref _accountsErrorMessage, value))
                    OnPropertyChanged(nameof(HasAccountsError));
            }
        }

        public bool HasAccountsError => !string.IsNullOrWhiteSpace(AccountsErrorMessage);
        public string AccountsHint => IsLoadingAccounts ? "Loading chart of accounts..."
            : !_accountsLoaded ? "Reload accounts to load the chart of accounts."
            : Accounts.Any(a => !a.IsGroupHeading)
                ? "Choose an account. Type its code while the list is focused to jump to it. Both dates are included."
                : "No accounts are available. Add accounts in Chart of Accounts, then reload.";

        public string ReportGeneralAccountCode
        {
            get => _reportGeneralAccountCode;
            private set => SetProperty(ref _reportGeneralAccountCode, value);
        }

        public DateTime FromDate
        {
            get => _fromDate;
            set
            {
                if (SetProperty(ref _fromDate, value.Date))
                    OnPropertyChanged(nameof(HasFilterChanges));
            }
        }

        public DateTime ToDate
        {
            get => _toDate;
            set
            {
                if (SetProperty(ref _toDate, value.Date))
                    OnPropertyChanged(nameof(HasFilterChanges));
            }
        }

        public AccountInquirySummaryDto Summary
        {
            get => _summary;
            private set
            {
                if (SetProperty(ref _summary, value))
                {
                    OnPropertyChanged(nameof(HasReport));
                    OnPropertyChanged(nameof(ReportPeriod));
                    OnPropertyChanged(nameof(HasFilterChanges));
                }
            }
        }

        public bool HasReport => Summary != null;
        public string ReportPeriod => HasReport
            ? $"{_loadedFromDate:dd MMM yyyy} – {_loadedToDate:dd MMM yyyy}"
            : "Select an account and date range to get started.";
        public bool HasFilterChanges => HasReport &&
            (SelectedAccountId != _loadedAccountId || FromDate != _loadedFromDate || ToDate != _loadedToDate);

        public ObservableCollection<AccountInquiryLineDto> TransactionLines
        {
            get => _transactionLines;
            private set => SetProperty(ref _transactionLines, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set
            {
                if (SetProperty(ref _isLoading, value))
                {
                    _loadReportCommand.RaiseCanExecuteChanged();
                    _reloadAccountsCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public ICommand LoadReportCommand => _loadReportCommand;
        public ICommand ReloadAccountsCommand => _reloadAccountsCommand;

        public async Task LoadAccountsAsync(bool force = false)
        {
            if (IsLoadingAccounts || IsLoading || (_accountsLoaded && !force))
                return;

            IsLoadingAccounts = true;
            AccountsErrorMessage = null;
            try
            {
                var accountTask = _accountingRepository.GetAccountsAsync();
                var typeTask = _accountingRepository.GetAccountTypesAsync();
                await Task.WhenAll(accountTask, typeTask);
                var accounts = (await accountTask).ToList();
                var types = (await typeTask).ToDictionary(t => t.AccountTypeId);
                var options = BuildAccountOptions(accounts, types);
                var selectedId = SelectedAccountId;
                SelectedAccount = null;
                Accounts.Clear();
                foreach (var option in options)
                    Accounts.Add(option);
                SelectedAccountId = selectedId;
                _accountsLoaded = true;
            }
            catch (Exception ex)
            {
                Trace.TraceError("Account inquiry account lookup failed: {0}", ex);
                AccountsErrorMessage = "Unable to load accounts. Select Reload accounts to try again.";
            }
            finally
            {
                IsLoadingAccounts = false;
            }
        }

        private static List<AccountInquiryAccountOption> BuildAccountOptions(
            List<AccountDto> accounts, Dictionary<int, AccountType> types)
        {
            var result = new List<AccountInquiryAccountOption>();
            var visited = new HashSet<int>();
            foreach (var group in accounts.GroupBy(a => a.AccountTypeId)
                .OrderBy(g => types.ContainsKey(g.Key) ? types[g.Key].Code : string.Empty)
                .ThenBy(g => g.Key))
            {
                var typeName = types.ContainsKey(group.Key) ? types[group.Key].Name : "Other accounts";
                result.Add(new AccountInquiryAccountOption(typeName));
                var members = group.OrderBy(a => a.Code, StringComparer.OrdinalIgnoreCase).ThenBy(a => a.Id).ToList();
                var memberIds = new HashSet<int>(members.Select(a => a.Id));
                var children = members.Where(a => a.ParentAccountId.HasValue)
                    .ToLookup(a => a.ParentAccountId.Value);
                // Iterative traversal also handles deep trees, orphaned parents, and cycles safely.
                Action<AccountDto> appendTree = root =>
                {
                    var pending = new Stack<Tuple<AccountDto, int, string>>();
                    pending.Push(Tuple.Create(root, 0, typeName));
                    while (pending.Count > 0)
                    {
                        var item = pending.Pop();
                        var account = item.Item1;
                        if (!visited.Add(account.Id))
                            continue;
                        var path = item.Item3 + " > " + account.Code + " - " + account.Name;
                        result.Add(new AccountInquiryAccountOption(account, item.Item2, path));
                        foreach (var child in children[account.Id].Reverse())
                            pending.Push(Tuple.Create(child, item.Item2 + 1, path));
                    }
                };
                foreach (var root in members.Where(a => !a.ParentAccountId.HasValue || !memberIds.Contains(a.ParentAccountId.Value)))
                    appendTree(root);
                foreach (var remaining in members.Where(a => !visited.Contains(a.Id)))
                    appendTree(remaining);
            }
            return result;
        }

        public async Task LoadReportAsync()
        {
            if (IsLoading || IsLoadingAccounts)
                return;

            ErrorMessage = null;
            Summary = null;
            TransactionLines = new ObservableCollection<AccountInquiryLineDto>();

            if (SelectedAccountId <= 0)
            {
                ErrorMessage = "Select an account to generate the report.";
                return;
            }
            if (FromDate.Date > ToDate.Date)
            {
                ErrorMessage = "From date must be on or before to date.";
                return;
            }

            IsLoading = true;
            try
            {
                var accountId = SelectedAccountId;
                var generalAccountCode = SelectedAccount.GeneralAccountCode;
                var fromDate = FromDate;
                var toDate = ToDate;
                // Await on the UI context so notifications and collection replacement stay on the UI thread.
                var response = await _repository.GetAccountInquiryAsync(accountId, fromDate, toDate);
                if (response == null || response.Summary == null || response.TransactionLines == null)
                    throw new InvalidOperationException("The account inquiry response was incomplete.");

                var lines = new ObservableCollection<AccountInquiryLineDto>(response.TransactionLines);
                _loadedAccountId = accountId;
                _loadedFromDate = fromDate;
                _loadedToDate = toDate;
                ReportGeneralAccountCode = string.IsNullOrWhiteSpace(generalAccountCode) ? "Not assigned" : generalAccountCode;
                Summary = response.Summary;
                TransactionLines = lines;
            }
            catch (AccountNotFoundException ex)
            {
                ErrorMessage = ex.Message;
            }
            catch (Exception ex)
            {
                Trace.TraceError("Account inquiry failed: {0}", ex);
                ErrorMessage = "Unable to generate the account inquiry. Please try again or contact support.";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    /// <summary>Presentation data for a flattened, ordered chart-of-accounts hierarchy.</summary>
    public sealed class AccountInquiryAccountOption
    {
        public int Id { get; }
        public int Depth { get; }
        public bool IsHeader { get; }
        public bool IsGroupHeading { get; }
        public string GeneralAccountCode { get; }
        public string DisplayText { get; }
        public string HierarchyPath { get; }
        public string ToolTip => HierarchyPath + Environment.NewLine + "General account code: " +
            (string.IsNullOrWhiteSpace(GeneralAccountCode) ? "Not assigned" : GeneralAccountCode);

        public AccountInquiryAccountOption(string typeName)
        {
            DisplayText = typeName;
            HierarchyPath = typeName;
            IsGroupHeading = true;
        }

        public AccountInquiryAccountOption(AccountDto account, int depth, string hierarchyPath)
        {
            Id = account.Id;
            Depth = depth;
            IsHeader = account.IsHeader;
            GeneralAccountCode = account.GeneralAccountCode;
            DisplayText = account.Code + " - " + account.Name + (account.IsActive ? string.Empty : " (inactive)");
            HierarchyPath = hierarchyPath;
        }
    }
}
