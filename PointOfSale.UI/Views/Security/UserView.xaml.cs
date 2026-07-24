using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PointOfSale.UI.Views.Security
{
    /// <summary>
    /// Interaction logic for UserView.xaml
    /// </summary>
    public partial class UserView : UserControl
    {
        public UserView()
        {
            InitializeComponent();
        }

        private void MoveFocusOnEnter(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Push latest value into ViewModel → triggers validation
                if (sender is ComboBox cmb)
                {
                    // If the dropdown is open, Enter should select the item first. 
                    // Don't move focus yet.
                    if (cmb.IsDropDownOpen) return;

                    cmb.GetBindingExpression(ComboBox.TextProperty)?.UpdateSource();
                    cmb.GetBindingExpression(ComboBox.SelectedValueProperty)?.UpdateSource();
                }
                else if (sender is TextBox txt)
                {
                    txt.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                }

                if (sender is FrameworkElement element && Validation.GetHasError(element))
                {
                    e.Handled = true;
                    return;
                }

                if (Keyboard.FocusedElement is UIElement currentFocus)
                {
                    currentFocus.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                }
                e.Handled = true;
            }
        }

        private void SubmitButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            FullNameTextBox.Focus();
        }

    }
}
