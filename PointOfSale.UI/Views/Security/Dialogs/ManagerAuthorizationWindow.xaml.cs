using System.Windows;
using System.Windows.Controls;
using PointOfSale.UI.ViewModels.Security;

namespace PointOfSale.UI.Views.Security.Dialogs
{
    public partial class ManagerAuthorizationWindow : Window
    {
        public ManagerAuthorizationWindow()
        {
            InitializeComponent();

            DataContextChanged += (s, e) =>
            {
                if (e.OldValue is ManagerAuthorizationViewModel oldVm)
                {
                    oldVm.RequestClose -= OnRequestClose;
                }

                if (e.NewValue is ManagerAuthorizationViewModel newVm)
                {
                    newVm.RequestClose += OnRequestClose;
                }
            };

            Loaded += (s, e) => PinBox.Focus();
        }

        private void OnRequestClose(bool result)
        {
            DialogResult = result;
            Close();
        }

        private void PinBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is ManagerAuthorizationViewModel vm)
            {
                vm.PinCode = ((PasswordBox)sender).Password;
            }
        }
    }
}
