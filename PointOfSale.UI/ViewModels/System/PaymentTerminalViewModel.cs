using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces;
using PointOfSale.Core.Interfaces.Repositories.Sales;
using PointOfSale.Core.Interfaces.Repositories.System;
using PointOfSale.Core.Models.Sales;
using PointOfSale.Core.Models.System;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Settings
{
    public class PaymentTerminalViewModel : BaseViewModel
    {
        private readonly IPaymentTerminalRepository _terminalRepository;
        private readonly IAccountingRepository _accountingRepository;
        private readonly IBankRepository _bankRepository;
        private readonly IBranchRepository _branchRepository;
        private readonly IUserSessionService _userSessionService;

        public PaymentTerminalViewModel(
            IPaymentTerminalRepository terminalRepository,
            IAccountingRepository accountingRepository,
            IBankRepository bankRepository,
            IBranchRepository branchRepository,
            IUserSessionService userSessionService)
        {
            _terminalRepository = terminalRepository ?? throw new ArgumentNullException(nameof(terminalRepository));
            _accountingRepository = accountingRepository ?? throw new ArgumentNullException(nameof(accountingRepository));
            _bankRepository = bankRepository ?? throw new ArgumentNullException(nameof(bankRepository));
            _branchRepository = branchRepository ?? throw new ArgumentNullException(nameof(branchRepository));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));

            SaveCommand = new AsyncRelayCommand(async _ => await SaveAsync(), _ => CanSave);
            EditCommand = new RelayCommand(_ => SetEditMode(true), _ => SelectedTerminal != null);
            DeleteCommand = new AsyncRelayCommand(async _ => await DeleteAsync(), _ => SelectedTerminal != null);
            NewCommand = new RelayCommand(_ => ResetForm());
            LoadCommand = new AsyncRelayCommand(async _ => await LoadTerminalsAsync());

            _ = InitialiseAsync();
        }

        #region Observable Collections

        public ObservableCollection<PaymentTerminal> Terminals { get; } = new ObservableCollection<PaymentTerminal>();
        public ObservableCollection<Branch> Branches { get; } = new ObservableCollection<Branch>();
        public ObservableCollection<Bank> Banks { get; } = new ObservableCollection<Bank>();
        public ObservableCollection<AccountDto> LinkedAccounts { get; } = new ObservableCollection<AccountDto>();

        #endregion

        #region Form Properties

        private PaymentTerminal _selectedTerminal;
        public PaymentTerminal SelectedTerminal
        {
            get => _selectedTerminal;
            set
            {
                if (SetProperty(ref _selectedTerminal, value))
                {
                    SetEditMode(false);
                    RaiseCanExecuteChanged();
                }
            }
        }

        private int _id;
        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        private string _terminalName;
        public string TerminalName
        {
            get => _terminalName;
            set
            {
                SetProperty(ref _terminalName, value);
                ValidateTerminalName();
                RaiseCanExecuteChanged();
            }
        }

        private string _terminalId;
        public string TerminalId
        {
            get => _terminalId;
            set
            {
                SetProperty(ref _terminalId, value);
                ValidateTerminalId();
                RaiseCanExecuteChanged();
            }
        }

        private Branch _selectedBranch;
        public Branch SelectedBranch
        {
            get => _selectedBranch;
            set
            {
                SetProperty(ref _selectedBranch, value);
                ValidateBranch();
                RaiseCanExecuteChanged();
            }
        }

        private AccountDto _selectedLinkedAccount;
        public AccountDto SelectedLinkedAccount
        {
            get => _selectedLinkedAccount;
            set
            {
                SetProperty(ref _selectedLinkedAccount, value);
                ValidateLinkedAccount();
                RaiseCanExecuteChanged();
            }
        }

        private Bank _selectedProviderBank;
        public Bank SelectedProviderBank
        {
            get => _selectedProviderBank;
            set
            {
                SetProperty(ref _selectedProviderBank, value);
                ValidateProviderBank();
                RaiseCanExecuteChanged();
            }
        }

        private bool _isActive = true;
        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        private bool _isEditing;
        public bool IsEditing
        {
            get => _isEditing;
            set
            {
                SetProperty(ref _isEditing, value);
                OnPropertyChanged(nameof(SaveButtonText));
            }
        }

        public string SaveButtonText => IsEditing ? "Update Terminal" : "Save Terminal";

        public bool CanSave =>
            !HasErrors &&
            !string.IsNullOrWhiteSpace(TerminalName) &&
            !string.IsNullOrWhiteSpace(TerminalId) &&
            SelectedBranch != null &&
            SelectedProviderBank != null && SelectedProviderBank.Id > 0 &&
            SelectedLinkedAccount != null && SelectedLinkedAccount.Id > 0;

        #endregion

        #region Commands

        public ICommand SaveCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand NewCommand { get; }
        public ICommand LoadCommand { get; }

        #endregion

        #region Load Methods

        private async Task InitialiseAsync()
        {
            await Task.WhenAll(
                LoadBranchesAsync(),
                LoadBankAccountsAsync(),
                LoadBanksAsync(),
                LoadTerminalsAsync());
        }

        private async Task LoadBranchesAsync()
        {
            try
            {
                Branches.Clear();
                var all = await _branchRepository.GetAllAsync();
                foreach (var b in all.Where(b => b.IsActive))
                    Branches.Add(b);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load branches: {ex.Message}";
            }
        }

        private async Task LoadBankAccountsAsync()
        {
            try
            {
                LinkedAccounts.Clear();
                var all = await _accountingRepository.GetAccountsAsync();
                foreach (var a in all.Where(a => (a.AccountTypeId == 1 || a.AccountTypeId == 2) && !a.IsHeader && a.IsActive))
                    LinkedAccounts.Add(a);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load linked accounts: {ex.Message}";
            }
        }

        private async Task LoadBanksAsync()
        {
            try
            {
                Banks.Clear();
                var all = await _bankRepository.GetAllAsync();
                foreach (var b in all.Where(b => b.IsActive))
                    Banks.Add(b);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load banks: {ex.Message}";
            }
        }

        private async Task LoadTerminalsAsync()
        {
            try
            {
                Terminals.Clear();
                var all = await _terminalRepository.GetAllAsync();
                foreach (var t in all)
                    Terminals.Add(t);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load payment terminals: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region CRUD Methods

        private async Task SaveAsync()
        {
            ValidateAll();
            if (HasErrors)
            {
                MessageBox.Show("Please correct the highlighted errors before saving.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (IsEditing && SelectedTerminal != null)
                {
                    SelectedTerminal.TerminalName = TerminalName;
                    SelectedTerminal.TerminalId = TerminalId;
                    SelectedTerminal.BranchId = SelectedBranch.Id;
                    SelectedTerminal.LinkedAccountId = SelectedLinkedAccount?.Id;
                    SelectedTerminal.ProviderBankId = SelectedProviderBank?.Id;
                    SelectedTerminal.IsActive = IsActive;
                    SelectedTerminal.UpdatedBy = _userSessionService.UserId;

                    await _terminalRepository.UpdateAsync(SelectedTerminal);
                    MessageBox.Show("Payment terminal updated successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    var newTerminal = new PaymentTerminal
                    {
                        TerminalName = TerminalName,
                        TerminalId = TerminalId,
                        BranchId = SelectedBranch.Id,
                        LinkedAccountId = SelectedLinkedAccount?.Id,
                        ProviderBankId = SelectedProviderBank?.Id,
                        IsActive = IsActive,
                        CreatedBy = _userSessionService.UserId
                    };

                    await _terminalRepository.CreateAsync(newTerminal);
                    MessageBox.Show("Payment terminal created successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                await LoadTerminalsAsync();
                ResetForm();
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving payment terminal: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task DeleteAsync()
        {
            if (SelectedTerminal == null) return;

            var result = MessageBox.Show(
                $"Are you sure you want to delete terminal '{SelectedTerminal.TerminalName}'?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _terminalRepository.DeleteAsync(SelectedTerminal.Id);
                await LoadTerminalsAsync();
                ResetForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting terminal: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Helper Methods

        private void SetEditMode(bool isEditing)
        {
            IsEditing = isEditing;
            if (!isEditing || SelectedTerminal == null) return;

            Id = SelectedTerminal.Id;
            TerminalName = SelectedTerminal.TerminalName;
            TerminalId = SelectedTerminal.TerminalId;
            IsActive = SelectedTerminal.IsActive;
            SelectedBranch = Branches.FirstOrDefault(b => b.Id == SelectedTerminal.BranchId);
            SelectedLinkedAccount = SelectedTerminal.LinkedAccountId.HasValue
                ? LinkedAccounts.FirstOrDefault(a => a.Id == SelectedTerminal.LinkedAccountId.Value)
                : null;
            SelectedProviderBank = SelectedTerminal.ProviderBankId.HasValue
                ? Banks.FirstOrDefault(b => b.Id == SelectedTerminal.ProviderBankId.Value)
                : null;
        }

        private void ResetForm()
        {
            SelectedTerminal = null;
            Id = 0;
            TerminalName = string.Empty;
            TerminalId = string.Empty;
            SelectedBranch = null;
            SelectedLinkedAccount = null;
            SelectedProviderBank = null;
            IsActive = true;
            IsEditing = false;
            ClearAllErrors();
            RaiseCanExecuteChanged();
        }

        private void RaiseCanExecuteChanged()
        {
            (SaveCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (EditCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (DeleteCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }

        #endregion

        #region Validation
        private void ValidateAll()
        {
            ValidateTerminalName();
            ValidateTerminalId();
            ValidateBranch();
            ValidateProviderBank();
            ValidateLinkedAccount();
        }
        private void ValidateTerminalName()
        {
            ClearErrors(nameof(TerminalName));
            if (string.IsNullOrWhiteSpace(TerminalName))
                AddError(nameof(TerminalName), "Terminal name is required.");
            else if (TerminalName.Length > 50)
                AddError(nameof(TerminalName), "Terminal name cannot exceed 50 characters.");
            else if (!Regex.IsMatch(TerminalName, @"^[a-zA-Z0-9\s\-_]+$"))
                AddError(nameof(TerminalName), "Terminal name contains invalid characters.");
        }
        private void ValidateTerminalId()
        {
            ClearErrors(nameof(TerminalId));
            if (string.IsNullOrWhiteSpace(TerminalId))
                AddError(nameof(TerminalId), "Terminal ID (TID) is required.");
            else if (TerminalId.Length > 20)
                AddError(nameof(TerminalId), "Terminal ID cannot exceed 20 characters.");
            else if (!Regex.IsMatch(TerminalId, @"^[a-zA-Z0-9]+$"))
                AddError(nameof(TerminalId), "Terminal ID contains invalid characters.");
        }
        private void ValidateBranch()
        {
            ClearErrors(nameof(SelectedBranch));
            if (SelectedBranch == null)
                AddError(nameof(SelectedBranch), "Branch is required.");
        }
        private void ValidateProviderBank()
        {
            ClearErrors(nameof(SelectedProviderBank));
            if (SelectedProviderBank == null || SelectedProviderBank.Id <= 0)
                AddError(nameof(SelectedProviderBank), "Provider bank is required.");
        }
        private void ValidateLinkedAccount()
        {
            ClearErrors(nameof(SelectedLinkedAccount));
            if (SelectedLinkedAccount == null || SelectedLinkedAccount.Id <= 0)
                AddError(nameof(SelectedLinkedAccount), "Linked account is required.");
        }
        #endregion
    }
}
