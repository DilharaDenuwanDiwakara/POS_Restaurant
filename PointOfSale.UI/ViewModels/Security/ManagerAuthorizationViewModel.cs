using System;
using System.Threading.Tasks;
using System.Windows.Input;
using PointOfSale.Core.Interfaces.Security;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Security
{
    public class ManagerAuthorizationViewModel : BaseViewModel
    {
        private readonly IUserRepository _userRepository;

        public ManagerAuthorizationViewModel(IUserRepository userRepository)
        {
            _userRepository = userRepository;

            ConfirmCommand = new AsyncRelayCommand(async _ => await OnConfirmAsync(), _ => !string.IsNullOrWhiteSpace(PinCode));
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
        }

        /// <summary>Permission key the entered PIN's user must hold (e.g. "SALES_RETURN").</summary>
        public string RequiredPermission { get; set; }

        private string _actionDescription = "perform this action";
        public string ActionDescription
        {
            get => _actionDescription;
            set => SetProperty(ref _actionDescription, value);
        }

        private string _pinCode;
        public string PinCode
        {
            get => _pinCode;
            set
            {
                if (SetProperty(ref _pinCode, value))
                {
                    ErrorMessage = null;
                    ConfirmCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsAuthorized { get; private set; }
        public int AuthorizedUserId { get; private set; }
        public string AuthorizedUserName { get; private set; }

        public AsyncRelayCommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }

        public event Action<bool> RequestClose;

        private async Task OnConfirmAsync()
        {
            try
            {
                var pin = PinCode?.Trim();
                if (string.IsNullOrEmpty(pin))
                {
                    ErrorMessage = "Enter a manager PIN.";
                    return;
                }

                var authResult = await _userRepository.GetUserByPinAsync(pin);
                if (authResult == null)
                {
                    ErrorMessage = "Invalid PIN.";
                    return;
                }

                if (!string.IsNullOrWhiteSpace(RequiredPermission))
                {
                    var permissions = await _userRepository.GetPermissionsAsync(authResult.UserId);
                    if (!permissions.Contains(RequiredPermission))
                    {
                        ErrorMessage = $"This user is not authorized to {ActionDescription}.";
                        return;
                    }
                }

                IsAuthorized = true;
                AuthorizedUserId = authResult.UserId;
                AuthorizedUserName = authResult.FullName;
                RequestClose?.Invoke(true);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Authorization failed: {ex.Message}";
            }
        }
    }
}
