using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using PointOfSale.Core.Interfaces.Security;
using PointOfSale.Core.Models.Security;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Security
{
    public class UserProfileViewModel : BaseViewModel
    {
        private readonly IUserRepository _userRepository;
        private readonly IUserSessionService _userSessionService;
        private readonly IPasswordHasher _passwordHasher;
        public UserProfileViewModel(IUserRepository userRepository,
            IUserSessionService sessionService,
            IPasswordHasher passwordHasher)
        {
            _userRepository = userRepository;
            _userSessionService = sessionService;
            _passwordHasher = passwordHasher;

            CurrentUser = _userSessionService.CurrentUser;

            ChangePasswordCommand = new AsyncRelayCommand(async _ => await ChangePasswordAsync(), _ => CanChangePassword);

        }

        #region Properties

        // 1. Profile Data (Read Only for now)
        public User CurrentUser { get; }

        private int _selectedTabIndex;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetProperty(ref _selectedTabIndex, value);
        }

        // 2. Change Password Fields
        private string _currentPassword;
        public string CurrentPassword
        {
            get => _currentPassword;
            set
            {
                if (SetProperty(ref _currentPassword, value))
                    RaiseCanExecuteChanged();
            }
        }

        private string _newPassword;
        public string NewPassword
        {
            get => _newPassword;
            set
            {
                if (SetProperty(ref _newPassword, value))
                {
                    ValidatePassword();
                    RaiseCanExecuteChanged();
                }
            }
        }

        private string _confirmPassword;
        public string ConfirmPassword
        {
            get => _confirmPassword;
            set
            {
                if (SetProperty(ref _confirmPassword, value))
                {
                    ValidatePassword();
                    RaiseCanExecuteChanged();
                }
            }
        }

        public bool CanChangePassword =>
            !string.IsNullOrEmpty(CurrentPassword) &&
            !string.IsNullOrEmpty(NewPassword) &&
            !HasErrors;

        #endregion

        #region Commands
        public ICommand ChangePasswordCommand { get; }
        #endregion

        #region Methods

        private async Task ChangePasswordAsync()
        {
            if (NewPassword != ConfirmPassword)
            {
                MessageBox.Show("New passwords do not match.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                bool isCurrentPasswordCorrect = _passwordHasher.VerifyPassword(CurrentPassword, CurrentUser.PasswordHash);

                if (!isCurrentPasswordCorrect)
                {
                    MessageBox.Show("The current password you entered is incorrect.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string newHash = _passwordHasher.HashPassword(NewPassword);

                await _userRepository.ChangePasswordAsync(CurrentUser.UserId, newHash);

                MessageBox.Show("Password changed successfully! Please login again.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                _userSessionService.ClearSession();

                // Clear fields
                CurrentPassword = string.Empty;
                NewPassword = string.Empty;
                ConfirmPassword = string.Empty;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to change password: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ValidatePassword()
        {
            ClearErrors(nameof(NewPassword));
            ClearErrors(nameof(ConfirmPassword));

            if (!string.IsNullOrEmpty(NewPassword) && NewPassword.Length < 6)
                AddError(nameof(NewPassword), "Password must be at least 6 characters.");

            if (!string.IsNullOrEmpty(ConfirmPassword) && NewPassword != ConfirmPassword)
                AddError(nameof(ConfirmPassword), "Passwords do not match.");
        }

        private void CloseWindow(object window)
        {
            (window as Window)?.Close();
        }

        private void RaiseCanExecuteChanged()
        {
            (ChangePasswordCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }

        #endregion
    }
}
