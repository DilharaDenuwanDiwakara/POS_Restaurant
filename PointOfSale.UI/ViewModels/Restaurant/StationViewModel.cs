using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using PointOfSale.Core.Interfaces.Repositories.Restaurant;
using PointOfSale.Core.Models.Restaurant;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Restaurant
{
    public class StationViewModel : BaseViewModel
    {
        private readonly IStationRepository _stationRepository;
        private readonly IUserSessionService _userSessionService;
        public StationViewModel(IStationRepository stationRepository, IUserSessionService sessionService)
        {
            _stationRepository = stationRepository ?? throw new ArgumentNullException(nameof(stationRepository));
            _userSessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));

            // Initialize Commands
            SaveCommand = new AsyncRelayCommand(async _ => await SaveStationAsync(), _ => CanSaveStation);
            RefreshCommand = new AsyncRelayCommand(async _ => await LoadStationsAsync());
            EditCommand = new RelayCommand(_ => SetEditMode(true), _ => SelectedStation != null);
            NewStationCommand = new RelayCommand(_ => CreateNewStation());

            // Load Data
            _ = LoadStationsAsync();
        }

        #region Properties
        public List<Station> AddedStation { get; private set; } = new List<Station>();

        public ObservableCollection<Station> StationList { get; } = new ObservableCollection<Station>();

        private Station _selectedStation;
        public Station SelectedStation
        {
            get => _selectedStation;
            set
            {
                if (SetProperty(ref _selectedStation, value))
                {
                    SetEditMode(false); // When selection changes, exit edit mode initially
                    RaiseCanExecuteChanged();
                }
            }
        }

        public string SaveButtonText => IsEditing ? "Update Station" : "Save Station";

        private bool _isEditing;
        public bool IsEditing
        {
            get => _isEditing;
            set
            {
                if (SetProperty(ref _isEditing, value))
                {
                    OnPropertyChanged(nameof(SaveButtonText));
                    // Optional: Trigger UI visual state changes
                }
            }
        }

        // --- Form Fields ---

        private int _stationId;
        public int StationId
        {
            get => _stationId;
            set => SetProperty(ref _stationId, value);
        }

        private string _stationName;
        public string StationName
        {
            get => _stationName;
            set
            {
                if (SetProperty(ref _stationName, value))
                {
                    ValidateStationName();
                    RaiseCanExecuteChanged();
                }
            }
        }

        private string _printerName;
        public string PrinterName
        {
            get => _printerName;
            set
            {
                if (SetProperty(ref _printerName, value))
                {
                    // Optional validation for Printer Name
                    RaiseCanExecuteChanged();
                }
            }
        }

        private string _printerIp;
        public string PrinterIp
        {
            get => _printerIp;
            set
            {
                if (SetProperty(ref _printerIp, value))
                {
                    ValidateIpAddress();
                    RaiseCanExecuteChanged();
                }
            }
        }

        public bool CanSaveStation => !HasErrors && !string.IsNullOrWhiteSpace(StationName);
        #endregion

        #region Commands
        public ICommand SaveCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand NewStationCommand { get; } // Clears form for new entry
        public ICommand EditCommand { get; }       // Populates form for editing
        public ICommand DeleteCommand { get; }
        #endregion

        #region Methods
        private async Task LoadStationsAsync()
        {
            try
            {
                var branchId = _userSessionService.BranchId;
                StationList.Clear();

                var stations = await _stationRepository.GetAllAsync(branchId);

                foreach (var s in stations)
                {
                    StationList.Add(s);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load stations: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SaveStationAsync()
        {
            ValidateAll();

            if (HasErrors)
            {
                MessageBox.Show("Please correct the errors before saving.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (IsEditing && StationId > 0)
                {
                    // UPDATE Logic
                    var stationToUpdate = new Station
                    {
                        Id = StationId,
                        BranchId = _userSessionService.BranchId,
                        Name = StationName,
                        PrinterName = PrinterName,
                        PrinterIP = PrinterIp
                    };

                    await _stationRepository.UpdateAsync(stationToUpdate);
                    MessageBox.Show("Station updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // CREATE Logic
                    var newStation = new Station
                    {
                        BranchId = _userSessionService.BranchId,
                        Name = StationName,
                        PrinterName = PrinterName,
                        PrinterIP = PrinterIp
                    };

                    int newId = await _stationRepository.CreateAsync(newStation);

                    newStation.Id = newId;

                    AddedStation.Add(newStation); // Keep track of newly added stations in the session if needed
                    MessageBox.Show("Station created successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                // Refresh List & Reset Form
                await LoadStationsAsync();
                CreateNewStation();
            }
            catch (InvalidOperationException ex) // Catch specific domain errors (e.g. duplicate name)
            {
                MessageBox.Show(ex.Message, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving station: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Form Helpers

        private void CreateNewStation()
        {
            SelectedStation = null; // Deselect grid item

            StationId = 0;
            StationName = string.Empty;
            PrinterName = string.Empty;
            PrinterIp = string.Empty;

            IsEditing = false;

            ClearErrors(nameof(StationName));
            ClearErrors(nameof(PrinterIp));

            RaiseCanExecuteChanged();
        }

        private void SetEditMode(bool isEditing)
        {
            IsEditing = isEditing;

            if (IsEditing && SelectedStation != null)
            {
                // Map Selected Item to Form Fields
                StationId = SelectedStation.Id;
                StationName = SelectedStation.Name;
                PrinterName = SelectedStation.PrinterName;
                PrinterIp = SelectedStation.PrinterIP;

                RaiseCanExecuteChanged();
            }
            else if (!isEditing)
            {
                // If exiting edit mode (e.g. just selecting a row), clear the form or keep it read-only
                // This logic depends on your UI UX preference. 
                // Often, selecting a row just shows details, clicking "Edit" enables the fields.
            }
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
            ValidateStationName();
            ValidateIpAddress();
        }

        private void ValidateStationName()
        {
            ClearErrors(nameof(StationName));
            if (string.IsNullOrWhiteSpace(StationName))
            {
                AddError(nameof(StationName), "Station Name is required.");
            }
            else if (!System.Text.RegularExpressions.Regex.IsMatch(StationName, @"^[a-zA-Z0-9\s\-]+$"))
                AddError(nameof(StationName), "Special characters are not allowed.");
        }

        private void ValidateIpAddress()
        {
            ClearErrors(nameof(PrinterIp));
            if (!string.IsNullOrWhiteSpace(PrinterIp))
            {
                // Simple IPv4 regex check
                string pattern = @"^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$";
                if (!System.Text.RegularExpressions.Regex.IsMatch(PrinterIp, pattern))
                {
                    AddError(nameof(PrinterIp), "Invalid IP Address format.");
                }
            }
        }
        #endregion
    }
}
