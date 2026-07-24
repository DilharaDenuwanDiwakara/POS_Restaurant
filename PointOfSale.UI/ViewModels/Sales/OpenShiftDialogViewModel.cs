using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Sales;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Sales
{
    public class OpenShiftDialogViewModel : BaseViewModel
    {
        private readonly IShiftRepository _shiftRepository;
        private readonly IUserSessionService _userSessionService;

        public OpenShiftDialogViewModel(
            IShiftRepository shiftRepository,
            IUserSessionService userSessionService)
        {
            _shiftRepository = shiftRepository ?? throw new ArgumentNullException(nameof(shiftRepository));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));

            RegisterDisplayName = "Terminal: Not Configured";
            SaveCommand = new AsyncRelayCommand(async _ => await SaveAsync(), _ => CanSave());
        }

        private RegisterDto _currentLocalRegister;
        public RegisterDto CurrentLocalRegister
        {
            get => _currentLocalRegister;
            private set
            {
                if (SetProperty(ref _currentLocalRegister, value))
                {
                    RegisterDisplayName = value == null ? "Terminal: Not Configured" : $"Terminal: {value.RegisterName}";
                    (SaveCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        private string _registerDisplayName;
        public string RegisterDisplayName
        {
            get => _registerDisplayName;
            private set => SetProperty(ref _registerDisplayName, value);
        }

        private decimal _startingFloat;
        public decimal StartingFloat
        {
            get => _startingFloat;
            set
            {
                if (SetProperty(ref _startingFloat, value))
                {
                    ClearErrors(nameof(StartingFloat));
                    (SaveCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public ShiftDto OpenedShift { get; private set; }

        public ICommand SaveCommand { get; }

        public event Action<bool?> CloseRequested;

        public void InitializeTerminal(int tillId, string registerName)
        {
            CurrentLocalRegister = new RegisterDto
            {
                Id = tillId,
                RegisterName = registerName
            };
        }

        private async Task SaveAsync()
        {
            try
            {
                ClearAllErrors();

                if (CurrentLocalRegister == null)
                {
                    MessageBox.Show("This POS terminal is not assigned to a valid register.", "Open Shift", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (StartingFloat < 0)
                {
                    AddError(nameof(StartingFloat), "Starting float cannot be negative.");
                }

                if (HasErrors)
                {
                    return;
                }

                var shift = new ShiftDto
                {
                    UserId = _userSessionService.UserId,
                    BranchId = _userSessionService.BranchId,
                    TillId = CurrentLocalRegister.Id,
                    TillName = CurrentLocalRegister.RegisterName,
                    CashierName = _userSessionService.Username,
                    StartingFloat = StartingFloat
                };

                await _shiftRepository.OpenShiftAsync(shift);
                OpenedShift = shift;

                CloseRequested?.Invoke(true);
            }
            catch (SqlException ex) when (ex.Number == 50000 || ex.Message.IndexOf("open shift", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                MessageBox.Show(ex.Message, "Open Shift", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open shift: {ex.Message}", "Open Shift", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanSave()
        {
            return CurrentLocalRegister != null && StartingFloat >= 0;
        }
    }
}
