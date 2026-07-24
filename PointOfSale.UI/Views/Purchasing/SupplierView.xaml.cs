using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PointOfSale.UI.Views.Purchasing
{
    /// <summary>
    /// Interaction logic for Supplier.xaml
    /// </summary>
    public partial class SupplierView : UserControl
    {
        public SupplierView()
        {
            InitializeComponent();
        }

        private void MoveFocusOnEnter(object sender, KeyEventArgs e)
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

        private void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            SupplierNameTextBox.Focus();
        }

        private void AlphaNumericNoSpace_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, "^[a-zA-Z0-9]+$");
        }

        private void AlphaNumericWithSpace_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, "^[a-zA-Z0-9\\s]+$");
        }

        private void DigitsOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, "^\\d+$");
        }

        private void AlphaNumericNoSpace_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            ValidatePaste(e, "^[a-zA-Z0-9]+$");
        }

        private void AlphaNumericWithSpace_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            ValidatePaste(e, "^[a-zA-Z0-9\\s]+$");
        }

        private void DigitsOnly_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            ValidatePaste(e, "^\\d+$");
        }

        private static void ValidatePaste(DataObjectPastingEventArgs e, string pattern)
        {
            if (!e.DataObject.GetDataPresent(DataFormats.Text))
            {
                e.CancelCommand();
                return;
            }

            var text = e.DataObject.GetData(DataFormats.Text) as string;
            if (string.IsNullOrEmpty(text) || !Regex.IsMatch(text, pattern))
            {
                e.CancelCommand();
            }
        }
    }
}
