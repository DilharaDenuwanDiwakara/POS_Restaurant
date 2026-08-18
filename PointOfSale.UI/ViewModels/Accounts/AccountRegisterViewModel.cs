using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces;
using PointOfSale.Core.Interfaces.Services;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Accounts
{
    public class AccountRegisterViewModel : BaseViewModel
    {
        private readonly IAccountingRepository _accountingRepository;
        private readonly IUserSessionService _userSessionService;
        private readonly IDialogService _dialogService;
        private readonly List<AccountDto> _flatAccounts = new List<AccountDto>();

        private AccountType _selectedAccountType;
        private AccountDto _selectedParentAccount;
        private AccountDto _selectedAccount;
        private string _accountCode;
        private string _lockedCodePrefix = string.Empty;
        private string _editableCodePart = "0000";
        private int _editableDigitsCount = 4;
        private string _codePatternHint = "0000";
        private string _accountName;
        private string _generalAccountCode;
        private string _description;
        private bool _isActive = true;
        private bool _isHeader;
        private bool _isEditMode;
        private bool _isOverlayVisible;

        public AccountRegisterViewModel(
            IAccountingRepository accountingRepository,
            IDialogService dialogService,
            IUserSessionService userSessionService)
        {
            _accountingRepository = accountingRepository ?? throw new ArgumentNullException(nameof(accountingRepository));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));

            AccountTypes = new ObservableCollection<AccountType>();
            AccountsList = new ObservableCollection<AccountDto>();
            AvailableParentAccounts = new ObservableCollection<AccountDto>();
            ChartOfAccounts = new ObservableCollection<AccountDto>();

            SaveCommand = new AsyncRelayCommand(async _ => await SaveAsync(), _ => CanSave());
            ClearCommand = new RelayCommand(_ => ClearForm());
            LoadCommand = new AsyncRelayCommand(async _ => await LoadAsync());

            _ = LoadAsync();
        }

        public ObservableCollection<AccountType> AccountTypes { get; }
        public ObservableCollection<AccountDto> AccountsList { get; }
        public ObservableCollection<AccountDto> AvailableParentAccounts { get; }
        public ObservableCollection<AccountDto> ChartOfAccounts { get; }

        public AccountType SelectedAccountType
        {
            get => _selectedAccountType;
            set
            {
                if (SetProperty(ref _selectedAccountType, value))
                {
                    PopulateAvailableParents();
                    if (!IsEditMode)
                    {
                        SelectedParentAccount = AvailableParentAccounts.FirstOrDefault();
                    }

                    RebuildCodeLockAndParts();
                    ValidateAccountCode();
                    RaiseCanExecuteChanged();
                }
            }
        }

        public AccountDto SelectedParentAccount
        {
            get => _selectedParentAccount;
            set
            {
                if (SetProperty(ref _selectedParentAccount, value))
                {
                    RebuildCodeLockAndParts();
                    ValidateAccountCode();
                    RaiseCanExecuteChanged();
                }
            }
        }

        public AccountDto SelectedAccount
        {
            get => _selectedAccount;
            set
            {
                if (SetProperty(ref _selectedAccount, value))
                {
                    LoadSelectedAccountToForm();
                }
            }
        }

        public string AccountCode
        {
            get => _accountCode;
            private set
            {
                if (SetProperty(ref _accountCode, value))
                {
                    ValidateAccountCode();
                    RaiseCanExecuteChanged();
                }
            }
        }

        public string LockedCodePrefix
        {
            get => _lockedCodePrefix;
            private set
            {
                if (SetProperty(ref _lockedCodePrefix, value))
                {
                    OnPropertyChanged(nameof(LockedCodePrefixDisplay));
                }
            }
        }

        public string LockedCodePrefixDisplay => FormatCodePrefixForDisplay(LockedCodePrefix);

        public string EditableCodePart
        {
            get => _editableCodePart;
            set
            {
                var sanitized = SanitizeEditablePart(value, EditableDigitsCount);
                if (SetProperty(ref _editableCodePart, sanitized))
                {
                    UpdateAccountCodeFromParts();
                }
            }
        }

        // Pads with trailing zeros on commit (LostFocus/Save), not on every keystroke.
        public void CommitEditableCodePart()
        {
            EditableCodePart = NormalizeEditablePart(EditableCodePart, EditableDigitsCount);
        }

        public int EditableDigitsCount
        {
            get => _editableDigitsCount;
            private set => SetProperty(ref _editableDigitsCount, value);
        }

        public string CodePatternHint
        {
            get => _codePatternHint;
            private set => SetProperty(ref _codePatternHint, value);
        }

        public string AccountName
        {
            get => _accountName;
            set
            {
                if (SetProperty(ref _accountName, value))
                {
                    ValidateAccountName();
                    RaiseCanExecuteChanged();
                }
            }
        }

        public string GeneralAccountCode
        {
            get => _generalAccountCode;
            set => SetProperty(ref _generalAccountCode, value);
        }

        public string Description
        {
            get => _description;
            set
            {
                if (SetProperty(ref _description, value))
                {
                    ValidateDescription();
                }
            }
        }

        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        public bool IsHeader
        {
            get => _isHeader;
            set => SetProperty(ref _isHeader, value);
        }

        public bool IsEditMode
        {
            get => _isEditMode;
            set
            {
                if (SetProperty(ref _isEditMode, value))
                {
                    OnPropertyChanged(nameof(SaveButtonText));
                }
            }
        }

        public string SaveButtonText => IsEditMode ? "Update" : "Save";

        public bool IsOverlayVisible
        {
            get => _isOverlayVisible;
            set
            {
                _isOverlayVisible = value;
                OnPropertyChanged(nameof(IsOverlayVisible));
            }
        }

        #region Commands
        public ICommand SaveCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand LoadCommand { get; }
        public ICommand OpenAccountTypesCommand => new RelayCommand(ExecuteOpenAccountTypes);
        #endregion

        private async Task LoadAsync()
        {
            try
            {
                var types = await _accountingRepository.GetAccountTypesAsync();
                AccountTypes.Clear();
                foreach (var type in types.Where(t => t.IsActive).OrderBy(t => t.Code))
                {
                    AccountTypes.Add(type);
                }

                _flatAccounts.Clear();
                _flatAccounts.AddRange(await _accountingRepository.GetAccountsAsync());

                AccountsList.Clear();
                foreach (var account in _flatAccounts.OrderBy(a => a.Code))
                {
                    account.AccountTypeName = AccountTypes.FirstOrDefault(t => t.AccountTypeId == account.AccountTypeId)?.Name ?? string.Empty;
                    account.DisplayName = $"{account.Code} - {account.Name}";
                    AccountsList.Add(account);
                }

                PopulateAvailableParents();
                BuildHierarchyTreeWithAccountTypeRoots();
                RebuildCodeLockAndParts();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load accounts: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ExecuteOpenAccountTypes(object parameter)
        {
            try
            {
                IsOverlayVisible = true;

                if (_dialogService == null)
                {
                    MessageBox.Show("Dialog Service is not initialized.");
                    return;
                }

                _dialogService.ShowDialog<AccountTypeRegisterViewModel>(out var accountTypeRegisterViewModel);

                if (accountTypeRegisterViewModel.AccountTypes != null && accountTypeRegisterViewModel.AccountTypes.Count > 0)
                {
                    foreach (var accountType in accountTypeRegisterViewModel.AccountTypes)
                    {
                        if (!AccountTypes.Any(a => a.AccountTypeId == accountType.AccountTypeId))
                        {
                            AccountTypes.Add(accountType);
                        }
                    }
                    SelectedAccountType = accountTypeRegisterViewModel.AccountTypes.Last();
                }
                else
                {
                    LoadSelectedAccountToForm();
                }
            }
            finally
            {
                IsOverlayVisible = false;
            }
        }

        private void BuildHierarchyTreeWithAccountTypeRoots()
        {
            var typedLookup = _flatAccounts
                .Where(a => a.Id > 0)
                .ToDictionary(a => a.Id, a => a);

            foreach (var account in _flatAccounts)
            {
                account.Children.Clear();
            }

            foreach (var account in _flatAccounts)
            {
                if (account.ParentAccountId.HasValue &&
                    typedLookup.TryGetValue(account.ParentAccountId.Value, out var parent))
                {
                    parent.Children.Add(account);
                }
            }

            ChartOfAccounts.Clear();
            foreach (var type in AccountTypes.OrderBy(t => t.Code))
            {
                var typeRoot = new AccountDto
                {
                    Id = -type.AccountTypeId,
                    AccountTypeId = type.AccountTypeId,
                    Code = type.Code,
                    Name = type.Name,
                    DisplayName = $"{type.Code} - {type.Name}",
                    IsActive = true
                };

                var rootsForType = _flatAccounts
                    .Where(a => a.AccountTypeId == type.AccountTypeId && !a.ParentAccountId.HasValue)
                    .OrderBy(a => a.Code)
                    .ToList();

                foreach (var root in rootsForType)
                {
                    typeRoot.Children.Add(root);
                }

                ChartOfAccounts.Add(typeRoot);
            }
        }

        private void PopulateAvailableParents()
        {
            AvailableParentAccounts.Clear();
            AvailableParentAccounts.Add(new AccountDto
            {
                Id = 0,
                DisplayName = "< NO PARENT >"
            });

            if (SelectedAccountType == null)
            {
                return;
            }

            var filtered = _flatAccounts
                .Where(a => a.AccountTypeId == SelectedAccountType.AccountTypeId && a.IsHeader && a.IsActive)
                .OrderBy(a => a.Code)
                .ToList();

            foreach (var account in filtered)
            {
                account.DisplayName = $"{account.Code} - {account.Name}";
                AvailableParentAccounts.Add(account);
            }
        }

        private void LoadSelectedAccountToForm()
        {
            if (SelectedAccount == null)
            {
                return;
            }

            IsEditMode = true;
            AccountName = SelectedAccount.Name;
            GeneralAccountCode = SelectedAccount.GeneralAccountCode;
            Description = SelectedAccount.Description;
            IsActive = SelectedAccount.IsActive;
            IsHeader = SelectedAccount.IsHeader;

            SelectedAccountType = AccountTypes.FirstOrDefault(t => t.AccountTypeId == SelectedAccount.AccountTypeId);
            PopulateAvailableParents();

            SelectedParentAccount = SelectedAccount.ParentAccountId.HasValue
                ? AvailableParentAccounts.FirstOrDefault(a => a.Id == SelectedAccount.ParentAccountId.Value)
                : AvailableParentAccounts.FirstOrDefault();

            ApplyAccountCodeToEditablePart(SelectedAccount.Code);
            UpdateAccountCodeFromParts();
        }

        private async Task SaveAsync()
        {
            CommitEditableCodePart();
            ValidateAll();
            if (HasErrors)
            {
                MessageBox.Show("Please fix validation errors before saving.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var code = AccountCode.Trim();
            var excludeId = IsEditMode && SelectedAccount != null ? (int?)SelectedAccount.Id : null;
            var exists = await _accountingRepository.AccountCodeExistsAsync(code, excludeId);
            if (exists)
            {
                AddError(nameof(AccountCode), "GL No must be unique.");
                MessageBox.Show("GL No must be unique.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!IsEditMode)
            {
                var dto = new AccountDto
                {
                    AccountTypeId = SelectedAccountType.AccountTypeId,
                    ParentAccountId = SelectedParentAccount != null && SelectedParentAccount.Id > 0 ? (int?)SelectedParentAccount.Id : null,
                    Code = code,
                    GeneralAccountCode = GeneralAccountCode?.Trim(),
                    Name = AccountName.Trim(),
                    Description = Description?.Trim(),
                    IsActive = IsActive,
                    IsHeader = IsHeader,
                    CreatedBy = _userSessionService.UserId
                };

                await _accountingRepository.CreateAccountAsync(dto);
                MessageBox.Show("Account saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadAsync();
                ClearForm();
                return;
            }

            SelectedAccount.AccountTypeId = SelectedAccountType.AccountTypeId;
            SelectedAccount.ParentAccountId = SelectedParentAccount != null && SelectedParentAccount.Id > 0 ? (int?)SelectedParentAccount.Id : null;
            SelectedAccount.Code = code;
            SelectedAccount.GeneralAccountCode = GeneralAccountCode?.Trim();
            SelectedAccount.Name = AccountName.Trim();
            SelectedAccount.Description = Description?.Trim();
            SelectedAccount.IsActive = IsActive;
            SelectedAccount.IsHeader = IsHeader;
            SelectedAccount.AccountTypeName = SelectedAccountType.Name;
            SelectedAccount.UpdatedBy = _userSessionService.UserId;

            await _accountingRepository.UpdateAccountAsync(SelectedAccount);
            MessageBox.Show("Account updated successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            await LoadAsync();
            ClearForm();
        }

        private void ValidateAll()
        {
            ValidateAccountType();
            ValidateAccountCode();
            ValidateAccountName();
            ValidateDescription();
        }

        private void ValidateAccountType()
        {
            ClearErrors(nameof(SelectedAccountType));
            if (SelectedAccountType == null)
            {
                AddError(nameof(SelectedAccountType), "Account Type is required.");
            }
        }

        private void ValidateAccountCode()
        {
            ClearErrors(nameof(AccountCode));
            if (string.IsNullOrWhiteSpace(AccountCode))
            {
                AddError(nameof(AccountCode), "GL No is required.");
                return;
            }

            var code = AccountCode.Trim();
            if (!Regex.IsMatch(code, @"^\d+$"))
            {
                AddError(nameof(AccountCode), "GL No must be numeric.");
                return;
            }

            if (code.Length != 4)
            {
                AddError(nameof(AccountCode), "GL No must be exactly 4 digits.");
                return;
            }

            if (SelectedAccountType != null)
            {
                var rootDigit = GetRootDigit(SelectedAccountType).ToString();
                if (!code.StartsWith(rootDigit, StringComparison.Ordinal))
                {
                    AddError(nameof(AccountCode), $"For {SelectedAccountType.Name}, GL No must start with '{rootDigit}'.");
                }
            }

            if (SelectedParentAccount != null && SelectedParentAccount.Id > 0)
            {
                var prefix = DeriveParentPrefix(SelectedParentAccount.Code);
                if (!string.IsNullOrWhiteSpace(prefix) && !code.StartsWith(prefix, StringComparison.Ordinal))
                {
                    AddError(nameof(AccountCode), $"GL No must logically follow parent block '{prefix}...'.");
                }
            }
        }

        private static readonly Regex AccountNameAllowedCharsRegex = new Regex(@"^[a-zA-Z0-9\s'/:&-]+$", RegexOptions.Compiled);

        private void ValidateAccountName()
        {
            ClearErrors(nameof(AccountName));
            if (string.IsNullOrWhiteSpace(AccountName))
            {
                AddError(nameof(AccountName), "GL Name is required.");
                return;
            }

            if (!AccountNameAllowedCharsRegex.IsMatch(AccountName))
            {
                AddError(nameof(AccountName), "GL Name can only contain letters, numbers, spaces, and ' / : & -");
            }
        }

        private void ValidateDescription()
        {
            ClearErrors(nameof(Description));
            if (!string.IsNullOrWhiteSpace(Description) && Description.Length > 500)
            {
                AddError(nameof(Description), "Description cannot exceed 500 characters.");
            }
        }

        private bool CanSave()
        {
            return !HasErrors &&
                   SelectedAccountType != null &&
                   !string.IsNullOrWhiteSpace(AccountCode) &&
                   AccountCode.Trim().Length == 4 &&
                   !string.IsNullOrWhiteSpace(AccountName);
        }

        private int GetRootDigit(AccountType type)
        {
            var code = (type.Code ?? string.Empty).Trim().ToUpperInvariant();
            var name = (type.Name ?? string.Empty).Trim().ToUpperInvariant();

            if (code.StartsWith("AST") || name.StartsWith("ASSET")) return 1;
            if (code.StartsWith("LIA") || name.StartsWith("LIABIL")) return 2;
            if (code.StartsWith("EQU") || name.StartsWith("EQUITY")) return 3;
            if (code.StartsWith("REV") || name.StartsWith("REVEN")) return 4;
            if (code.StartsWith("EXP") || name.StartsWith("EXPENS")) return 5;

            throw new InvalidOperationException($"Unsupported account type mapping: {type.Code} - {type.Name}");
        }

        private void RebuildCodeLockAndParts()
        {
            var newPrefix = GetCurrentLockedPrefix();
            var newDigitsCount = Math.Max(0, 4 - newPrefix.Length);

            var currentCode = AccountCode ?? string.Empty;
            string suffix;
            if (!string.IsNullOrWhiteSpace(currentCode) &&
                currentCode.Length == 4 &&
                currentCode.StartsWith(newPrefix, StringComparison.Ordinal))
            {
                suffix = currentCode.Substring(newPrefix.Length);
            }
            else
            {
                suffix = EditableCodePart;
            }

            LockedCodePrefix = newPrefix;
            EditableDigitsCount = newDigitsCount;
            CodePatternHint = BuildCodePatternHint(newPrefix, newDigitsCount);
            EditableCodePart = NormalizeEditablePart(suffix, EditableDigitsCount);
            UpdateAccountCodeFromParts();
        }

        private void ApplyAccountCodeToEditablePart(string fullCode)
        {
            var normalizedCode = new string((fullCode ?? string.Empty).Where(char.IsDigit).ToArray());
            if (normalizedCode.Length != 4)
            {
                EditableCodePart = NormalizeEditablePart(string.Empty, EditableDigitsCount);
                return;
            }

            if (!normalizedCode.StartsWith(LockedCodePrefix, StringComparison.Ordinal))
            {
                EditableCodePart = NormalizeEditablePart(string.Empty, EditableDigitsCount);
                return;
            }

            var editablePart = normalizedCode.Substring(LockedCodePrefix.Length);
            EditableCodePart = NormalizeEditablePart(editablePart, EditableDigitsCount);
        }

        private void UpdateAccountCodeFromParts()
        {
            var paddedSuffix = NormalizeEditablePart(EditableCodePart, EditableDigitsCount);
            AccountCode = $"{LockedCodePrefix}{paddedSuffix}";
        }

        private string GetCurrentLockedPrefix()
        {
            if (SelectedParentAccount != null && SelectedParentAccount.Id > 0)
            {
                var parentPrefix = DeriveParentPrefix(SelectedParentAccount.Code);
                if (!string.IsNullOrWhiteSpace(parentPrefix))
                {
                    return parentPrefix;
                }
            }

            if (SelectedAccountType == null)
            {
                return string.Empty;
            }

            return GetRootDigit(SelectedAccountType).ToString();
        }

        private static string DeriveParentPrefix(string parentCode)
        {
            var normalized = new string((parentCode ?? string.Empty).Where(char.IsDigit).ToArray());
            if (normalized.Length != 4)
            {
                return string.Empty;
            }

            var trimmed = normalized.TrimEnd('0');
            return string.IsNullOrWhiteSpace(trimmed)
                ? normalized.Substring(0, 1)
                : trimmed;
        }

        private static string SanitizeEditablePart(string value, int requiredLength)
        {
            if (requiredLength <= 0)
            {
                return string.Empty;
            }

            var digitsOnly = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
            return digitsOnly.Length > requiredLength
                ? digitsOnly.Substring(0, requiredLength)
                : digitsOnly;
        }

        private static string NormalizeEditablePart(string value, int requiredLength)
        {
            return SanitizeEditablePart(value, requiredLength).PadRight(requiredLength, '0');
        }

        private static string BuildCodePatternHint(string prefix, int editableDigitsCount)
        {
            var editableMask = new string('0', Math.Max(0, editableDigitsCount));
            var displayPrefix = FormatCodePrefixForDisplay(prefix);
            return string.IsNullOrWhiteSpace(displayPrefix)
                ? editableMask
                : $"{displayPrefix}{editableMask}";
        }

        private static string FormatCodePrefixForDisplay(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                return string.Empty;
            }

            return prefix.Length == 1
                ? $"{prefix}-"
                : $"{prefix[0]}-{prefix.Substring(1)}";
        }

        private void RaiseCanExecuteChanged()
        {
            (SaveCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }

        private void ClearForm()
        {
            SelectedAccount = null;
            IsEditMode = false;
            AccountName = string.Empty;
            GeneralAccountCode = string.Empty;
            Description = string.Empty;
            IsActive = true;
            IsHeader = false;
            EditableDigitsCount = 4;
            CodePatternHint = "0000";
            EditableCodePart = "0000";
            AccountCode = "0000";
            ClearAllErrors();
            RaiseCanExecuteChanged();
        }
    }
}
