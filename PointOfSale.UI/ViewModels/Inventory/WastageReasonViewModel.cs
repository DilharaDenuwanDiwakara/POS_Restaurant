using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using PointOfSale.Core.Interfaces.Repositories.Inventory;
using PointOfSale.Core.Models.Inventory;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Inventory
{
    public class WastageReasonViewModel : BaseViewModel
    {
        private readonly IWastageReasonRepository _repository;

        public WastageReasonViewModel(IWastageReasonRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));

            // Initialize commands
            SaveReasonCommand = new AsyncRelayCommand(async param => await SaveReasonAsync(param), _ => CanSaveReason);
            EditReasonCommand = new RelayCommand(_ => SetEditMode(true), _ => SelectedReason != null);
            DeleteReasonCommand = new AsyncRelayCommand(async _ => await DeleteReasonAsync(), _ => SelectedReason != null);

            _ = LoadReasonsAsync();
        }

        #region Properties
        public List<WastageReason> AddedReason { get; private set; } = new List<WastageReason>();
        public ObservableCollection<WastageReason> ReasonList { get; } = new ObservableCollection<WastageReason>();

        private WastageReason _selectedReason;
        public WastageReason SelectedReason
        {
            get => _selectedReason;
            set
            {
                if (SetProperty(ref _selectedReason, value))
                {
                    SetEditMode(false);
                    RaiseCanExecuteChanged();
                }
            }
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

        private int _wastageReasonId;
        public int WastageReasonId
        {
            get => _wastageReasonId;
            set => SetProperty(ref _wastageReasonId, value);
        }

        private string _reasonName;
        public string ReasonName
        {
            get => _reasonName;
            set
            {
                SetProperty(ref _reasonName, value);
                ValidateReasonName();
                RaiseCanExecuteChanged();
            }
        }

        // Optional: To display messages/errors on UI if you have an ErrorMessage property in Base
        private string _errorMessage;
        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        #endregion

        #region Commands
        public ICommand SaveReasonCommand { get; }
        public ICommand EditReasonCommand { get; }
        public ICommand DeleteReasonCommand { get; }
        #endregion

        #region CRUD Methods

        private async Task LoadReasonsAsync()
        {
            try
            {
                ReasonList.Clear();
                var reasons = await _repository.GetAllAsync();

                // Assuming you want newest first
                var sorted = reasons.OrderByDescending(r => r.Id);

                foreach (var r in sorted)
                    ReasonList.Add(r);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load reasons: {ex.Message}";
            }
        }

        private async Task SaveReasonAsync(object parameter)


        {
            ValidateAll();
            if (HasErrors) return;

            try
            {
                if (IsEditing && SelectedReason != null)
                {
                    // Update
                    SelectedReason.Reason = ReasonName;
                    await _repository.UpdateAsync(SelectedReason);
                    MessageBox.Show("Reason updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // Create
                    var newReason = new WastageReason
                    {
                        Reason = ReasonName
                    };

                    int newId = await _repository.CreateAsync(newReason);
                    newReason.Id = newId;

                    MessageBox.Show("Reason created successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                    AddedReason.Add(newReason);
                }

                await LoadReasonsAsync();
                CreateNewReason(); // Reset form
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving reason: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task DeleteReasonAsync()
        {
            if (SelectedReason == null) return;

            try
            {
                var result = MessageBox.Show(
                    "Are you sure you want to delete this reason?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    await _repository.DeleteAsync(SelectedReason.Id);
                    await LoadReasonsAsync();
                    CreateNewReason();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error deleting reason: {ex.Message}";
                MessageBox.Show(ErrorMessage);
            }
        }

        #endregion

        #region Helper Methods

        private void CreateNewReason()
        {
            SelectedReason = null;
            WastageReasonId = 0;
            ReasonName = string.Empty;
            IsEditing = false;

            ClearErrors(nameof(ReasonName));
            RaiseCanExecuteChanged();
        }

        private void SetEditMode(bool isEditing)
        {
            IsEditing = isEditing;
            if (isEditing && SelectedReason != null)
            {
                WastageReasonId = SelectedReason.Id;
                ReasonName = SelectedReason.Reason;
            }
        }

        private void RaiseCanExecuteChanged()
        {
            (SaveReasonCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (EditReasonCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (DeleteReasonCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }

        public bool CanSaveReason => !HasErrors && !string.IsNullOrWhiteSpace(ReasonName);

        #endregion

        #region Validation

        private void ValidateAll()
        {
            ValidateReasonName();
        }

        private void ValidateReasonName()
        {
            ClearErrors(nameof(ReasonName));
            if (string.IsNullOrWhiteSpace(ReasonName))
                AddError(nameof(ReasonName), "Reason is required.");
            else if (!Regex.IsMatch(ReasonName, @"^[a-zA-Z0-9\s/]+$"))
                AddError(nameof(ReasonName), "Cannot contain special characters");
        }

        #endregion
    }
}
