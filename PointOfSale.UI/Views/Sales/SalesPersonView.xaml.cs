using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PointOfSale.UI.Views.Sales
{
    public partial class SalesPersonView : UserControl
    {
        public SalesPersonView()
        {
            InitializeComponent();
        }

        private void MoveFocusOnEnter(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            if (sender is TextBox textBox)
                textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();

            if (sender is FrameworkElement element && Validation.GetHasError(element))
            {
                e.Handled = true;
                return;
            }

            if (Keyboard.FocusedElement is UIElement currentFocus)
                currentFocus.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));

            e.Handled = true;
        }

        private void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            SalesPersonNameTextBox.Focus();
        }
    }
}
