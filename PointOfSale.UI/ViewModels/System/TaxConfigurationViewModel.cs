using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
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
    public class TaxConfigurationViewModel : BaseViewModel
    {
        private readonly ITaxConfigurationRepository _taxConfigurationRepository;
        private readonly IAccountingRepository _accountingRepository;
        private readonly IUserSessionService _userSessionService;

        public TaxConfigurationViewModel(
            ITaxConfigurationRepository taxConfigurationRepository,
            IAccountingRepository accountingRepository,
            IUserSessionService userSessionService)
        {
            _taxConfigurationRepository = taxConfigurationRepository ?? throw new ArgumentNullException(nameof(taxConfigurationRepository));
            _accountingRepository = accountingRepository ?? throw new ArgumentNullException(nameof(accountingRepository));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));

            SaveTaxConfigurationCommand = new AsyncRelayCommand(async _ => await SaveTaxConfigurationAsync(), _ => CanSaveTaxConfiguration);
            LoadTaxConfigurationCommand = new AsyncRelayCommand(async _ => await LoadTaxConfigurationAsync());
            EditTaxConfigurationCommand = new RelayCommand(_ => SetEditMode(true), _ => SelectedTaxConfiguration != null);
            NewTaxConfigurationCommand = new RelayCommand(_ => CreateNewTaxConfiguration());

            _ = LoadInitialDataAsync();
        }

        #region Properties
        private int _currentUserId => _userSessionService.CurrentUser.UserId;

        public ObservableCollection<TaxConfiguration> TaxConfigurations { get; } = new ObservableCollection<TaxConfiguration>();

        public ObservableCollection<AccountDto> TaxAccounts { get; }
            = new ObservableCollection<AccountDto>();

        private TaxConfiguration _selectedTaxConfiguration;
        public TaxConfiguration SelectedTaxConfiguration
        {
            get => _selectedTaxConfiguration;
            set
            {
                if (SetProperty(ref _selectedTaxConfiguration, value))
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

        private string _taxCode;
        public string TaxCode
        {
            get => _taxCode;
            set
            {
                SetProperty(ref _taxCode, value);
                ValidateTaxCode();
                RaiseCanExecuteChanged();
            }
        }

        private string _taxName;
        public string TaxName
        {
            get => _taxName;
            set
            {
                SetProperty(ref _taxName, value);
                ValidateTaxName();
                RaiseCanExecuteChanged();
            }
        }

        private string _rate;
        public string Rate
        {
            get => _rate;
            set
            {
                SetProperty(ref _rate, value);
                ValidateRate();
                RaiseCanExecuteChanged();
            }
        }

        private int _calculationOrder = 1;
        public int CalculationOrder
        {
            get => _calculationOrder;
            set
            {
                SetProperty(ref _calculationOrder, value);
                ValidateCalculationOrder();
                RaiseCanExecuteChanged();
            }
        }

        private bool _isActive = true;
        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        private bool _isInclusive;
        public bool IsInclusive
        {
            get => _isInclusive;
            set => SetProperty(ref _isInclusive, value);
        }

        private DateTime _effectiveDate = DateTime.Today;
        public DateTime EffectiveDate
        {
            get => _effectiveDate;
            set
            {
                SetProperty(ref _effectiveDate, value);
                RaiseCanExecuteChanged();
            }
        }

        private string _accountCode;
        public string AccountCode
        {
            get => _accountCode;
            set
            {
                SetProperty(ref _accountCode, value);

                RaiseCanExecuteChanged();
            }
        }

        public bool _isEditing;
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
        #endregion

        #region Commands
        public ICommand SaveTaxConfigurationCommand { get; }
        public ICommand LoadTaxConfigurationCommand { get; }
        public ICommand EditTaxConfigurationCommand { get; }
        public ICommand NewTaxConfigurationCommand { get; }
        #endregion

        #region Methods
        private async Task LoadInitialDataAsync()
        {
            await LoadTaxAccountsAsync();
            await LoadTaxConfigurationAsync();
        }
        private async Task LoadTaxAccountsAsync()
        {
            try
            {
                var accounts = await _accountingRepository.GetAccountsAsync();

                TaxAccounts.Clear();
                foreach (var account in accounts.Where(a => a.AccountTypeId == 2 && !a.IsHeader && a.IsActive))
                {
                    TaxAccounts.Add(account);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load liability accounts: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async Task LoadTaxConfigurationAsync()
        {
            try
            {
                TaxConfigurations.Clear();
                var allItems = await _taxConfigurationRepository.GetAllAsync();
                foreach (var item in allItems)
                {
                    TaxConfigurations.Add(item);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load tax configurations: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        public bool CanSaveTaxConfiguration => !HasErrors
            && !string.IsNullOrWhiteSpace(TaxCode)
            && !string.IsNullOrWhiteSpace(TaxName)
            && !string.IsNullOrWhiteSpace(AccountCode);
        private async Task SaveTaxConfigurationAsync()
        {
            ValidateAll();
            if (HasErrors)
            {
                MessageBox.Show("Please correct the highlighted errors before saving.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (IsEditing && SelectedTaxConfiguration != null)
                {
                    SelectedTaxConfiguration.TaxCode = TaxCode;
                    SelectedTaxConfiguration.TaxName = TaxName;
                    SelectedTaxConfiguration.Rate = Convert.ToDecimal(Rate);
                    SelectedTaxConfiguration.CalculationOrder = CalculationOrder;
                    SelectedTaxConfiguration.IsActive = IsActive;
                    SelectedTaxConfiguration.IsInclusive = IsInclusive;
                    SelectedTaxConfiguration.EffectiveDate = EffectiveDate;
                    SelectedTaxConfiguration.AccountCode = AccountCode;
                    SelectedTaxConfiguration.LastModifiedBy = _currentUserId;

                    await _taxConfigurationRepository.UpdateAsync(SelectedTaxConfiguration);
                    MessageBox.Show("Tax configuration updated successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    var newItem = new TaxConfiguration
                    {
                        TaxCode = TaxCode,
                        TaxName = TaxName,
                        Rate = Convert.ToDecimal(Rate),
                        CalculationOrder = CalculationOrder,
                        IsActive = IsActive,
                        IsInclusive = IsInclusive,
                        EffectiveDate = EffectiveDate,
                        AccountCode = AccountCode,
                        LastModifiedBy = _currentUserId
                    };

                    await _taxConfigurationRepository.CreateAsync(newItem);
                    MessageBox.Show("Tax configuration created successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                await LoadTaxConfigurationAsync();
                CreateNewTaxConfiguration();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving tax configuration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Helper Methods
        private void SetEditMode(bool isEditing)
        {
            IsEditing = isEditing;
            if (isEditing && SelectedTaxConfiguration != null)
            {
                Id = SelectedTaxConfiguration.Id;
                TaxCode = SelectedTaxConfiguration.TaxCode;
                TaxName = SelectedTaxConfiguration.TaxName;
                Rate = SelectedTaxConfiguration.Rate.ToString();
                CalculationOrder = SelectedTaxConfiguration.CalculationOrder;
                IsActive = SelectedTaxConfiguration.IsActive;
                IsInclusive = SelectedTaxConfiguration.IsInclusive;
                EffectiveDate = SelectedTaxConfiguration.EffectiveDate;
                AccountCode = SelectedTaxConfiguration.AccountCode;
            }
        }
        private void CreateNewTaxConfiguration()
        {
            SelectedTaxConfiguration = null;
            Id = 0;
            TaxCode = string.Empty;
            TaxName = string.Empty;
            Rate = "0.00";
            CalculationOrder = 1;
            IsActive = true;
            IsInclusive = false;
            EffectiveDate = DateTime.Today;
            AccountCode = null;
            IsEditing = false;
            ClearAllErrors();
            RaiseCanExecuteChanged();
        }
        private void RaiseCanExecuteChanged()
        {
            (SaveTaxConfigurationCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (EditTaxConfigurationCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
        #endregion

        #region Validation
        private void ValidateAll()
        {
            ValidateTaxCode();
            ValidateTaxName();
            ValidateRate();
            ValidateCalculationOrder();
        }
        private void ValidateTaxCode()
        {
            ClearErrors(nameof(TaxCode));
            if (string.IsNullOrWhiteSpace(TaxCode))
                AddError(nameof(TaxCode), "Tax code is required.");
            else if (!Regex.IsMatch(TaxCode, @"^[a-zA-Z0-9\-_]+$"))
                AddError(nameof(TaxCode), "Tax code contains invalid characters.");
        }
        private void ValidateTaxName()
        {
            ClearErrors(nameof(TaxName));
            if (string.IsNullOrWhiteSpace(TaxName))
                AddError(nameof(TaxName), "Tax name is required.");
        }
        private void ValidateRate()
        {
            ClearErrors(nameof(Rate));

            if (string.IsNullOrWhiteSpace(Rate))
            {
                AddError(nameof(Rate), "Rate is required.");
                return;
            }

            decimal validRate;
            if (!decimal.TryParse(Rate, out validRate))
            {
                AddError(nameof(Rate), "Rate must be a valid number.");
                return;
            }

            if (validRate < 0 || validRate > 100)
                AddError(nameof(Rate), "Rate must be between 0 and 100.");
        }
        private void ValidateCalculationOrder()
        {
            ClearErrors(nameof(CalculationOrder));
            if (CalculationOrder <= 0)
                AddError(nameof(CalculationOrder), "Calculation order must be greater than zero.");
        }
        #endregion
    }
}
