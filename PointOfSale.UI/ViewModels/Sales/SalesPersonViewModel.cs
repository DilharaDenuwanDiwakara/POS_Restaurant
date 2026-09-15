using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using PointOfSale.Core.Interfaces.Repositories.Sales;
using PointOfSale.Core.Interfaces.Services;
using PointOfSale.Core.Models.Sales;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Sales
{
    public class SalesPersonViewModel : BaseViewModel
    {
        private readonly ISalesPersonRepository _repository;
        private readonly IUserSessionService _userSession;
        private readonly IDialogService _dialogService;
        private readonly ICollectionView _listView;

        public SalesPersonViewModel(ISalesPersonRepository repository,
            IUserSessionService userSession, IDialogService dialogService)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _userSession = userSession ?? throw new ArgumentNullException(nameof(userSession));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _listView = CollectionViewSource.GetDefaultView(SalesPersonList);
            _listView.Filter = FilterSalesPerson;
            SaveCommand = new AsyncRelayCommand(async _ => await SaveAsync(), _ => CanSave);
            LoadCommand = new AsyncRelayCommand(async _ => await LoadAsync(), _ => IsFormEnabled);
            EditCommand = new RelayCommand(_ => EditSelected(), _ => IsFormEnabled && SelectedSalesPerson != null);
            NewCommand = new RelayCommand(_ => CreateNew(), _ => IsFormEnabled);
            _ = LoadAsync();
        }

        public ObservableCollection<SalesPerson> SalesPersonList { get; } = new ObservableCollection<SalesPerson>();
        public ICommand SaveCommand { get; }
        public ICommand LoadCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand NewCommand { get; }

        private SalesPerson _selectedSalesPerson;
        public SalesPerson SelectedSalesPerson
        {
            get => _selectedSalesPerson;
            set { if (SetProperty(ref _selectedSalesPerson, value)) RaiseCanExecuteChanged(); }
        }

        private string _searchQuery;
        public string SearchQuery
        {
            get => _searchQuery;
            set { if (SetProperty(ref _searchQuery, value)) _listView.Refresh(); }
        }

        private int _id;
        public int Id { get => _id; private set => SetProperty(ref _id, value); }
        private bool _isEditing;
        public bool IsEditing
        {
            get => _isEditing;
            private set { if (SetProperty(ref _isEditing, value)) OnPropertyChanged(nameof(SaveButtonText)); }
        }
        public string SaveButtonText => IsEditing ? "Update" : "Save";
        private bool _isBusy;
        public bool IsFormEnabled => !_isBusy;
        public bool CanSave => IsFormEnabled && !HasErrors &&
            !string.IsNullOrWhiteSpace(Name) &&
            !string.IsNullOrWhiteSpace(NIC) && !string.IsNullOrWhiteSpace(ContactNo);

        private string _code;
        public string Code
        {
            get => _code;
            private set => SetProperty(ref _code, value);
        }
        private string _name;
        public string Name
        {
            get => _name;
            set { if (SetProperty(ref _name, value)) { ValidateName(); RaiseCanExecuteChanged(); } }
        }
        private string _email;
        public string Email
        {
            get => _email;
            set { if (SetProperty(ref _email, value)) { ValidateEmail(); RaiseCanExecuteChanged(); } }
        }
        private string _nic;
        public string NIC
        {
            get => _nic;
            set { if (SetProperty(ref _nic, value)) { ValidateRequiredText(nameof(NIC), value, 20, "NIC"); RaiseCanExecuteChanged(); } }
        }
        private string _contactNo;
        public string ContactNo
        {
            get => _contactNo;
            set { if (SetProperty(ref _contactNo, value)) { ValidateRequiredText(nameof(ContactNo), value, 15, "Contact number"); RaiseCanExecuteChanged(); } }
        }
        private string _address;
        public string Address
        {
            get => _address;
            set { if (SetProperty(ref _address, value)) { ValidateLength(nameof(Address), value, 255); RaiseCanExecuteChanged(); } }
        }
        private bool _isActive = true;
        public bool IsActive { get => _isActive; set => SetProperty(ref _isActive, value); }

        private bool FilterSalesPerson(object item)
        {
            var person = item as SalesPerson;
            if (person == null) return false;
            var query = (SearchQuery ?? string.Empty).Trim();
            return query.Length == 0 || new[] { person.Code, person.Name, person.Email, person.NIC, person.ContactNo }
                .Any(value => value != null && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void EditSelected()
        {
            var person = SelectedSalesPerson;
            if (person == null) return;
            Id = person.Id;
            Code = person.Code;
            Name = person.Name;
            Email = person.Email;
            NIC = person.NIC;
            ContactNo = person.ContactNo;
            Address = person.Address;
            IsActive = person.IsActive;
            IsEditing = true;
            RaiseCanExecuteChanged();
        }

        private void CreateNew()
        {
            SelectedSalesPerson = null;
            IsEditing = false;
            Id = 0;
            Code = Name = Email = NIC = ContactNo = Address = string.Empty;
            IsActive = true;
            ClearAllErrors();
            RaiseCanExecuteChanged();
        }

        private async Task ReloadListAsync()
        {
            var people = await _repository.GetAllAsync();
            SalesPersonList.Clear();
            foreach (var person in people.OrderByDescending(x => x.Id)) SalesPersonList.Add(person);
        }

        private async Task LoadAsync()
        {
            SetBusy(true);
            try { await ReloadListAsync(); }
            catch (Exception ex) { _dialogService.ShowMessage($"Failed to load sales people: {ex.Message}", "Error", DialogMessageType.Error); }
            finally { SetBusy(false); }
        }

        private async Task SaveAsync()
        {
            ValidateName();
            ValidateEmail();
            ValidateRequiredText(nameof(NIC), NIC, 20, "NIC");
            ValidateRequiredText(nameof(ContactNo), ContactNo, 15, "Contact number");
            ValidateLength(nameof(Address), Address, 255);
            if (!CanSave) return;

            var person = new SalesPerson
            {
                Id = Id,
                Code = Normalize(Code),
                Name = Normalize(Name),
                Email = Normalize(Email),
                NIC = Normalize(NIC),
                ContactNo = Normalize(ContactNo),
                Address = Normalize(Address),
                IsActive = IsActive,
                CreateBy = _userSession.UserId,
                UpdateBy = IsEditing ? (int?)_userSession.UserId : null
            };
            var wasEditing = IsEditing;
            SetBusy(true);
            try
            {
                if (wasEditing) await _repository.UpdateAsync(person);
                else await _repository.CreateAsync(person);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Failed to save sales person: {ex.Message}", "Error", DialogMessageType.Error);
                SetBusy(false);
                return;
            }

            // Clear only after persistence succeeds, even if refreshing the list fails.
            CreateNew();
            _dialogService.ShowMessage(wasEditing ? "Sales person updated successfully." : "Sales person saved successfully.", "Success");
            try { await ReloadListAsync(); }
            catch (Exception ex) { _dialogService.ShowMessage($"Sales person was saved, but the list could not be refreshed: {ex.Message}", "Error", DialogMessageType.Error); }
            finally { SetBusy(false); }
        }

        private static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        private void ValidateLength(string property, string value, int maximumLength)
        {
            ClearErrors(property);
            if ((value?.Trim().Length ?? 0) > maximumLength)
                AddError(property, $"Maximum length is {maximumLength} characters.");
        }
        private void ValidateRequiredText(string property, string value, int maximumLength, string label)
        {
            ValidateLength(property, value, maximumLength);
            if (string.IsNullOrWhiteSpace(value)) AddError(property, $"{label} is required.");
        }
        private void ValidateName()
        {
            ValidateRequiredText(nameof(Name), Name, 100, "Name");
        }
        private void ValidateEmail()
        {
            ValidateLength(nameof(Email), Email, 150);
            if (string.IsNullOrWhiteSpace(Email)) return;
            try
            {
                if (!string.Equals(new MailAddress(Email.Trim()).Address, Email.Trim(), StringComparison.OrdinalIgnoreCase))
                    AddError(nameof(Email), "Enter a valid email address.");
            }
            catch (FormatException) { AddError(nameof(Email), "Enter a valid email address."); }
        }
        private void SetBusy(bool value)
        {
            _isBusy = value;
            OnPropertyChanged(nameof(IsFormEnabled));
            RaiseCanExecuteChanged();
        }
        private void RaiseCanExecuteChanged()
        {
            (SaveCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (LoadCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (EditCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (NewCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }
}
