using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PointOfSale.Core.Interfaces.Services;
using PointOfSale.UI.ViewModels.Accounts;
using PointOfSale.UI.ViewModels.Inventory;
using PointOfSale.UI.ViewModels.Restaurant;
using PointOfSale.UI.ViewModels.Sales;
using PointOfSale.UI.ViewModels.Security;
using PointOfSale.UI.Views.Accounts;
using PointOfSale.UI.Views.Accounts.Dialogs;
using PointOfSale.UI.Views.Inventory.Dialogs;
using PointOfSale.UI.Views.Restaurant.Dialogs;
using PointOfSale.UI.Views.Sales.Dialogs;
using PointOfSale.UI.Views.Security.Dialogs;

namespace PointOfSale.UI.Services
{
    public class DialogService : IDialogService
    {
        private readonly IServiceProvider _serviceProvider;

        public DialogService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public bool? ShowDialog<TViewModel>(out TViewModel viewModel) where TViewModel : class
        {
            return ShowDialog(null, out viewModel);
        }

        public bool? ShowDialog<TViewModel>(Action<TViewModel> configureViewModel, out TViewModel viewModel) where TViewModel : class
        {
            // 1. Get ViewModel
            viewModel = _serviceProvider.GetRequiredService<TViewModel>();
            configureViewModel?.Invoke(viewModel);

            // 2. Create Window
            Window window = null;

            if (typeof(TViewModel) == typeof(UnitMeasureViewModel)) window = new UnitMeasureWindow();
            else if (typeof(TViewModel) == typeof(WastageReasonViewModel)) window = new WastageReason();
            else if (typeof(TViewModel) == typeof(StationViewModel)) window = new StationWindow();
            else if (typeof(TViewModel) == typeof(MealPeriodViewModel)) window = new MealPeriodWindow();
            else if (typeof(TViewModel) == typeof(ExpensesCategoryViewModel)) window = new ExpensesCategoryWindow();
            else if (typeof(TViewModel) == typeof(CashInOutViewModel)) window = new CashInOutWindow();
            else if (typeof(TViewModel) == typeof(OpenShiftDialogViewModel)) window = new OpenShiftDialogView();
            else if (typeof(TViewModel) == typeof(CloseShiftDialogViewModel)) window = new CloseShiftDialogView();
            else if (typeof(TViewModel) == typeof(UserProfileViewModel)) window = new UserProfileWindow();
            else if (typeof(TViewModel) == typeof(DeviceRegistrationViewModel)) window = new DeviceRegistrationWindow();
            else if (typeof(TViewModel) == typeof(AccountTypeRegisterViewModel)) window = new AccountTypeRegisterWindow();
            else if (typeof(TViewModel) == typeof(ManagerAuthorizationViewModel)) window = new ManagerAuthorizationWindow();
            else if (typeof(TViewModel) == typeof(SalesReturnViewModel)) window = new SalesReturnWindow();
            if (window == null) throw new InvalidOperationException($"No Window registered for {typeof(TViewModel).Name}");

            // 3. Connect VM
            window.DataContext = viewModel;

            // safely get the main window
            var mainWindow = Application.Current.MainWindow;

            // Only set the owner if the MainWindow exists AND it is NOT the window we just created
            if (mainWindow != null && mainWindow != window)
            {
                window.Owner = mainWindow;
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                // Fallback: If there is no valid owner, just center on screen
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            // 4. Show
            return window.ShowDialog();
        }

        public void ShowMessage(string message, string title, DialogMessageType messageType = DialogMessageType.Information)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, ToMessageBoxImage(messageType));
        }

        private static MessageBoxImage ToMessageBoxImage(DialogMessageType messageType)
        {
            switch (messageType)
            {
                case DialogMessageType.Warning:
                    return MessageBoxImage.Warning;
                case DialogMessageType.Error:
                    return MessageBoxImage.Error;
                default:
                    return MessageBoxImage.Information;
            }
        }
    }

}
