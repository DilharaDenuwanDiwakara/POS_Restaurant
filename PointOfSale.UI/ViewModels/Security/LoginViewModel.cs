using System;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Authentication;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Sales;
using PointOfSale.Core.Interfaces.Services;
using PointOfSale.Core.Models.Security;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;
using PointOfSale.UI.ViewModels.Sales;
using PointOfSale.UI.ViewModels.Shell;
using PointOfSale.UI.Views.Sales;
using PointOfSale.UI.Views.Security;
using PointOfSale.UI.Views.Shell;

namespace PointOfSale.UI.ViewModels.Security
{
    public class LoginViewModel : BaseViewModel
    {
        private readonly IAuthService _authService;
        private readonly IUserSessionService _userSessionService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IShiftRepository _shiftRepository;
        private readonly IDialogService _dialogService;

        public LoginViewModel(
            IAuthService authService,
            IUserSessionService userSessionService,
            IServiceProvider serviceProvider,
            IShiftRepository shiftRepository,
            IDialogService dialogService)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _shiftRepository = shiftRepository ?? throw new ArgumentNullException(nameof(shiftRepository));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

            // Initialize commands
            LoginCommand = new AsyncRelayCommand(async _ => await HandleLoginAsync(), _ => CanExecute);

        }

        #region Properties
        private string _username;
        public string Username
        {
            get => _username;
            set
            {
                if (SetProperty(ref _username, value))
                {
                    ValidateUsername();
                    RaiseCanExecuteChanged();
                }

            }
        }

        private string _password;
        public string Password
        {
            get => _password;
            set
            {
                if (SetProperty(ref _password, value))
                {
                    ValidatePassword();
                    RaiseCanExecuteChanged();
                }

            }
        }

        // Validation gate for save button
        public bool CanExecute => !HasErrors &&
            !string.IsNullOrWhiteSpace(Username) &&
            !string.IsNullOrWhiteSpace(Password);
        #endregion

        #region Commands
        public ICommand LoginCommand { get; }
        #endregion

        #region Methods
        private async Task HandleLoginAsync()
        {
            ErrorMessage = null;
            ValidateAll();
            if (HasErrors) return;

            try
            {
                var user = await _authService.LoginAsync(Username, Password);
                _userSessionService.SetCurrentUser(user);

                await ContinueAuthenticatedLoginAsync(user, true);
            }
            catch (AuthenticationException ex)
            {
                ErrorMessage = ex.Message;
            }
            catch (Exception ex)
            {
                ErrorMessage = "Unexpected error occurred.";
                MessageBox.Show($"Login Failed: {ex.Message}", "Error");
            }
        }

        private async Task ContinueAuthenticatedLoginAsync(User user, bool allowDeviceRegistration)
        {
            var machineName = Environment.MachineName;
            var hasPosAccess = _userSessionService.HasPermission("ACCESS_POS_TERMINAL");
            var canAccessDashboard = _userSessionService.HasPermission("ACCESS_ADMIN_PANEL");
            TillShiftStatusDto activeTillShift = null;

            try
            {
                activeTillShift = await _shiftRepository.CheckActiveShiftAsync(machineName, user.UserId);
            }
            catch (SqlException ex) when (IsUnregisteredTerminal(ex))
            {
                if (!canAccessDashboard)
                {
                    _userSessionService.ClearSession();
                    ErrorMessage = "This terminal is unregistered. Please ask a Manager to log in to authorize it.";
                    MessageBox.Show(ErrorMessage, "Unregistered Terminal", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!allowDeviceRegistration)
                {
                    _userSessionService.ClearSession();
                    ErrorMessage = "This terminal is still unregistered. Please verify the terminal registration and try again.";
                    MessageBox.Show(ErrorMessage, "Unregistered Terminal", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var registrationResult = _dialogService.ShowDialog<DeviceRegistrationViewModel>(
                    vm => vm.Initialize(machineName, user.BranchId),
                    out var deviceRegistrationViewModel);

                if (registrationResult == true)
                {
                    await ContinueAuthenticatedLoginAsync(user, false);
                    return;
                }

                if (registrationResult == false && deviceRegistrationViewModel?.WasSkipped == true)
                {
                    if (canAccessDashboard)
                    {
                        OpenAdminDashboard();
                        return;
                    }

                    if (hasPosAccess)
                    {
                        if (!ResolveCashierShift(activeTillShift, machineName))
                        {
                            return;
                        }

                        OpenCashierSales();
                        return;
                    }

                    _userSessionService.ClearSession();
                    ErrorMessage = "No modules assigned to this account. Please contact your administrator.";
                    MessageBox.Show(ErrorMessage, "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _userSessionService.ClearSession();
                return;
            }

            if (canAccessDashboard)
            {
                OpenAdminDashboard();
                return;
            }

            if (hasPosAccess)
            {
                if (!ResolveCashierShift(activeTillShift, machineName))
                {
                    return;
                }

                OpenCashierSales();
                return;
            }

            _userSessionService.ClearSession();
            ErrorMessage = "No modules assigned to this account. Please contact your administrator.";
            MessageBox.Show(ErrorMessage, "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private bool ResolveCashierShift(TillShiftStatusDto activeTillShift, string machineName)
        {
            if (activeTillShift == null)
            {
                throw new InvalidOperationException($"Unable to resolve POS register for terminal '{machineName}'.");
            }

            if (activeTillShift.RequiresFloat)
            {
                var shiftDialogResult = _dialogService.ShowDialog<OpenShiftDialogViewModel>(
                    vm => vm.InitializeTerminal(activeTillShift.TillId, activeTillShift.RegisterName),
                    out var openShiftDialogViewModel);

                if (shiftDialogResult != true || openShiftDialogViewModel?.OpenedShift == null)
                {
                    _userSessionService.ClearSession();
                    return false;
                }

                _userSessionService.SetCurrentShift(openShiftDialogViewModel.OpenedShift.Id);
                return true;
            }

            if (!activeTillShift.ShiftId.HasValue)
            {
                throw new InvalidOperationException("The terminal shift state is invalid.");
            }

            _userSessionService.SetCurrentShift(activeTillShift.ShiftId.Value);
            return true;
        }

        private void OpenCashierSales()
        {
            var nextWindow = _serviceProvider.GetRequiredService<SalesView>();
            nextWindow.DataContext = _serviceProvider.GetRequiredService<SalesViewModel>();
            ShowNextWindow(nextWindow);
        }

        private void OpenAdminDashboard()
        {
            var nextWindow = _serviceProvider.GetRequiredService<MainView>();
            nextWindow.DataContext = _serviceProvider.GetRequiredService<MainViewModel>();
            ShowNextWindow(nextWindow);
        }

        private void ShowNextWindow(Window nextWindow)
        {
            Application.Current.MainWindow = nextWindow;
            nextWindow.Show();

            Application.Current.Windows.OfType<LoginView>().FirstOrDefault()?.Close();
        }

        private static bool IsUnregisteredTerminal(SqlException ex)
        {
            return ex.Message.IndexOf("Unregistered Terminal", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        #endregion

        #region FormHelpers
        private void RaiseCanExecuteChanged()
        {
            (LoginCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
        #endregion

        #region Validation
        private void ValidateAll()
        {
            ValidateUsername();
            ValidatePassword();
        }
        private void ValidateUsername()
        {
            ClearErrors(nameof(Username));
            if (string.IsNullOrWhiteSpace(Username))
                AddError(nameof(Username), "Username is required.");
            else if (!Regex.IsMatch(Username, @"^[a-zA-Z0-9]+$"))
                AddError(nameof(Username), "Username must be alphanumeric (no spaces).");
        }
        private void ValidatePassword()
        {
            ClearErrors(nameof(Password));
            if (string.IsNullOrWhiteSpace(Password))
                AddError(nameof(Password), "Password is required ");
        }
        #endregion
    }
}
