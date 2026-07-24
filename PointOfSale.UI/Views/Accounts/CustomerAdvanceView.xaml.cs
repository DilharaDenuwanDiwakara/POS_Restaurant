using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace PointOfSale.UI.Views.Accounts
{
    /// <summary>
    /// Interaction logic for CustomerAdvanceView.xaml
    /// </summary>
    public partial class CustomerAdvanceView : UserControl
    {
        public CustomerAdvanceView()
        {
            InitializeComponent();
        }

        private bool HasValidationError(DependencyObject element, DependencyProperty property)
        {
            var bindingExpression = BindingOperations.GetBindingExpression(element, property);
            if (bindingExpression != null)
            {
                // Force update source if it hasn't happened yet (important for ComboBoxes)
                bindingExpression.UpdateSource();
            }

            return Validation.GetHasError(element);
        }

        private void CustomerComboBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var cb = (ComboBox)sender;

                if (HasValidationError(cb, ComboBox.SelectedItemProperty)) return;

                PaymentMethodComboBox.Focus();

                e.Handled = true;
            }
        }

        private void PaymentMethodComboBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var cb = (ComboBox)sender;

                if (HasValidationError(cb, ComboBox.SelectedItemProperty)) return;

                PaymentAmountTextBox.Focus();

                e.Handled = true;
            }
        }

        private void ReferenceNumberText_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var tb = (TextBox)sender;

                if (HasValidationError(tb, TextBox.TextProperty)) return;

                PaymentAmountTextBox.Focus();
                e.Handled = true;
            }
        }

        private void PaymentAmountText_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var tb = (TextBox)sender;

                if (HasValidationError(tb, TextBox.TextProperty)) return;

                SaveButton.Focus();
                e.Handled = true;
            }
        }
    }
}
