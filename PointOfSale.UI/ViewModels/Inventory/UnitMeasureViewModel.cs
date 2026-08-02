using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using PointOfSale.Core.Interfaces.Repositories.Inventory;
using PointOfSale.Core.Models.Inventory;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Inventory
{
    public class UnitMeasureViewModel : BaseViewModel
    {
        private readonly IUnitMeasureRepository _unitMeasureRepository;
        public UnitMeasureViewModel(IUnitMeasureRepository unitMeasureRepository)
        {
            _unitMeasureRepository = unitMeasureRepository ?? throw new ArgumentNullException(nameof(unitMeasureRepository));

            // Initialize commands
            SaveUnitMeasureCommand = new AsyncRelayCommand(async param => await SaveUnitMeasureAsync(param), _ => CanSaveUnitMeasure);
            LoadUnitMeasureCommand = new AsyncRelayCommand(async _ => await LoadUnitMeasureAsync());
            NewUnitMeasureCommand = new RelayCommand(_ => CreateNewUnitMeasure());
            EditUnitMeasureCommand = new RelayCommand(_ => SetEditMode(true), _ => SelectedUnitMeasure != null);

            _ = LoadUnitMeasureAsync();
        }

        #region Properties
        public ObservableCollection<UnitMeasure> UnitMeasureList { get; } = new ObservableCollection<UnitMeasure>();

        public List<UnitMeasure> AddedUnitMeasures { get; private set; } = new List<UnitMeasure>();

        public UnitMeasure ResultData { get; private set; }

        private UnitMeasure _selectedUnitMeasure;
        public UnitMeasure SelectedUnitMeasure
        {
            get => _selectedUnitMeasure;
            set
            {
                if (SetProperty(ref _selectedUnitMeasure, value))
                {
                    SetEditMode(false);
                    RaiseCanExecuteChanged();
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

        private int _unitMeasureId;
        public int UnitMeasureId
        {
            get => _unitMeasureId;
            set => SetProperty(ref _unitMeasureId, value);
        }

        private string _unitMeasureCode;
        public string UnitMeasureCode
        {
            get => _unitMeasureCode;
            set
            {
                SetProperty(ref _unitMeasureCode, value);
                ValidateUnitMeasureCode();
                RaiseCanExecuteChanged();
            }
        }

        private string _unitMeasureName;
        public string UnitMeasureName
        {
            get => _unitMeasureName;
            set
            {
                SetProperty(ref _unitMeasureName, value);
                ValidateUnitMeasureName();
                RaiseCanExecuteChanged();
            }
        }

        public bool CanSaveUnitMeasure => !HasErrors &&
           !string.IsNullOrWhiteSpace(UnitMeasureCode) &&
           !string.IsNullOrWhiteSpace(UnitMeasureName);
        #endregion

        #region Commands
        public ICommand SaveUnitMeasureCommand { get; }
        public ICommand LoadUnitMeasureCommand { get; }
        public ICommand NewUnitMeasureCommand { get; }
        public ICommand EditUnitMeasureCommand { get; }
        #endregion

        #region CRUD Methods
        private async Task LoadUnitMeasureAsync()
        {
            try
            {
                UnitMeasureList.Clear();
                var unitMeasures = await _unitMeasureRepository.GetAllAsync();

                foreach (var unitMeasure in unitMeasures)
                    UnitMeasureList.Add(unitMeasure);

                ErrorMessage = null;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load unit Measure: {ex.Message}";
            }

        }
        private async Task SaveUnitMeasureAsync(object parameter)
        {
            var window = parameter as Window;

            ValidateAll();

            if (HasErrors) return;

            try
            {
                if (IsEditing && SelectedUnitMeasure != null)
                {
                    SelectedUnitMeasure.Code = UnitMeasureCode;
                    SelectedUnitMeasure.UnitMeasureName = UnitMeasureName;

                    await _unitMeasureRepository.UpdateAsync(SelectedUnitMeasure);
                    MessageBox.Show("Unit Measure updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                    ResultData = SelectedUnitMeasure;
                }
                else
                {
                    var newUnitMeasure = new UnitMeasure
                    {
                        Code = UnitMeasureCode,
                        UnitMeasureName = UnitMeasureName
                    };
                    await _unitMeasureRepository.CreateAsync(newUnitMeasure);
                    MessageBox.Show("Unit Measure created successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                    AddedUnitMeasures.Add(newUnitMeasure);
                }

                await LoadUnitMeasureAsync();
                CreateNewUnitMeasure();
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving brand: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Form Helpers
        private void CreateNewUnitMeasure()
        {
            SelectedUnitMeasure = null;

            UnitMeasureId = 0;
            UnitMeasureCode = string.Empty;
            UnitMeasureName = string.Empty;

            ClearErrors(nameof(UnitMeasureCode));
            ClearErrors(nameof(UnitMeasureName));

            ErrorMessage = null;

            RaiseCanExecuteChanged();
        }
        private void SetEditMode(bool isEditing)
        {
            IsEditing = isEditing;

            if (isEditing && SelectedUnitMeasure != null)
            {
                UnitMeasureId = SelectedUnitMeasure.UnitMeasureId;
                UnitMeasureCode = SelectedUnitMeasure.Code;
                UnitMeasureName = SelectedUnitMeasure.UnitMeasureName;
            }
        }
        private void RaiseCanExecuteChanged()
        {
            (SaveUnitMeasureCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (EditUnitMeasureCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
        #endregion

        #region Validation
        private void ValidateAll()
        {
            ValidateUnitMeasureCode();
            ValidateUnitMeasureName();
        }
        private void ValidateUnitMeasureCode()
        {
            ClearErrors(nameof(UnitMeasureCode));
            if (string.IsNullOrWhiteSpace(UnitMeasureCode))
                AddError(nameof(UnitMeasureCode), "Code is required.");
            else if (!Regex.IsMatch(UnitMeasureCode, @"^[a-zA-Z0-9\s-]+$"))
                AddError(nameof(UnitMeasureCode), "Cannot contain numbers or special characters.");
        }
        private void ValidateUnitMeasureName()
        {
            ClearErrors(nameof(UnitMeasureName));
            if (string.IsNullOrWhiteSpace(UnitMeasureName))
                AddError(nameof(UnitMeasureName), "Name is required.");
            else if (!Regex.IsMatch(UnitMeasureName, @"^[a-zA-Z0-9 ()\s]+$"))
                AddError(nameof(UnitMeasureName), "Cannot contain numbers or special characters.");
        }
        #endregion
    }
}
