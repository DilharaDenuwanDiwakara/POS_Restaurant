using System;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using PointOfSale.Core.Interfaces.Repositories.Restaurant;
using PointOfSale.Core.Models.Restaurant;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Restaurant
{
    public class TableViewModel : BaseViewModel
    {
        private readonly ITableRepository _tableRepository;
        private readonly IUserSessionService _userSessionService;

        public TableViewModel(ITableRepository tableRepository, IUserSessionService userSessionService)
        {
            _tableRepository = tableRepository ?? throw new ArgumentNullException(nameof(tableRepository));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));

            // Initialize Commands
            SaveCommand = new AsyncRelayCommand(async _ => await SaveTableAsync(), _ => CanSaveTable);
            RefreshCommand = new AsyncRelayCommand(async _ => await LoadTablesAsync());

            EditCommand = new RelayCommand(
                execute: _ => ExecuteEditCommand(),
                canExecute: _ => SelectedTable != null && CanEditTable
            );
            NewTableCommand = new RelayCommand(_ => CreateNewTable());
            DeleteCommand = new AsyncRelayCommand(async _ => await DeleteTableAsync(), _ => SelectedTable != null);

            // Load Initial Data
            _ = LoadTablesAsync();
        }

        #region Permissions
        public bool CanCreateTable => _userSessionService.HasPermission("RESTAURANT_TABLE_CREATE");
        public bool CanEditTable => _userSessionService.HasPermission("RESTAURANT_TABLE_EDIT");
        public bool CanDeleteTable => _userSessionService.HasPermission("RESTAURANT_TABLE_DELETE");
        #endregion

        #region Properties

        public ObservableCollection<Table> TableList { get; } = new ObservableCollection<Table>();

        private Table _selectedTable;
        public Table SelectedTable
        {
            get => _selectedTable;
            set
            {
                if (SetProperty(ref _selectedTable, value))
                {
                    SetEditMode(false);
                    RaiseCanExecuteChanged();
                }
            }
        }

        public string SaveButtonText => IsEditing ? "Update" : "Save";

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

        private int _tableId;
        public int TableId
        {
            get => _tableId;
            set => SetProperty(ref _tableId, value);
        }

        private string _tableName;
        public string TableName
        {
            get => _tableName;
            set
            {
                SetProperty(ref _tableName, value);
                ValidateTableName();
                RaiseCanExecuteChanged();
            }
        }

        private int _capacity = 4; // Default to 4 seats
        public int Capacity
        {
            get => _capacity;
            set
            {
                SetProperty(ref _capacity, value);
                ValidateCapacity();
                RaiseCanExecuteChanged();
            }
        }

        private string _currentStatus = "AVAILABLE";
        public string CurrentStatus
        {
            get => _currentStatus;
            set => SetProperty(ref _currentStatus, value);
        }

        public bool CanSaveTable => !HasErrors &&
            !string.IsNullOrWhiteSpace(TableName) &&
            Capacity > 0;

        #endregion

        #region Commands
        public ICommand SaveCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand NewTableCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }
        #endregion

        #region Form Helpers

        private void CreateNewTable()
        {
            SelectedTable = null;

            TableId = 0;
            TableName = string.Empty;
            Capacity = 4; // Reset to default
            CurrentStatus = "Available";
            IsEditing = false;

            ClearErrors(nameof(TableName));
            ClearErrors(nameof(Capacity));

            RaiseCanExecuteChanged();
        }

        private void ExecuteEditCommand()
        {
            // Layer 2: Hard Security Check (Fail-Fast)
            if (!CanEditTable)
            {
                MessageBox.Show("You do not have permission to edit table configurations.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SetEditMode(true);
        }
        private void SetEditMode(bool isEditing)
        {
            IsEditing = isEditing;

            if (IsEditing && SelectedTable != null)
            {
                TableId = SelectedTable.Id;
                TableName = SelectedTable.Name;
                Capacity = SelectedTable.Capacity;
                CurrentStatus = SelectedTable.CurrentStatus;

                RaiseCanExecuteChanged();
            }
        }

        private void RaiseCanExecuteChanged()
        {
            (SaveCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (EditCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (DeleteCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }

        #endregion

        #region Methods
        private async Task LoadTablesAsync()
        {
            try
            {
                var branchId = _userSessionService.BranchId;
                TableList.Clear();

                // Assuming your repository has GetByBranchAsync logic
                var tables = await _tableRepository.GetAllAsync(branchId);

                foreach (var table in tables)
                    TableList.Add(table);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load tables: {ex.Message}";
            }
        }
        private async Task SaveTableAsync()
        {
            ValidateAll();

            if (HasErrors) // Note: BaseViewModel usually exposes 'HasErrors' (plural)
            {
                MessageBox.Show("Please correct the highlighted errors before saving.");
                return;
            }
            if (IsEditing && !CanEditTable)
            {
                MessageBox.Show("You do not have permission to modify existing tables.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!IsEditing && !CanCreateTable)
            {
                MessageBox.Show("You do not have permission to create new tables.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                if (IsEditing && SelectedTable != null)
                {
                    // Update Existing
                    SelectedTable.BranchId = _userSessionService.BranchId;
                    SelectedTable.Name = TableName;
                    SelectedTable.Capacity = Capacity;

                    await _tableRepository.UpdateAsync(SelectedTable);
                    MessageBox.Show("Table updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // Create New
                    var newTable = new Table
                    {
                        BranchId = _userSessionService.BranchId,
                        Name = TableName,
                        Capacity = Capacity,
                        CurrentStatus = "Available" // Default status
                    };

                    await _tableRepository.CreateAsync(newTable);
                    MessageBox.Show("Table created successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                await LoadTablesAsync();
                CreateNewTable();
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving table: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async Task DeleteTableAsync()
        {
            if (!CanDeleteTable)
            {
                MessageBox.Show("You do not have permission to delete tables.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SelectedTable == null) return;

            var result = MessageBox.Show($"Are you sure you want to delete table '{SelectedTable.Name}'?",
                                         "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    //await _tableRepository.DeleteAsync(SelectedTable.Id);
                    await LoadTablesAsync();
                    CreateNewTable();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not delete table. It might be in use.\nError: {ex.Message}", "Delete Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        #endregion

        #region Validation
        private void ValidateAll()
        {
            ValidateTableName();
            ValidateCapacity();
        }
        private void ValidateTableName()
        {
            ClearErrors(nameof(TableName));
            if (string.IsNullOrWhiteSpace(TableName))
                AddError(nameof(TableName), "Table Name is required.");
            else if (!Regex.IsMatch(TableName, @"^[a-zA-Z-0-9_-]+$"))
                AddError(nameof(TableName), "Cannot contain special characters.");
        }
        private void ValidateCapacity()
        {
            ClearErrors(nameof(Capacity));
            if (Capacity <= 0)
            {
                AddError(nameof(Capacity), "Capacity must be greater than 0.");
            }
        }
        #endregion
    }
}
