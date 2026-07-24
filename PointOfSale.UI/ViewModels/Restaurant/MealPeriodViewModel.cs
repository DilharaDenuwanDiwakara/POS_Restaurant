using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using PointOfSale.Core.Interfaces.Repositories.Restaurant;
using PointOfSale.Core.Models.Restaurant;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Restaurant
{
    public class MealPeriodViewModel : BaseViewModel, IDataErrorInfo
    {
        private readonly IMealPeriodRepository _mealPeriodRepository;

        public MealPeriodViewModel(IMealPeriodRepository mealPeriodRepository)
        {
            _mealPeriodRepository = mealPeriodRepository ?? throw new ArgumentNullException(nameof(mealPeriodRepository));

            SaveCommand = new AsyncRelayCommand(async _ => await SaveMealPeriodAsync(), _ => CanSaveMealPeriod);
            RefreshCommand = new AsyncRelayCommand(async _ => await LoadMealPeriodsAsync());
            EditCommand = new RelayCommand(_ => SetEditMode(true), _ => SelectedMealPeriod != null);
            NewMealPeriodCommand = new RelayCommand(_ => PrepareNewMealPeriod());

            _ = LoadMealPeriodsAsync();
        }

        public List<MealPeriod> AddedMealPeriods { get; private set; } = new List<MealPeriod>();

        public ObservableCollection<MealPeriod> MealPeriodList { get; } = new ObservableCollection<MealPeriod>();

        private MealPeriod _selectedMealPeriod;
        public MealPeriod SelectedMealPeriod
        {
            get => _selectedMealPeriod;
            set
            {
                if (SetProperty(ref _selectedMealPeriod, value))
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
                if (SetProperty(ref _isEditing, value))
                {
                    OnPropertyChanged(nameof(SaveButtonText));
                }
            }
        }

        private int _mealPeriodId;
        public int MealPeriodId
        {
            get => _mealPeriodId;
            set => SetProperty(ref _mealPeriodId, value);
        }

        private string _mealPeriodName;
        public string MealPeriodName
        {
            get => _mealPeriodName;
            set
            {
                if (SetProperty(ref _mealPeriodName, value))
                {
                    ValidateMealPeriodName();
                    RaiseCanExecuteChanged();
                }
            }
        }

        private string _startTimeText;
        public string StartTimeText
        {
            get => _startTimeText;
            set
            {
                if (SetProperty(ref _startTimeText, value))
                {
                    ValidateStartTimeText();
                    ValidateEndTimeText();
                    RaiseCanExecuteChanged();
                }
            }
        }

        private string _endTimeText;
        public string EndTimeText
        {
            get => _endTimeText;
            set
            {
                if (SetProperty(ref _endTimeText, value))
                {
                    ValidateEndTimeText();
                    RaiseCanExecuteChanged();
                }
            }
        }

        private bool _isActive = true;
        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        public bool CanSaveMealPeriod =>
            !HasErrors &&
            !string.IsNullOrWhiteSpace(MealPeriodName) &&
            !string.IsNullOrWhiteSpace(StartTimeText) &&
            !string.IsNullOrWhiteSpace(EndTimeText);

        string IDataErrorInfo.Error => null;

        string IDataErrorInfo.this[string columnName]
        {
            get
            {
                var errors = GetErrors(columnName);
                if (errors == null)
                {
                    return null;
                }

                foreach (var error in errors)
                {
                    return error?.ToString();
                }

                return null;
            }
        }

        public ICommand SaveCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand NewMealPeriodCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }

        private async Task LoadMealPeriodsAsync()
        {
            try
            {
                MealPeriodList.Clear();

                var mealPeriods = await _mealPeriodRepository.GetAllAsync();
                foreach (var mealPeriod in mealPeriods)
                {
                    MealPeriodList.Add(mealPeriod);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load meal periods: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SaveMealPeriodAsync()
        {
            ValidateAll();

            if (HasErrors)
            {
                MessageBox.Show("Please correct the errors before saving.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var mealPeriod = new MealPeriod
                {
                    Id = MealPeriodId,
                    Name = MealPeriodName,
                    DefaultStartTime = ParseTime(StartTimeText),
                    DefaultEndTime = ParseTime(EndTimeText),
                    IsActive = IsActive
                };

                if (IsEditing && MealPeriodId > 0)
                {
                    await _mealPeriodRepository.UpdateAsync(mealPeriod);
                    MessageBox.Show("Meal period updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    var newId = await _mealPeriodRepository.CreateAsync(mealPeriod);
                    mealPeriod.Id = newId;

                    AddedMealPeriods.Add(mealPeriod);
                    MessageBox.Show("Meal period created successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                await LoadMealPeriodsAsync();
                PrepareNewMealPeriod();
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving meal period: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void PrepareNewMealPeriod()
        {
            SelectedMealPeriod = null;
            MealPeriodId = 0;
            MealPeriodName = string.Empty;
            StartTimeText = string.Empty;
            EndTimeText = string.Empty;
            IsActive = true;
            IsEditing = false;

            ClearAllErrors();

            RaiseCanExecuteChanged();
        }

        private void SetEditMode(bool isEditing)
        {
            IsEditing = isEditing;

            if (IsEditing && SelectedMealPeriod != null)
            {
                MealPeriodId = SelectedMealPeriod.Id;
                MealPeriodName = SelectedMealPeriod.Name;
                StartTimeText = SelectedMealPeriod.DefaultStartTime.ToString(@"hh\:mm");
                EndTimeText = SelectedMealPeriod.DefaultEndTime.ToString(@"hh\:mm");
                IsActive = SelectedMealPeriod.IsActive;

                RaiseCanExecuteChanged();
            }
        }

        private void RaiseCanExecuteChanged()
        {
            (SaveCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (EditCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (DeleteCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }

        private void ValidateAll()
        {
            ValidateMealPeriodName();
            ValidateStartTimeText();
            ValidateEndTimeText();
        }

        private void ValidateMealPeriodName()
        {
            ClearErrors(nameof(MealPeriodName));
            if (string.IsNullOrWhiteSpace(MealPeriodName))
            {
                AddError(nameof(MealPeriodName), "Meal Period Name is required.");
            }
            else if (!Regex.IsMatch(MealPeriodName, @"^[a-zA-Z0-9\s]+$"))
            {
                AddError(nameof(MealPeriodName), "Special characters are not allowed.");
            }
        }

        private void ValidateStartTimeText()
        {
            ClearErrors(nameof(StartTimeText));

            TimeSpan startTime;
            if (!TryParseTime(StartTimeText, out startTime))
            {
                AddError(nameof(StartTimeText), "Start time must be 24-hour time in HH:mm format.");
            }
        }

        private void ValidateEndTimeText()
        {
            ClearErrors(nameof(EndTimeText));

            TimeSpan startTime;
            TimeSpan endTime;
            var isStartTimeValid = TryParseTime(StartTimeText, out startTime);
            var isEndTimeValid = TryParseTime(EndTimeText, out endTime);

            if (!isEndTimeValid)
            {
                AddError(nameof(EndTimeText), "End time must be 24-hour time in HH:mm format.");
                return;
            }

            if (isStartTimeValid && endTime == startTime)
            {
                AddError(nameof(EndTimeText), "End time must be different from start time.");
            }
        }

        private static bool TryParseTime(string value, out TimeSpan time)
        {
            DateTime parsedTime;
            var isValid = DateTime.TryParseExact(
                value,
                "HH:mm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out parsedTime);

            time = isValid ? parsedTime.TimeOfDay : TimeSpan.Zero;
            return isValid;
        }

        private static TimeSpan ParseTime(string value)
        {
            TimeSpan time;
            if (!TryParseTime(value, out time))
            {
                throw new InvalidOperationException("Meal period contains an invalid time value.");
            }

            return time;
        }
    }
}
