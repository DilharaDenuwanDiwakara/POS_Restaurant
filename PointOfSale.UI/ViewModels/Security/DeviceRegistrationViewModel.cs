using System;
using System.Data.SqlClient;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using PointOfSale.Core.Interfaces.Repositories.Sales;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Security
{
    public class DeviceRegistrationViewModel : BaseViewModel
    {
        private readonly IRegisterRepository _registerRepository;
        private readonly IUserSessionService _userSessionService;

        public DeviceRegistrationViewModel(
            IRegisterRepository registerRepository,
            IUserSessionService userSessionService)
        {
            _registerRepository = registerRepository ?? throw new ArgumentNullException(nameof(registerRepository));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));

            RegisterCommand = new AsyncRelayCommand(async _ => await RegisterAsync(), _ => CanRegister());
            SkipCommand = new RelayCommand(_ => Skip());
        }

        private int _branchId;
        public int BranchId
        {
            get => _branchId;
            set
            {
                if (SetProperty(ref _branchId, value))
                {
                    ValidateBranchId();
                    RaiseCanExecuteChanged();
                }
            }
        }

        private string _registerCode;
        public string RegisterCode
        {
            get => _registerCode;
            set
            {
                if (SetProperty(ref _registerCode, value))
                {
                    ValidateRegisterCode();
                    RaiseCanExecuteChanged();
                }
            }
        }

        private string _registerName;
        public string RegisterName
        {
            get => _registerName;
            set
            {
                if (SetProperty(ref _registerName, value))
                {
                    ValidateRegisterName();
                    RaiseCanExecuteChanged();
                }
            }
        }

        private string _machineName;
        public string MachineName
        {
            get => _machineName;
            set
            {
                // Hardware identity is assigned by Initialize only. This public setter keeps
                // an accidental TwoWay TextBox binding from crashing or changing the value.
            }
        }

        public int RegisteredTerminalId { get; private set; }
        public bool WasSkipped { get; private set; }

        public ICommand RegisterCommand { get; }
        public ICommand SkipCommand { get; }

        public event Action<bool?> CloseRequested;

        public void Initialize(string machineName, int branchId)
        {
            SetMachineName(machineName);
            BranchId = branchId;
            RegisterCode = "REG-01";
            RegisterName = "Ground Floor Cashier";
        }

        private void SetMachineName(string machineName)
        {
            if (SetProperty(ref _machineName, machineName, nameof(MachineName)))
            {
                RaiseCanExecuteChanged();
            }
        }

        private async Task RegisterAsync()
        {
            try
            {
                ErrorMessage = null;
                ValidateAll();

                if (HasErrors)
                {
                    return;
                }

                RegisteredTerminalId = await _registerRepository.RegisterTerminalAsync(
                    BranchId,
                    RegisterCode,
                    RegisterName,
                    MachineName,
                    _userSessionService.UserId);

                MessageBox.Show(
                    "POS terminal registered successfully. Login will continue.",
                    "Terminal Registration",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                CloseRequested?.Invoke(true);
            }
            catch (SqlException ex)
            {
                ErrorMessage = ex.Message;
                MessageBox.Show(ex.Message, "Terminal Registration", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                ErrorMessage = "Unable to register this terminal.";
                MessageBox.Show($"Unable to register this terminal: {ex.Message}", "Terminal Registration", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Skip()
        {
            WasSkipped = true;
            CloseRequested?.Invoke(false);
        }

        private bool CanRegister()
        {
            return !HasErrors
                && BranchId > 0
                && !string.IsNullOrWhiteSpace(RegisterCode)
                && !string.IsNullOrWhiteSpace(RegisterName)
                && !string.IsNullOrWhiteSpace(MachineName);
        }

        private void ValidateAll()
        {
            ValidateBranchId();
            ValidateRegisterCode();
            ValidateRegisterName();
        }

        private void ValidateBranchId()
        {
            ClearErrors(nameof(BranchId));

            if (BranchId <= 0)
            {
                AddError(nameof(BranchId), "Branch is required.");
            }
        }

        private void ValidateRegisterCode()
        {
            ClearErrors(nameof(RegisterCode));

            if (string.IsNullOrWhiteSpace(RegisterCode))
            {
                AddError(nameof(RegisterCode), "Register code is required.");
            }
            else if (!Regex.IsMatch(RegisterCode.Trim(), @"^[A-Za-z0-9\-_]+$"))
            {
                AddError(nameof(RegisterCode), "Use letters, numbers, hyphens, or underscores only.");
            }
        }

        private void ValidateRegisterName()
        {
            ClearErrors(nameof(RegisterName));

            if (string.IsNullOrWhiteSpace(RegisterName))
            {
                AddError(nameof(RegisterName), "Register name is required.");
            }
        }

        private void RaiseCanExecuteChanged()
        {
            (RegisterCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
    }
}
