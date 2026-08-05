using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using PointOfSale.Core.Common;
using PointOfSale.Core.Enums;
using PointOfSale.Core.Interfaces.Purchasing;
using PointOfSale.Core.Interfaces.Repositories.System;
using PointOfSale.Core.Interfaces.Services;
using PointOfSale.Core.Models.Purchasing;
using PointOfSale.Core.Models.System;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;
using PointOfSale.UI.Services;


namespace PointOfSale.UI.ViewModels.Purchasing
{
    public class SupplierViewModel : BaseViewModel
    {
        private readonly CloudStorageService _storageService;
        private readonly ISupplierRepository _supplierRepository;
        private readonly IUserSessionService _userSessionService;
        private readonly IExcelService _excelService;
        private readonly IBankRepository _bankRepository;
        private readonly IBankBranchRepository _bankBranchRepository;

        private readonly List<long> _pendingDeleteDocumentIds = new List<long>();
        private readonly List<Supplier> _allSuppliers = new List<Supplier>();
        private CancellationTokenSource _searchDebounceTokenSource;
        private SupplierContact _editingContact;
        private const string NewContactNameErrorKey = "NewContact.ContactName";
        private const string NewContactPhoneErrorKey = "NewContact.PhoneNumber";
        private const string NewContactEmailErrorKey = "NewContact.EmailAddress";
        private const string AlphaNumericPattern = @"^[a-zA-Z0-9\s.]+$";
        private const string AlphaNumericNoSpacePattern = @"^[a-zA-Z0-9-]+$";
        private const string AlphaNumericDashPattern = @"^[a-zA-Z0-9\-]+$";
        private const string DigitsOnlyPattern = @"^\d+$";
        private const string EmailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";

        public SupplierViewModel(
            ISupplierRepository supplierRepository,
            IUserSessionService userSessionService,
            IExcelService excelService,
            IBankRepository bankRepository,
            IBankBranchRepository bankBranchRepository,
            CloudStorageService storageService)
        {
            _supplierRepository = supplierRepository ?? throw new ArgumentNullException(nameof(supplierRepository));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));
            _excelService = excelService ?? throw new ArgumentNullException(nameof(excelService));
            _bankRepository = bankRepository ?? throw new ArgumentNullException(nameof(bankRepository));
            _bankBranchRepository = bankBranchRepository ?? throw new ArgumentNullException(nameof(bankBranchRepository));
            _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));

            SaveSupplierCommand = new AsyncRelayCommand(async _ => await SaveSupplierAsync(), _ => CanSaveSupplier);
            LoadSupplierCommand = new AsyncRelayCommand(async _ => await LoadSupplierAsync());
            NewSupplierCommand = new RelayCommand(_ => CreateNewSupplier());
            EditSupplierCommand = new AsyncRelayCommand(async _ => await BeginEditAsync(), _ => SelectedSupplier != null);
            NewContact = CreateBlankContact();
            AddContactCommand = new RelayCommand(_ => AddContact());
            EditContactCommand = new RelayCommand<SupplierContact>(EditContact);
            RemoveContactCommand = new RelayCommand<SupplierContact>(RemoveContact);
            AddDocumentCommand = new AsyncRelayCommand(async _ => await UploadAndAddDocumentAsync());
            OpenDocumentCommand = new RelayCommand(doc => OpenDocument(doc as SupplierDocument));
            RemoveDocumentCommand = new RelayCommand(doc => RemoveDocument(doc as SupplierDocument));
            ExportToExcelCommand = new RelayCommand(_ => ExportToExcel(), _ => SuppliersList.Any());

            _ = LoadSupplierAsync();
            _ = LoadBanksAsync();
        }

        #region Properties : UI Bindings
        private int _currentUserId => _userSessionService.CurrentUser.UserId;
        public ObservableCollection<Supplier> SuppliersList { get; } = new ObservableCollection<Supplier>();
        public ObservableCollection<SupplierContact> ContactsList { get; } = new ObservableCollection<SupplierContact>();
        public ObservableCollection<SupplierDocument> Documents { get; } = new ObservableCollection<SupplierDocument>();

        public ObservableCollection<Bank> AvailableBanks { get; } = new ObservableCollection<Bank>();
        public ObservableCollection<BankBranch> AvailableBranches { get; } = new ObservableCollection<BankBranch>();
        private bool _isLoadingSupplier;

        public List<SupplierPaymentMethod> AvailablePaymentMethods { get; } =
            Enum.GetValues(typeof(SupplierPaymentMethod))
                    .Cast<SupplierPaymentMethod>()
                    .ToList();

        private Supplier _selectedSupplier;
        public Supplier SelectedSupplier
        {
            get => _selectedSupplier;
            set
            {
                if (SetProperty(ref _selectedSupplier, value))
                {
                    ExitEditMode();
                    RaiseCanExecuteChanged();
                }
            }
        }

        private SupplierContact _newContact;
        public SupplierContact NewContact
        {
            get => _newContact;
            set => SetProperty(ref _newContact, value);
        }

        private string _contactActionButtonText = "Add";
        public string ContactActionButtonText
        {
            get => _contactActionButtonText;
            set => SetProperty(ref _contactActionButtonText, value);
        }

        private string _searchTerm;
        public string SearchTerm
        {
            get => _searchTerm;
            set
            {
                if (SetProperty(ref _searchTerm, value))
                {
                    DebounceSearch();
                }
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

        // Supplier fields bound to form
        private int _supplierId;
        public int SupplierId
        {
            get => _supplierId;
            set => SetProperty(ref _supplierId, value);
        }
        private string _supplierCode;
        public string SupplierCode
        {
            get => _supplierCode;
            set => SetProperty(ref _supplierCode, value);
        }

        private string _supplierName;
        public string SupplierName
        {
            get => _supplierName;
            set
            {
                SetProperty(ref _supplierName, value);
                ValidateSupplierName();
                RaiseCanExecuteChanged();
            }
        }

        private string _taxRegistrationNumber;
        public string TaxRegistrationNumber
        {
            get => _taxRegistrationNumber;
            set
            {
                SetProperty(ref _taxRegistrationNumber, value);
                ValidateTaxRegistrationNumber();
                RaiseCanExecuteChanged();
            }
        }

        private string _businessRegistrationNumber;
        public string BusinessRegistrationNumber
        {
            get => _businessRegistrationNumber;
            set
            {
                SetProperty(ref _businessRegistrationNumber, value);
                ValidateBusinessRegistrationNumber();
                RaiseCanExecuteChanged();
            }
        }

        private string _address;
        public string Address
        {
            get => _address;
            set
            {
                SetProperty(ref _address, value);
                ValidateAddress();
                RaiseCanExecuteChanged();
            }
        }

        private SupplierPaymentMethod _defaultPaymentMethod = SupplierPaymentMethod.BANK_TRANSFER;
        public SupplierPaymentMethod DefaultPaymentMethod
        {
            get => _defaultPaymentMethod;
            set
            {
                if (SetProperty(ref _defaultPaymentMethod, value))
                {
                    OnPropertyChanged(nameof(IsBankTransferDetailsVisible));
                    OnPropertyChanged(nameof(IsAccountNameVisible));
                    ValidateBank();
                    ValidateBankBranch();
                    ValidateAccountName();
                    ValidateAccountNumber();
                    RaiseCanExecuteChanged();
                }
            }
        }

        private int? _bankId;
        public int? BankId
        {
            get => _bankId;
            set
            {
                if (SetProperty(ref _bankId, value))
                {
                    ValidateBank();
                    if (!_isLoadingSupplier)
                    {
                        BankBranchId = null;
                        _ = LoadBranchesForSelectedBankAsync();
                    }
                    OnPropertyChanged(nameof(IsBranchEnabled));
                }
            }
        }

        private int? _branchId;
        public int? BankBranchId
        {
            get => _branchId;
            set
            {
                if (SetProperty(ref _branchId, value))
                {
                    ValidateBankBranch();
                    RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsBranchEnabled => BankId.HasValue;

        private string _accountNumber;
        public string AccountNumber
        {
            get => _accountNumber;
            set
            {
                SetProperty(ref _accountNumber, value);
                ValidateAccountNumber();
                RaiseCanExecuteChanged();
            }
        }

        private string _accountName;
        public string AccountName
        {
            get => _accountName;
            set
            {
                SetProperty(ref _accountName, value);
                ValidateAccountName();
                RaiseCanExecuteChanged();
            }
        }

        public Visibility IsBankTransferDetailsVisible =>
                DefaultPaymentMethod == SupplierPaymentMethod.BANK_TRANSFER ? Visibility.Visible : Visibility.Collapsed;

        public Visibility IsAccountNameVisible =>
                (DefaultPaymentMethod == SupplierPaymentMethod.BANK_TRANSFER || DefaultPaymentMethod == SupplierPaymentMethod.CHEQUE)
                ? Visibility.Visible : Visibility.Collapsed;

        public Visibility IsCreditDetailsVisible => IsCredit ? Visibility.Visible : Visibility.Collapsed;

        private bool _isCredit;
        public bool IsCredit
        {
            get => _isCredit;
            set
            {
                if (SetProperty(ref _isCredit, value))
                {
                    OnPropertyChanged(nameof(IsCreditDetailsVisible));
                    if (!value)
                    {
                        CreditPeriodDays = null;
                        CreditLimit = null;
                    }
                }
            }
        }
        private int? _creditPeriodDays;
        public int? CreditPeriodDays
        {
            get => _creditPeriodDays;
            set => SetProperty(ref _creditPeriodDays, value);
        }

        private decimal? _creditLimit;
        public decimal? CreditLimit
        {
            get => _creditLimit;
            set => SetProperty(ref _creditLimit, value);
        }

        private bool _isActive = true;
        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        // Validation gate for save button
        public bool CanSaveSupplier => !HasErrors &&
            !string.IsNullOrWhiteSpace(SupplierName) &&
            !string.IsNullOrWhiteSpace(Address) &&
            ContactsList.Any();
        #endregion

        #region Commands
        public ICommand SaveSupplierCommand { get; }
        public ICommand LoadSupplierCommand { get; }
        public ICommand NewSupplierCommand { get; }
        public ICommand EditSupplierCommand { get; }
        public ICommand AddContactCommand { get; }
        public ICommand EditContactCommand { get; }
        public ICommand RemoveContactCommand { get; }
        public ICommand AddDocumentCommand { get; }
        public ICommand OpenDocumentCommand { get; }
        public ICommand RemoveDocumentCommand { get; }
        public ICommand ExportToExcelCommand { get; }
        #endregion

        #region CRUD Methods
        private async Task LoadBanksAsync()
        {
            try
            {
                AvailableBanks.Clear();
                var banks = await _bankRepository.GetAllAsync();
                foreach (var bank in banks.Where(b => b.IsActive))
                {
                    AvailableBanks.Add(bank);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load banks: {ex.Message}";
            }
        }

        private async Task LoadBranchesForSelectedBankAsync()
        {
            try
            {
                AvailableBranches.Clear();
                if (BankId.HasValue)
                {
                    var branches = await _bankBranchRepository.GetByBankIdAsync(BankId.Value);
                    foreach (var branch in branches.Where(b => b.IsActive))
                    {
                        AvailableBranches.Add(branch);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load branches: {ex.Message}";
            }
        }

        private async Task LoadSupplierAsync()
        {
            try
            {
                SuppliersList.Clear();
                _allSuppliers.Clear();
                var suppliers = await _supplierRepository.GetAllAsync();

                foreach (var supplier in suppliers)
                    _allSuppliers.Add(supplier);

                await LoadAllSupplierDocumentsAsync(_allSuppliers);

                ApplySupplierSearch();

                ClearAllErrors();
                (ExportToExcelCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load supplier: {ex.Message}";
            }
        }

        // Populates each Supplier row's Documents collection so DocumentsCount
        // is accurate in the DataGrid without a second user action.
        private async Task LoadAllSupplierDocumentsAsync(IEnumerable<Supplier> suppliers)
        {
            foreach (var supplier in suppliers)
            {
                try
                {
                    var docs = await _supplierRepository.GetDocumentsBySupplierIdAsync(supplier.SupplierId);
                    supplier.Documents.Clear();
                    foreach (var doc in docs)
                        supplier.Documents.Add(doc);
                }
                catch
                {
                    // Don't let a single supplier's document failure block the list.
                }
            }
        }

        private async void DebounceSearch()
        {
            _searchDebounceTokenSource?.Cancel();
            var currentTokenSource = new CancellationTokenSource();
            _searchDebounceTokenSource = currentTokenSource;

            try
            {
                await Task.Delay(500, currentTokenSource.Token);

                if (!currentTokenSource.IsCancellationRequested)
                {
                    ApplySupplierSearch();
                }
            }
            catch (TaskCanceledException)
            {
            }
        }

        private void ApplySupplierSearch()
        {
            var term = SearchTerm?.Trim();

            var filteredSuppliers = string.IsNullOrWhiteSpace(term)
                ? _allSuppliers
                : _allSuppliers.Where(supplier =>
                    Contains(supplier.SupplierCode, term) ||
                    Contains(supplier.SupplierName, term) ||
                    Contains(supplier.BusinessRegistrationNumber, term) ||
                    Contains(supplier.TaxRegistrationNumber, term) ||
                    Contains(supplier.Address, term) ||
                    supplier.Contacts.Any(contact =>
                        Contains(contact.ContactName, term) ||
                        Contains(contact.Designation, term) ||
                        Contains(contact.PhoneNumber, term) ||
                        Contains(contact.EmailAddress, term)));

            SuppliersList.Clear();
            foreach (var supplier in filteredSuppliers)
            {
                SuppliersList.Add(supplier);
            }

            (ExportToExcelCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private static bool Contains(string value, string searchTerm)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private async Task SaveSupplierAsync()
        {
            if (!string.IsNullOrWhiteSpace(Address))
            {
                Address = Regex.Replace(Address, @"(\r\n|\n|\r){2,}", Environment.NewLine);
                Address = Address.Trim();
            }

            ValidateAll();
            if (HasErrors)
            {
                MessageBox.Show("Please correct the highlighted errors before saving.");
                return;
            }

            try
            {
                if (IsEditing && SelectedSupplier != null)
                {
                    SelectedSupplier.SupplierId = SupplierId;
                    SelectedSupplier.SupplierName = SupplierName;
                    SelectedSupplier.TaxRegistrationNumber = TaxRegistrationNumber;
                    SelectedSupplier.BusinessRegistrationNumber = BusinessRegistrationNumber;
                    SelectedSupplier.Address = Address;
                    SelectedSupplier.Contacts = ContactsList.ToList();
                    SelectedSupplier.DefaultPaymentMethod = DefaultPaymentMethod;
                    SelectedSupplier.IsCredit = IsCredit;
                    SelectedSupplier.CreditPeriodDays = CreditPeriodDays;
                    SelectedSupplier.CreditLimit = CreditLimit;
                    SelectedSupplier.BankId = BankId;
                    SelectedSupplier.BankBranchId = BankBranchId;
                    SelectedSupplier.AccountNumber = AccountNumber;
                    SelectedSupplier.AccountName = AccountName;
                    SelectedSupplier.IsActive = IsActive;
                    SelectedSupplier.UpdatedBy = _currentUserId;

                    await _supplierRepository.UpdateAsync(SelectedSupplier);
                    await SyncDocumentsAsync(SelectedSupplier.SupplierId);
                    MessageBox.Show("Supplier updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    var newSupplier = new Supplier
                    {
                        SupplierName = SupplierName,
                        TaxRegistrationNumber = TaxRegistrationNumber,
                        BusinessRegistrationNumber = BusinessRegistrationNumber,
                        Address = Address,
                        Contacts = ContactsList.ToList(),
                        DefaultPaymentMethod = DefaultPaymentMethod,
                        IsCredit = IsCredit,
                        CreditPeriodDays = CreditPeriodDays,
                        CreditLimit = CreditLimit,
                        BankId = BankId,
                        BankBranchId = BankBranchId,
                        AccountNumber = AccountNumber,
                        AccountName = AccountName,
                        IsActive = IsActive,
                        CreatedBy = _currentUserId
                    };
                    int newSupplierId = await _supplierRepository.CreateAsync(newSupplier);
                    await SyncDocumentsAsync(newSupplierId);
                    MessageBox.Show("Supplier saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                await LoadSupplierAsync();
                CreateNewSupplier();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving supplier: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportToExcel()
        {
            try
            {
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel Files (*.xlsx)|*.xlsx",
                    Title = "Export Suppliers to Excel",
                    FileName = $"Suppliers_List_{DateTime.Now:yyyyMMdd}.xlsx"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    _excelService.ExportSuppliers(SuppliersList, saveFileDialog.FileName);
                    MessageBox.Show("Export completed successfully!", "Export Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "File Locked", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while exporting: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Contact Management
        private SupplierContact CreateBlankContact()
        {
            return new SupplierContact
            {
                SupplierId = SupplierId,
                ContactName = string.Empty,
                PhoneNumber = string.Empty,
                EmailAddress = string.Empty,
                IsWhatsApp = false,
                IsPrimary = false,
                IsActive = true
            };
        }

        private void AddContact()
        {
            if (!ValidateContactEditor())
            {
                return;
            }

            if (_editingContact != null)
            {
                ApplyNewContactTo(_editingContact);
            }
            else
            {
                ContactsList.Add(CreateContactFromNewContact());
            }

            ResetContactEditor();
            RefreshContactsView();
            ValidateContacts();
            RaiseCanExecuteChanged();
        }

        private void EditContact(SupplierContact contact)
        {
            if (contact == null)
                return;

            _editingContact = contact;
            NewContact = new SupplierContact
            {
                SupplierId = contact.SupplierId,
                ContactName = contact.ContactName,
                PhoneNumber = contact.PhoneNumber,
                EmailAddress = contact.EmailAddress,
                IsWhatsApp = contact.IsWhatsApp,
                IsPrimary = contact.IsPrimary,
                IsActive = contact.IsActive
            };
            ContactActionButtonText = "Update Contact";
            ClearContactEditorErrors();
        }

        private SupplierContact CreateContactFromNewContact()
        {
            return new SupplierContact
            {
                Id = 0,
                SupplierId = SupplierId,
                ContactName = NewContact.ContactName.Trim(),
                PhoneNumber = Clean(NewContact.PhoneNumber),
                EmailAddress = Clean(NewContact.EmailAddress),
                IsWhatsApp = NewContact.IsWhatsApp,
                IsPrimary = GetPrimaryValue(),
                IsActive = true
            };
        }

        private void ApplyNewContactTo(SupplierContact contact)
        {
            contact.SupplierId = SupplierId;
            contact.ContactName = NewContact.ContactName.Trim();
            contact.PhoneNumber = Clean(NewContact.PhoneNumber);
            contact.EmailAddress = Clean(NewContact.EmailAddress);
            contact.IsWhatsApp = NewContact.IsWhatsApp;
            contact.IsPrimary = GetPrimaryValue(contact);
            contact.IsActive = true;
        }

        private void RemoveContact(SupplierContact contact)
        {
            if (contact == null)
                return;

            if (ReferenceEquals(contact, _editingContact))
            {
                ResetContactEditor();
            }

            ContactsList.Remove(contact);
            if (ContactsList.Any() && !ContactsList.Any(contactItem => contactItem.IsPrimary))
            {
                ContactsList[0].IsPrimary = true;
            }

            RefreshContactsView();
            ValidateContacts();
            RaiseCanExecuteChanged();
        }

        private bool GetPrimaryValue(SupplierContact editingContact = null)
        {
            return NewContact.IsPrimary ||
                   !ContactsList.Any(contactItem => contactItem.IsPrimary && !ReferenceEquals(contactItem, editingContact));
        }

        private void ResetContactEditor()
        {
            _editingContact = null;
            NewContact = CreateBlankContact();
            ContactActionButtonText = "+ Add Contact";
            ClearContactEditorErrors();
        }

        private void RefreshContactsView()
        {
            System.Windows.Data.CollectionViewSource.GetDefaultView(ContactsList)?.Refresh();
        }

        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
        #endregion

        #region Document Management
        private Task UploadAndAddDocumentAsync()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Registration Files (*.jpg;*.jpeg;*.pdf)|*.jpg;*.jpeg;*.pdf",
                CheckFileExists = true,
                Multiselect = true
            };

            if (dialog.ShowDialog() != true)
                return Task.CompletedTask;

            try
            {
                foreach (var filePath in dialog.FileNames)
                {
                    var extension = Path.GetExtension(filePath);
                    if (!string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
                    {
                        MessageBox.Show($"Skipped '{Path.GetFileName(filePath)}': only JPG and PDF files are supported.", "Invalid File", MessageBoxButton.OK, MessageBoxImage.Warning);
                        continue;
                    }

                    var fileInfo = new FileInfo(filePath);
                    if (fileInfo.Length > FileUploadConstraints.MaxFileSizeBytes)
                    {
                        MessageBox.Show(FileUploadConstraints.BuildFileTooLargeMessage(fileInfo.Name), "File Too Large", MessageBoxButton.OK, MessageBoxImage.Warning);
                        continue;
                    }

                    var fileName = Path.GetFileName(filePath);

                    Documents.Add(new SupplierDocument
                    {
                        Id = 0,
                        SupplierId = SupplierId,
                        DocumentName = fileName,
                        LocalFilePath = filePath,
                        UploadedDate = DateTime.Now,
                        UploadedBy = _currentUserId
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to add document: {ex.Message}", "Document Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return Task.CompletedTask;
        }

        private void OpenDocument(SupplierDocument document)
        {
            if (document == null ||
                (string.IsNullOrWhiteSpace(document.DocumentUrl) && string.IsNullOrWhiteSpace(document.LocalFilePath)))
            {
                MessageBox.Show("No document available.", "Document Not Found", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var secureUrl = File.Exists(document.LocalFilePath)
                    ? document.LocalFilePath
                    : !string.IsNullOrWhiteSpace(document.SecureDocumentUrl)
                        ? document.SecureDocumentUrl
                        : _storageService.GetSecureFileUrl(document.DocumentUrl);

                Process.Start(new ProcessStartInfo
                {
                    FileName = secureUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open document: {ex.Message}", "Open Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RemoveDocument(SupplierDocument document)
        {
            if (document == null) return;

            if (document.Id > 0)
                _pendingDeleteDocumentIds.Add(document.Id);

            Documents.Remove(document);
        }

        private async Task LoadDocumentsAsync(int supplierId)
        {
            Documents.Clear();
            _pendingDeleteDocumentIds.Clear();

            var docs = await _supplierRepository.GetDocumentsBySupplierIdAsync(supplierId);
            foreach (var doc in docs)
            {
                doc.SecureDocumentUrl = _storageService.GetSecureFileUrl(doc.DocumentUrl);
                Documents.Add(doc);
            }
        }

        private async Task SyncDocumentsAsync(int supplierId)
        {
            foreach (var id in _pendingDeleteDocumentIds)
                await _supplierRepository.DeleteDocumentAsync(id);

            _pendingDeleteDocumentIds.Clear();

            foreach (var doc in Documents.Where(d => d.Id == 0).ToList())
            {
                doc.SupplierId = supplierId;
                doc.UploadedBy = _currentUserId;

                if (string.IsNullOrWhiteSpace(doc.DocumentUrl))
                {
                    if (string.IsNullOrWhiteSpace(doc.LocalFilePath) || !File.Exists(doc.LocalFilePath))
                    {
                        throw new FileNotFoundException($"Supplier document file is missing: {doc.DocumentName}");
                    }

                    doc.DocumentUrl = await _storageService.UploadFileAsync(doc.LocalFilePath, "supplier-documents", doc.DocumentName);
                    doc.SecureDocumentUrl = _storageService.GetSecureFileUrl(doc.DocumentUrl);
                }

                doc.Id = await _supplierRepository.AddDocumentAsync(doc);
            }
        }
        #endregion

        #region Form Helpers
        private void CreateNewSupplier()
        {
            _isLoadingSupplier = true;
            try
            {
                SelectedSupplier = null;

                SupplierId = 0;
                SupplierCode = string.Empty;
                SupplierName = string.Empty;
                TaxRegistrationNumber = string.Empty;
                BusinessRegistrationNumber = string.Empty;
                Address = string.Empty;
                ContactsList.Clear();
                ResetContactEditor();
                DefaultPaymentMethod = SupplierPaymentMethod.BANK_TRANSFER;
                IsCredit = false;
                CreditPeriodDays = null;
                CreditLimit = null;
                BankId = null;
                BankBranchId = null;
                AvailableBranches.Clear();
                AccountNumber = string.Empty;
                AccountName = string.Empty;
                Documents.Clear();
                _pendingDeleteDocumentIds.Clear();
                IsActive = true;

                ClearAllErrors();
                RaiseCanExecuteChanged();
            }
            finally
            {
                _isLoadingSupplier = false;
            }
        }

        private void ExitEditMode()
        {
            IsEditing = false;
            Documents.Clear();
            ContactsList.Clear();
            ResetContactEditor();
            _pendingDeleteDocumentIds.Clear();
        }

        private async Task BeginEditAsync()
        {
            if (SelectedSupplier == null) return;

            _isLoadingSupplier = true;
            try
            {
                IsEditing = true;
                var supplier = await _supplierRepository.GetByIdAsync(SelectedSupplier.SupplierId) ?? SelectedSupplier;
                _selectedSupplier = supplier;
                OnPropertyChanged(nameof(SelectedSupplier));

                SupplierId = supplier.SupplierId;
                SupplierCode = supplier.SupplierCode;
                SupplierName = supplier.SupplierName;
                TaxRegistrationNumber = supplier.TaxRegistrationNumber;
                BusinessRegistrationNumber = supplier.BusinessRegistrationNumber;
                Address = supplier.Address;
                DefaultPaymentMethod = supplier.DefaultPaymentMethod ?? SupplierPaymentMethod.CASH;
                IsCredit = supplier.IsCredit;
                CreditPeriodDays = supplier.CreditPeriodDays;
                CreditLimit = supplier.CreditLimit;
                BankId = supplier.BankId;

                await LoadBranchesForSelectedBankAsync();

                BankBranchId = supplier.BankBranchId;
                AccountNumber = supplier.AccountNumber;
                AccountName = supplier.AccountName;
                IsActive = supplier.IsActive;

                ContactsList.Clear();
                foreach (var contact in supplier.Contacts.Where(contact => contact.IsActive))
                {
                    ContactsList.Add(contact);
                }
                ResetContactEditor();

                try
                {
                    await LoadDocumentsAsync(supplier.SupplierId);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to load documents: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                RaiseCanExecuteChanged();
            }
            finally
            {
                _isLoadingSupplier = false;
            }
        }

        private void RaiseCanExecuteChanged()
        {
            (SaveSupplierCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (EditSupplierCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (ExportToExcelCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
        #endregion

        #region Validation
        private void ValidateAll()
        {
            ValidateSupplierName();
            ValidateBusinessRegistrationNumber();
            ValidateTaxRegistrationNumber();
            ValidateContacts();
            ValidateAddress();
            ValidateBank();
            ValidateBankBranch();
            ValidateAccountName();
            ValidateAccountNumber();
        }

        private void ValidateSupplierName()
        {
            ClearErrors(nameof(SupplierName));
            if (string.IsNullOrWhiteSpace(SupplierName))
                AddError(nameof(SupplierName), "Supplier name is required.");
            else if (!Regex.IsMatch(SupplierName, @"^[a-zA-Z0-9\s\.\,\-\&]+$"))
                AddError(nameof(SupplierName), "Supplier name contains invalid characters.");
        }

        private void ValidateBusinessRegistrationNumber()
        {
            ClearErrors(nameof(BusinessRegistrationNumber));

            if (!string.IsNullOrWhiteSpace(BusinessRegistrationNumber) &&
                !Regex.IsMatch(BusinessRegistrationNumber.Trim(), AlphaNumericNoSpacePattern))
            {
                AddError(nameof(BusinessRegistrationNumber), "Business registration number cannot contain spaces or special characters.");
            }
        }

        private void ValidateTaxRegistrationNumber()
        {
            ClearErrors(nameof(TaxRegistrationNumber));

            if (!string.IsNullOrWhiteSpace(TaxRegistrationNumber) &&
                !Regex.IsMatch(TaxRegistrationNumber.Trim(), AlphaNumericDashPattern))
            {
                AddError(nameof(TaxRegistrationNumber), "Tax registration number cannot contain spaces or special characters.");
            }
        }

        private bool ValidateContactEditor()
        {
            ClearContactEditorErrors();

            if (NewContact == null || string.IsNullOrWhiteSpace(NewContact.ContactName))
            {
                AddError(NewContactNameErrorKey, "Contact name is required.");
            }
            else if (!Regex.IsMatch(NewContact.ContactName.Trim(), AlphaNumericPattern))
            {
                AddError(NewContactNameErrorKey, "Contact name cannot contain special characters.");
            }

            if (!string.IsNullOrWhiteSpace(NewContact?.PhoneNumber) &&
                !Regex.IsMatch(NewContact.PhoneNumber.Trim(), DigitsOnlyPattern))
            {
                AddError(NewContactPhoneErrorKey, "Phone number must contain digits only.");
            }

            if (!string.IsNullOrWhiteSpace(NewContact?.EmailAddress) &&
                !Regex.IsMatch(NewContact.EmailAddress.Trim(), EmailPattern))
            {
                AddError(NewContactEmailErrorKey, "Contact email address is invalid.");
            }

            RaiseCanExecuteChanged();
            return GetErrors(NewContactNameErrorKey) == null &&
                   GetErrors(NewContactPhoneErrorKey) == null &&
                   GetErrors(NewContactEmailErrorKey) == null;
        }

        private void ClearContactEditorErrors()
        {
            ClearErrors(NewContactNameErrorKey);
            ClearErrors(NewContactPhoneErrorKey);
            ClearErrors(NewContactEmailErrorKey);
        }

        private void ValidateContacts()
        {
            ClearErrors(nameof(ContactsList));

            if (!ContactsList.Any())
            {
                AddError(nameof(ContactsList), "At least one supplier contact is required.");
                return;
            }

            if (!ContactsList.Any(contact => contact.IsPrimary))
            {
                AddError(nameof(ContactsList), "At least one contact must be marked as primary.");
            }

            foreach (var contact in ContactsList)
            {
                if (string.IsNullOrWhiteSpace(contact.ContactName))
                {
                    AddError(nameof(ContactsList), "Contact name is required for every contact.");
                    break;
                }

                if (!Regex.IsMatch(contact.ContactName.Trim(), AlphaNumericPattern))
                {
                    AddError(nameof(ContactsList), "One or more contact names contain special characters.");
                    break;
                }

                if (!string.IsNullOrWhiteSpace(contact.PhoneNumber) &&
                    !Regex.IsMatch(contact.PhoneNumber.Trim(), DigitsOnlyPattern))
                {
                    AddError(nameof(ContactsList), "One or more contact phone numbers contain non-digit characters.");
                    break;
                }

                if (!string.IsNullOrWhiteSpace(contact.EmailAddress) &&
                    !Regex.IsMatch(contact.EmailAddress.Trim(), EmailPattern))
                {
                    AddError(nameof(ContactsList), "One or more contact email addresses are invalid.");
                    break;
                }
            }
        }
        private void ValidateAddress()
        {
            ClearErrors(nameof(Address));

            if (!string.IsNullOrWhiteSpace(Address))
            {
                if (Regex.IsMatch(Address, @"(\r\n|\n|\r)\s*(\r\n|\n|\r)"))
                {
                    AddError(nameof(Address), "Address cannot contain consecutive blank lines.");
                }
                else if (Address.StartsWith("\r") || Address.StartsWith("\n") || Address.StartsWith(" ") ||
                         Address.EndsWith("\r") || Address.EndsWith("\n") || Address.EndsWith(" "))
                {
                    AddError(nameof(Address), "Address cannot start or end with blank lines or spaces.");
                }
            }
        }
        private void ValidateBank()
        {
            ClearErrors(nameof(BankId));
            if (DefaultPaymentMethod == SupplierPaymentMethod.BANK_TRANSFER)
            {
                if (!BankId.HasValue)
                    AddError(nameof(BankId), "Bank is required.");
            }
        }

        private void ValidateBankBranch()
        {
            ClearErrors(nameof(BankBranchId));
            if (DefaultPaymentMethod == SupplierPaymentMethod.BANK_TRANSFER)
            {
                if (!BankBranchId.HasValue)
                    AddError(nameof(BankBranchId), "Branch is required.");
            }
        }

        private void ValidateAccountName()
        {
            ClearErrors(nameof(AccountName));
            if (DefaultPaymentMethod == SupplierPaymentMethod.BANK_TRANSFER || DefaultPaymentMethod == SupplierPaymentMethod.CHEQUE)
            {
                if (string.IsNullOrWhiteSpace(AccountName))
                    AddError(nameof(AccountName), "Account name is required.");
                else if (!Regex.IsMatch(AccountName.Trim(), AlphaNumericPattern))
                    AddError(nameof(AccountName), "Account name cannot contain special characters.");
            }
        }
        private void ValidateAccountNumber()
        {
            ClearErrors(nameof(AccountNumber));
            if (DefaultPaymentMethod == SupplierPaymentMethod.BANK_TRANSFER)
            {
                if (string.IsNullOrWhiteSpace(AccountNumber))
                    AddError(nameof(AccountNumber), "Account number is required.");
                else if (!Regex.IsMatch(AccountNumber.Trim(), DigitsOnlyPattern))
                    AddError(nameof(AccountNumber), "Account number must contain digits only.");
            }
            else if (!string.IsNullOrWhiteSpace(AccountNumber) &&
                     !Regex.IsMatch(AccountNumber.Trim(), DigitsOnlyPattern))
            {
                AddError(nameof(AccountNumber), "Account number must contain digits only.");
            }
        }
        #endregion
    }
}
