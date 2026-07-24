using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Accounts
{
    public class AccountTypeRegisterViewModel : BaseViewModel
    {
        private readonly IAccountingRepository _accountingRepository;
        private AccountType _selectedAccountType;
        private string _code;
        private string _name;
        private string _normalBalance = "D";
        private bool _isActive = true;
        private bool _isEditing;

        public AccountTypeRegisterViewModel(IAccountingRepository accountingRepository)
        {
            _accountingRepository = accountingRepository ?? throw new ArgumentNullException(nameof(accountingRepository));
            AccountTypes = new ObservableCollection<AccountType>();

            SaveCommand = new AsyncRelayCommand(async _ => await SaveAsync());
            ClearCommand = new RelayCommand(_ => ClearForm());
            LoadCommand = new AsyncRelayCommand(async _ => await LoadAsync());
            DeactivateCommand = new AsyncRelayCommand(async _ => await DeactivateAsync(), _ => SelectedAccountType != null);

            _ = LoadAsync();
        }

        public ObservableCollection<AccountType> AccountTypes { get; }

        public AccountType SelectedAccountType
        {
            get => _selectedAccountType;
            set
            {
                if (SetProperty(ref _selectedAccountType, value))
                {
                    if (value != null)
                    {
                        Code = value.Code;
                        Name = value.Name;
                        NormalBalance = value.NormalBalance;
                        IsActive = value.IsActive;
                        IsEditing = true;
                    }

                    (DeactivateCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public string Code
        {
            get => _code;
            set => SetProperty(ref _code, value);
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string NormalBalance
        {
            get => _normalBalance;
            set => SetProperty(ref _normalBalance, value);
        }

        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        public bool IsEditing
        {
            get => _isEditing;
            set
            {
                if (SetProperty(ref _isEditing, value))
                {
                    OnPropertyChanged(nameof(SaveButtonText));
                }
            }
        }

        public string SaveButtonText => IsEditing ? "Update" : "Save";

        public ICommand SaveCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand LoadCommand { get; }
        public ICommand DeactivateCommand { get; }

        private async Task LoadAsync()
        {
            try
            {
                var types = await _accountingRepository.GetAccountTypesAsync();
                AccountTypes.Clear();
                foreach (var type in types.OrderBy(t => t.Code))
                {
                    AccountTypes.Add(type);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load account types: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(Code) || string.IsNullOrWhiteSpace(Name) ||
                (NormalBalance != "D" && NormalBalance != "C"))
            {
                MessageBox.Show("Code, Name and Normal Balance (D/C) are required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (IsEditing && SelectedAccountType != null)
                {
                    SelectedAccountType.Code = Code.Trim().ToUpperInvariant();
                    SelectedAccountType.Name = Name.Trim();
                    SelectedAccountType.NormalBalance = NormalBalance.Trim().ToUpperInvariant();
                    SelectedAccountType.IsActive = IsActive;

                    await _accountingRepository.UpdateAccountTypeAsync(SelectedAccountType);
                    MessageBox.Show("Account type updated successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    await _accountingRepository.CreateAccountTypeAsync(new AccountType
                    {
                        Code = Code.Trim().ToUpperInvariant(),
                        Name = Name.Trim(),
                        NormalBalance = NormalBalance.Trim().ToUpperInvariant(),
                        IsActive = IsActive
                    });
                    MessageBox.Show("Account type created successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                await LoadAsync();
                ClearForm();
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save account type: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task DeactivateAsync()
        {
            if (SelectedAccountType == null)
            {
                return;
            }

            try
            {
                await _accountingRepository.DeactivateAccountTypeAsync(SelectedAccountType.AccountTypeId);
                await LoadAsync();
                ClearForm();
                MessageBox.Show("Account type deactivated successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to deactivate account type: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearForm()
        {
            SelectedAccountType = null;
            Code = string.Empty;
            Name = string.Empty;
            NormalBalance = "D";
            IsActive = true;
            IsEditing = false;
        }
    }
}
