using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PointOfSale.UI.ViewModels.Security;

namespace PointOfSale.UI.Views.Security
{
    /// <summary>
    /// Interaction logic for LoginView.xaml
    /// </summary>
    public partial class LoginView : Window
    {
        public LoginView()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void PasswordBox_Changed(object sender, RoutedEventArgs e)
        {
            if (DataContext is LoginViewModel vm)
                vm.Password = ((PasswordBox)sender).Password;
        }

        private void MoveFocusOnEnter(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (sender is FrameworkElement element)
                {
                    // Push latest value into ViewModel → triggers validation
                    var binding = element.GetBindingExpression(TextBox.TextProperty);
                    binding?.UpdateSource();

                    // Check if this control has any validation errors
                    if (Validation.GetHasError(element))
                    {
                        e.Handled = true; // stay here, don't move
                        return;
                    }

                    // No errors → move focus to next control
                    e.Handled = true;
                    var request = new TraversalRequest(FocusNavigationDirection.Next);
                    element.MoveFocus(request);
                }
            }
        }

        private void SubmitOnEnter(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                var button = sender as Button;
                button?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
        }
    }
}
