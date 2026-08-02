using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using PointOfSale.Core.Common;
using PointOfSale.Core.Interfaces.Repositories.System;
using PointOfSale.Core.Models.System;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Settings
{
    public class CompanySettingsViewModel : BaseViewModel
    {
        private readonly ICompanyRepository _companyRepository;
        private readonly IUserSessionService _userSessionService;
        private bool _isLoadingCompany;

        public CompanySettingsViewModel(ICompanyRepository companyRepository, IUserSessionService userSessionService)
        {
            _companyRepository = companyRepository ?? throw new ArgumentNullException(nameof(companyRepository));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));

            SaveCompanyCommand = new AsyncRelayCommand(async _ => await SaveCompanyAsync(), _ => CanSaveCompany);
            LoadCompanyCommand = new AsyncRelayCommand(async _ => await LoadCompanyAsync());
            NewCompanyCommand = new RelayCommand(_ => CreateNewCompany());
            UploadLogoCommand = new RelayCommand(_ => BrowseLogo());

            _ = LoadCompanyAsync();
        }

        private int _companyId;
        public int CompanyId
        {
            get => _companyId;
            set => SetProperty(ref _companyId, value);
        }

        private string _tradingName;
        public string TradingName
        {
            get => _tradingName;
            set
            {
                if (SetProperty(ref _tradingName, value))
                {
                    if (!_isLoadingCompany)
                    {
                        ValidateTradingName();
                    }

                    RaiseCanExecuteChanged();
                }
            }
        }

        private string _legalName;
        public string LegalName
        {
            get => _legalName;
            set => SetProperty(ref _legalName, value);
        }

        private string _businessRegistrationNumber;
        public string BusinessRegistrationNumber
        {
            get => _businessRegistrationNumber;
            set
            {
                if (SetProperty(ref _businessRegistrationNumber, value))
                {
                    if (!_isLoadingCompany)
                    {
                        ValidateBusinessRegistrationNumber();
                    }

                    RaiseCanExecuteChanged();
                }
            }
        }

        private string _taxRegistrationNumber;
        public string TaxRegistrationNumber
        {
            get => _taxRegistrationNumber;
            set => SetProperty(ref _taxRegistrationNumber, value);
        }

        private string _addressLine1;
        public string AddressLine1
        {
            get => _addressLine1;
            set
            {
                if (SetProperty(ref _addressLine1, value))
                {
                    if (!_isLoadingCompany)
                    {
                        ValidateAddressLine1();
                    }

                    RaiseCanExecuteChanged();
                }
            }
        }

        private string _addressLine2;
        public string AddressLine2
        {
            get => _addressLine2;
            set => SetProperty(ref _addressLine2, value);
        }

        private string _contactNumber;
        public string ContactNumber
        {
            get => _contactNumber;
            set
            {
                if (SetProperty(ref _contactNumber, value))
                {
                    if (!_isLoadingCompany)
                    {
                        ValidateContactNumber();
                    }

                    RaiseCanExecuteChanged();
                }
            }
        }

        private string _email;
        public string Email
        {
            get => _email;
            set
            {
                if (SetProperty(ref _email, value))
                {
                    if (!_isLoadingCompany)
                    {
                        ValidateEmail();
                    }

                    RaiseCanExecuteChanged();
                }
            }
        }

        private string _website;
        public string Website
        {
            get => _website;
            set => SetProperty(ref _website, value);
        }

        private string _baseCurrency;
        public string BaseCurrency
        {
            get => _baseCurrency;
            set
            {
                if (SetProperty(ref _baseCurrency, value))
                {
                    if (!_isLoadingCompany)
                    {
                        ValidateBaseCurrency();
                    }

                    RaiseCanExecuteChanged();
                }
            }
        }

        private string _receiptFooterText;
        public string ReceiptFooterText
        {
            get => _receiptFooterText;
            set => SetProperty(ref _receiptFooterText, value);
        }

        private string _documentTerms;
        public string DocumentTerms
        {
            get => _documentTerms;
            set => SetProperty(ref _documentTerms, value);
        }

        private byte[] _companyLogo;
        public byte[] CompanyLogo
        {
            get => _companyLogo;
            set => SetProperty(ref _companyLogo, value);
        }

        private string _logoUrl;
        public string LogoUrl
        {
            get => _logoUrl;
            set => SetProperty(ref _logoUrl, value);
        }

        private bool _isEditing;
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

        public bool CanSaveCompany =>
            !HasErrors &&
            !string.IsNullOrWhiteSpace(TradingName) &&
            !string.IsNullOrWhiteSpace(BusinessRegistrationNumber) &&
            !string.IsNullOrWhiteSpace(AddressLine1) &&
            !string.IsNullOrWhiteSpace(ContactNumber) &&
            !string.IsNullOrWhiteSpace(BaseCurrency);

        #region Commands
        public ICommand SaveCompanyCommand { get; }
        public ICommand LoadCompanyCommand { get; }
        public ICommand NewCompanyCommand { get; }
        public ICommand UploadLogoCommand { get; }
        #endregion

        private async Task LoadCompanyAsync()
        {
            try
            {
                var company = await _companyRepository.GetAsync();
                if (company == null)
                {
                    CreateNewCompany();
                    return;
                }

                MapFromCompany(company);
                RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load company details: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SaveCompanyAsync()
        {
            ValidateAll();
            if (HasErrors)
            {
                MessageBox.Show("Please correct the highlighted errors before saving.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var company = BuildCompany();

                if (IsEditing && CompanyId > 0)
                {
                    await _companyRepository.UpdateAsync(company);
                    MessageBox.Show("Company details updated successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    CompanyId = await _companyRepository.CreateAsync(company);
                    IsEditing = true;
                    MessageBox.Show("Company details saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                await LoadCompanyAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving company details: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CreateNewCompany()
        {
            _isLoadingCompany = true;
            try
            {
                CompanyId = 0;
                TradingName = string.Empty;
                LegalName = string.Empty;
                BusinessRegistrationNumber = string.Empty;
                TaxRegistrationNumber = string.Empty;
                AddressLine1 = string.Empty;
                AddressLine2 = string.Empty;
                ContactNumber = string.Empty;
                Email = string.Empty;
                Website = string.Empty;
                BaseCurrency = "LKR";
                ReceiptFooterText = string.Empty;
                DocumentTerms = string.Empty;
                CompanyLogo = null;
                LogoUrl = null;
                IsEditing = false;
            }
            finally
            {
                _isLoadingCompany = false;
            }

            ClearAllErrors();
            RaiseCanExecuteChanged();
        }

        private void BrowseLogo()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                var fileInfo = new FileInfo(dialog.FileName);
                if (fileInfo.Length > FileUploadConstraints.MaxFileSizeBytes)
                {
                    MessageBox.Show(
                        FileUploadConstraints.BuildFileTooLargeMessage(fileInfo.Name),
                        "File Too Large",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                CompanyLogo = File.ReadAllBytes(dialog.FileName);
                LogoUrl = dialog.FileName;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load logo file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MapFromCompany(Company company)
        {
            _isLoadingCompany = true;
            try
            {
                CompanyId = company.Id;
                TradingName = company.TradingName;
                LegalName = company.LegalName;
                BusinessRegistrationNumber = company.BusinessRegistrationNumber;
                TaxRegistrationNumber = company.TaxRegistrationNumber;
                AddressLine1 = company.AddressLine1;
                AddressLine2 = company.AddressLine2;
                ContactNumber = company.ContactNumber;
                Email = company.Email;
                Website = company.Website;
                BaseCurrency = company.BaseCurrency;
                ReceiptFooterText = company.ReceiptFooterText;
                DocumentTerms = company.DocumentTerms;
                CompanyLogo = company.CompanyLogo;
                LogoUrl = CreateLogoPreviewFile(company.CompanyLogo);
                IsEditing = company.Id > 0;
            }
            finally
            {
                _isLoadingCompany = false;
            }

            ClearAllErrors();
        }

        private Company BuildCompany()
        {
            return new Company
            {
                Id = CompanyId,
                TradingName = Normalize(TradingName),
                LegalName = Normalize(LegalName),
                BusinessRegistrationNumber = Normalize(BusinessRegistrationNumber),
                TaxRegistrationNumber = Normalize(TaxRegistrationNumber),
                AddressLine1 = Normalize(AddressLine1),
                AddressLine2 = Normalize(AddressLine2),
                ContactNumber = Normalize(ContactNumber),
                Email = Normalize(Email),
                Website = Normalize(Website),
                BaseCurrency = Normalize(BaseCurrency)?.ToUpperInvariant(),
                ReceiptFooterText = Normalize(ReceiptFooterText),
                DocumentTerms = Normalize(DocumentTerms),
                CompanyLogo = CompanyLogo,
                CreatedBy = _userSessionService.CurrentUser.UserId
            };
        }

        private static string CreateLogoPreviewFile(byte[] logoBytes)
        {
            if (logoBytes == null || logoBytes.Length == 0)
            {
                return null;
            }

            var previewPath = Path.Combine(
                Path.GetTempPath(),
                $"company-logo-{Guid.NewGuid():N}.img");

            File.WriteAllBytes(previewPath, logoBytes);
            return previewPath;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private void RaiseCanExecuteChanged()
        {
            (SaveCompanyCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }

        #region Validation
        private void ValidateAll()
        {
            ValidateTradingName();
            ValidateBusinessRegistrationNumber();
            ValidateAddressLine1();
            ValidateContactNumber();
            ValidateEmail();
            ValidateBaseCurrency();
        }

        private void ValidateTradingName()
        {
            ClearErrors(nameof(TradingName));
            if (string.IsNullOrWhiteSpace(TradingName))
            {
                AddError(nameof(TradingName), "Trading name is required.");
            }
        }

        private void ValidateBusinessRegistrationNumber()
        {
            ClearErrors(nameof(BusinessRegistrationNumber));
            if (string.IsNullOrWhiteSpace(BusinessRegistrationNumber))
            {
                AddError(nameof(BusinessRegistrationNumber), "Business registration number is required.");
            }
        }

        private void ValidateAddressLine1()
        {
            ClearErrors(nameof(AddressLine1));
            if (string.IsNullOrWhiteSpace(AddressLine1))
            {
                AddError(nameof(AddressLine1), "Address line 1 is required.");
            }
        }

        private void ValidateContactNumber()
        {
            ClearErrors(nameof(ContactNumber));
            if (string.IsNullOrWhiteSpace(ContactNumber))
            {
                AddError(nameof(ContactNumber), "Contact number is required.");
            }
        }

        private void ValidateEmail()
        {
            ClearErrors(nameof(Email));
            if (!string.IsNullOrWhiteSpace(Email) &&
                !Regex.IsMatch(Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                AddError(nameof(Email), "Email address is not valid.");
            }
        }

        private void ValidateBaseCurrency()
        {
            ClearErrors(nameof(BaseCurrency));
            if (string.IsNullOrWhiteSpace(BaseCurrency))
            {
                AddError(nameof(BaseCurrency), "Base currency is required.");
                return;
            }

            if (!Regex.IsMatch(BaseCurrency.Trim(), @"^[A-Za-z]{3,10}$"))
            {
                AddError(nameof(BaseCurrency), "Base currency must contain only letters.");
            }
        }
        #endregion
    }
}
