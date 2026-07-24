using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PointOfSale.UI.ViewModels.Security;

namespace PointOfSale.UI.Views.Security.Dialogs
{
    public partial class DeviceRegistrationWindow : Window
    {
        public DeviceRegistrationWindow()
        {
            InitializeComponent();
            DataContextChanged += DeviceRegistrationWindow_DataContextChanged;
        }

        private void DeviceRegistrationWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is DeviceRegistrationViewModel oldViewModel)
            {
                oldViewModel.CloseRequested -= OnCloseRequested;
            }

            if (e.NewValue is DeviceRegistrationViewModel newViewModel)
            {
                newViewModel.CloseRequested += OnCloseRequested;
            }
        }

        private void OnCloseRequested(bool? dialogResult)
        {
            DialogResult = dialogResult;
            Close();
        }

        private void MoveFocusOnEnter(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            if (sender is TextBox textBox)
            {
                textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            }

            if (sender is FrameworkElement element && Validation.GetHasError(element))
            {
                e.Handled = true;
                return;
            }

            if (Keyboard.FocusedElement is UIElement focusedElement)
            {
                focusedElement.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }

            e.Handled = true;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
