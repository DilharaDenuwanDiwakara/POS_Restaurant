using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using PointOfSale.Core.Interfaces.Repositories.Sales;
using PointOfSale.Core.Models.Sales;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Sales
{
    public class CustomerViewModel : BaseViewModel
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly IUserSessionService _userSessionService;
        private readonly ICollectionView _customerListView;
        public CustomerViewModel(ICustomerRepository customerRepository, IUserSessionService userSessionService)
        {
            _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));

            _customerListView = CollectionViewSource.GetDefaultView(CustomerList);
            _customerListView.Filter = FilterCustomer;

            // Initialize command
            SaveCustomerCommand = new AsyncRelayCommand(async _ => await SaveCustomerAsync(), _ => CanSaveCustomer);
            LoadCustomerCommand = new AsyncRelayCommand(async _ => await LoadCustomerAsync());
            EditCustomerCommand = new RelayCommand(_ => SetEditMode(true), _ => SelectedCustomer != null);
            NewCustomerCommand = new RelayCommand(_ => CreateNewCustomer());

            _ = LoadCustomerAsync();
        }

        #region Properties
        public ObservableCollection<Customer> CustomerList { get; } = new ObservableCollection<Customer>();

        private string _searchQuery;
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                    _customerListView.Refresh();
            }
        }

        public Customer _selectedCustomer;
        public Customer SelectedCustomer
        {
            get => _selectedCustomer;
            set
            {
                if (SetProperty(ref _selectedCustomer, value))
                {
                    SetEditMode(false);
                    RaiseCanExecuteChanged();
                }
            }
        }

        public string SaveButtonText => IsEditing ? "Update" : "Save";

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

        private int _customerId;
        public int CustomerId
        {
            get => _customerId;
            set => SetProperty(ref _customerId, value);
        }

        private string _customerName;
        public string CustomerName
        {
            get => _customerName;
            set
            {
                SetProperty(ref _customerName, value);
                ValidateCustomerName();
                RaiseCanExecuteChanged();
            }
        }

        private string _contactNumber;
        public string ContactNumber
        {
            get => _contactNumber;
            set
            {
                SetProperty(ref _contactNumber, value);
                ValidateContactNumber();
                RaiseCanExecuteChanged();
            }
        }

        private string _billingAddress;
        public string BillingAddress
        {
            get => _billingAddress;
            set
            {
                SetProperty(ref _billingAddress, value);
                RaiseCanExecuteChanged();
            }
        }

        private bool _isTaxRegistered;
        public bool IsTaxRegistered
        {
            get => _isTaxRegistered;
            set
            {
                if (SetProperty(ref _isTaxRegistered, value))
                {
                    if (!value)
                        TaxRegistrationNumber = string.Empty;

                    RaiseCanExecuteChanged();
                }
            }
        }

        private string _taxRegistrationNumber;
        public string TaxRegistrationNumber
        {
            get => _taxRegistrationNumber;
            set
            {
                SetProperty(ref _taxRegistrationNumber, value);
                RaiseCanExecuteChanged();
            }
        }

        private bool _isActive = true;
        public bool IsActive
        {
            get => _isActive;
            set
            {
                SetProperty(ref _isActive, value);
                RaiseCanExecuteChanged();
            }
        }

        public bool CanSaveCustomer => !HasErrors &&
            !string.IsNullOrWhiteSpace(CustomerName) &&
            !string.IsNullOrWhiteSpace(ContactNumber);
        #endregion

        #region Command
        public ICommand SaveCustomerCommand { get; }
        public ICommand LoadCustomerCommand { get; }
        public ICommand NewCustomerCommand { get; }
        public ICommand EditCustomerCommand { get; }
        #endregion

        #region Form Helpers
        private void CreateNewCustomer()
        {
            SelectedCustomer = null;

            CustomerId = 0;
            CustomerName = string.Empty;
            ContactNumber = string.Empty;
            BillingAddress = string.Empty;
            IsTaxRegistered = false;
            TaxRegistrationNumber = string.Empty;
            IsActive = true;

            ClearAllErrors();

            RaiseCanExecuteChanged();
        }
        private void SetEditMode(bool isEditing)
        {
            IsEditing = isEditing;

            if (IsEditing && SelectedCustomer != null)
            {
                CustomerId = SelectedCustomer.Id;
                CustomerName = SelectedCustomer.CustomerName;
                ContactNumber = SelectedCustomer.ContactNumber;
                BillingAddress = SelectedCustomer.BillingAddress;
                IsTaxRegistered = SelectedCustomer.IsTaxRegistered;
                TaxRegistrationNumber = SelectedCustomer.IsTaxRegistered
                    ? SelectedCustomer.TaxRegistrationNumber
                    : string.Empty;
                IsActive = SelectedCustomer.IsActive;

                RaiseCanExecuteChanged();
            }
        }
        private void RaiseCanExecuteChanged()
        {
            (SaveCustomerCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (EditCustomerCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
        #endregion

        #region Method
        private async Task LoadCustomerAsync()
        {
            try
            {
                CustomerList.Clear();
                var customers = await _customerRepository.GetAllAsync();

                foreach (var customer in customers)
                    CustomerList.Add(customer);

                _customerListView.Refresh();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load customer: {ex.Message}";
            }

        }
        private bool FilterCustomer(object item)
        {
            if (!(item is Customer customer))
                return false;

            var query = (SearchQuery ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(query))
                return true;

            return Contains(customer.CustomerName, query) ||
                   Contains(customer.ContactNumber, query);
        }

        private static bool Contains(string value, string query)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string NormalizeOptional(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private async Task SaveCustomerAsync()
        {
            var currentWindow = Application.Current.Windows.OfType<Window>().SingleOrDefault(w => w.IsActive);

            ValidateAll();

            if (HasErrors)
            {
                // Get the specific errors to see WHY it failed
                var errors = string.Join("\n", GetErrors(nameof(CustomerName)).Cast<string>()
                             .Concat(GetErrors(nameof(ContactNumber)).Cast<string>()));

                MessageBox.Show($"Please fix the following errors:\n{errors}", "Validation Failed");
                return;
            }

            try
            {
                if (IsEditing && SelectedCustomer != null)
                {
                    SelectedCustomer.CustomerName = CustomerName;
                    SelectedCustomer.ContactNumber = ContactNumber;
                    SelectedCustomer.BillingAddress = NormalizeOptional(BillingAddress);
                    SelectedCustomer.IsTaxRegistered = IsTaxRegistered;
                    SelectedCustomer.TaxRegistrationNumber = IsTaxRegistered
                        ? TaxRegistrationNumber
                        : string.Empty;
                    SelectedCustomer.IsActive = IsActive;

                    await _customerRepository.UpdateAsync(SelectedCustomer);
                    MessageBox.Show("Customer updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                    if (currentWindow != null && currentWindow.IsActive)
                    {
                        try
                        {
                            if (currentWindow.ShowActivated && currentWindow.WindowState != WindowState.Minimized)
                                currentWindow.DialogResult = true;
                        }
                        catch
                        {
                            // Ignore if not a dialog
                        }
                    }
                }
                else
                {
                    var newCustomer = new Customer
                    {
                        CustomerName = CustomerName,
                        ContactNumber = ContactNumber,
                        BillingAddress = NormalizeOptional(BillingAddress),
                        IsTaxRegistered = IsTaxRegistered,
                        TaxRegistrationNumber = IsTaxRegistered
                            ? TaxRegistrationNumber
                            : string.Empty,
                        CreditLimit = 0,
                        IsActive = IsActive,
                        CreatedBy = _userSessionService.CurrentUser.UserId,
                    };
                    await _customerRepository.CreateAsync(newCustomer);
                    MessageBox.Show("Customer saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                    if (currentWindow != null && currentWindow.IsActive)
                    {
                        try
                        {
                            if (currentWindow.ShowActivated && currentWindow.WindowState != WindowState.Minimized)
                                currentWindow.DialogResult = true;
                        }
                        catch
                        {
                            // Ignore if not a dialog
                        }
                    }
                }

                await LoadCustomerAsync();
                CreateNewCustomer();
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving customer: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Validation
        private void ValidateAll()
        {
            ValidateCustomerName();
            ValidateContactNumber();
        }
        private void ValidateCustomerName()
        {
            ClearErrors(nameof(CustomerName));
            if (string.IsNullOrWhiteSpace(CustomerName))
                AddError(nameof(CustomerName), "Customer name is required.");
            else if (!Regex.IsMatch(CustomerName, @"^[a-zA-Z\s\-\'\.]+$"))
            {
                AddError(nameof(CustomerName), "Invalid characters in name.");
            }
        }
        private void ValidateContactNumber()
        {
            ClearErrors(nameof(ContactNumber));
            if (string.IsNullOrWhiteSpace(ContactNumber))
                AddError(nameof(ContactNumber), "Contact number is required.");
            else if (!Regex.IsMatch(ContactNumber, @"^\d+$"))
            {
                AddError(nameof(ContactNumber), "Contact number must contain digits only.");
            }
            else if (ContactNumber.Length != 10)
            {
                AddError(nameof(ContactNumber), "Contact number must be 10 digits long.");
            }
        }
        #endregion

    }
}
