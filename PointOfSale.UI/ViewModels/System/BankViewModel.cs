using System;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using PointOfSale.Core.Interfaces.Repositories.System;
using PointOfSale.Core.Models.System;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Settings
{
    public class BankViewModel : BaseViewModel
    {
        private readonly IBankRepository _bankRepository;

        public BankViewModel(IBankRepository bankRepository)
        {
            _bankRepository = bankRepository ?? throw new ArgumentNullException(nameof(bankRepository));

            SaveBankCommand = new AsyncRelayCommand(async _ => await SaveBankAsync(), _ => CanSaveBank);
            EditBankCommand = new RelayCommand(_ => SetEditMode(true), _ => SelectedBank != null);
            DeleteBankCommand = new AsyncRelayCommand(async _ => await DeleteBankAsync(), _ => SelectedBank != null);
            NewBankCommand = new RelayCommand(_ => CreateNewBank());
            LoadBankCommand = new AsyncRelayCommand(async _ => await LoadBanksAsync());

            _ = LoadBanksAsync();
        }

        #region Properties

        public ObservableCollection<Bank> Banks { get; } = new ObservableCollection<Bank>();

        private Bank _selectedBank;
        public Bank SelectedBank
        {
            get => _selectedBank;
            set
            {
                if (SetProperty(ref _selectedBank, value))
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

        private string _bankCode;
        public string BankCode
        {
            get => _bankCode;
            set
            {
                SetProperty(ref _bankCode, value);
                ValidateBankCode();
                RaiseCanExecuteChanged();
            }
        }

        private string _bankName;
        public string BankName
        {
            get => _bankName;
            set
            {
                SetProperty(ref _bankName, value);
                ValidateBankName();
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

        public string SaveButtonText => IsEditing ? "Update" : "Save";

        public bool CanSaveBank => !HasErrors && !string.IsNullOrWhiteSpace(BankName);

        #endregion

        #region Commands

        public ICommand SaveBankCommand { get; }
        public ICommand EditBankCommand { get; }
        public ICommand DeleteBankCommand { get; }
        public ICommand NewBankCommand { get; }
        public ICommand LoadBankCommand { get; }

        #endregion

        #region CRUD Methods

        private async Task LoadBanksAsync()
        {
            try
            {
                Banks.Clear();
                var all = await _bankRepository.GetAllAsync();
                foreach (var bank in all)
                    Banks.Add(bank);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load banks: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SaveBankAsync()
        {
            ValidateAll();
            if (HasErrors)
            {
                MessageBox.Show("Please correct the highlighted errors before saving.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (IsEditing && SelectedBank != null)
                {
                    SelectedBank.BankCode = BankCode;
                    SelectedBank.BankName = BankName;
                    SelectedBank.IsActive = IsActive;

                    await _bankRepository.UpdateAsync(SelectedBank);
                    MessageBox.Show("Bank updated successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    var newBank = new Bank
                    {
                        BankCode = BankCode,
                        BankName = BankName,
                        IsActive = IsActive
                    };

                    await _bankRepository.CreateAsync(newBank);
                    MessageBox.Show("Bank created successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                await LoadBanksAsync();
                CreateNewBank();
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving bank: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task DeleteBankAsync()
        {
            if (SelectedBank == null) return;

            var result = MessageBox.Show(
                $"Are you sure you want to delete '{SelectedBank.BankName}'?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _bankRepository.DeleteAsync(SelectedBank.Id);
                await LoadBanksAsync();
                CreateNewBank();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting bank: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Helper Methods

        private void SetEditMode(bool isEditing)
        {
            IsEditing = isEditing;
            if (isEditing && SelectedBank != null)
            {
                Id = SelectedBank.Id;
                BankCode = SelectedBank.BankCode;
                BankName = SelectedBank.BankName;
                IsActive = SelectedBank.IsActive;
            }
        }

        private void CreateNewBank()
        {
            SelectedBank = null;
            Id = 0;
            BankCode = string.Empty;
            BankName = string.Empty;
            IsActive = true;
            IsEditing = false;
            ClearAllErrors();
            RaiseCanExecuteChanged();
        }

        private void RaiseCanExecuteChanged()
        {
            (SaveBankCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (EditBankCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (DeleteBankCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }

        #endregion

        #region Validation

        private void ValidateAll()
        {
            ValidateBankCode();
            ValidateBankName();
        }

        private void ValidateBankCode()
        {
            ClearErrors(nameof(BankCode));
            if (!string.IsNullOrWhiteSpace(BankCode) && !Regex.IsMatch(BankCode, @"^[a-zA-Z0-9\-_]+$"))
                AddError(nameof(BankCode), "Bank code contains invalid characters.");
        }

        private void ValidateBankName()
        {
            ClearErrors(nameof(BankName));
            if (string.IsNullOrWhiteSpace(BankName))
                AddError(nameof(BankName), "Bank name is required.");
            else if (BankName.Length > 100)
                AddError(nameof(BankName), "Bank name cannot exceed 100 characters.");
        }

        #endregion
    }
}
