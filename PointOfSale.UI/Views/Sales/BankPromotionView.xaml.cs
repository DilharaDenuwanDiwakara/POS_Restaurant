using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PointOfSale.UI.Views.Sales
{
    public partial class BankPromotionView : UserControl
    {
        public BankPromotionView() { InitializeComponent(); }

        // Focus belongs to the view. Validation and saving remain in the ViewModel.
        private void MoveFocusOnEnter(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None) return;
            if (sender is ComboBox comboBox)
            {
                // Let Enter commit a highlighted dropdown choice before navigating.
                if (comboBox.IsDropDownOpen) return;
                comboBox.GetBindingExpression(ComboBox.TextProperty)?.UpdateSource();
                comboBox.GetBindingExpression(ComboBox.SelectedItemProperty)?.UpdateSource();
            }
            else if (sender is DatePicker datePicker)
            {
                if (datePicker.IsDropDownOpen) return;
                datePicker.GetBindingExpression(DatePicker.SelectedDateProperty)?.UpdateSource();
            }
            else if (sender is TextBox textBox)
                textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();

            e.Handled = true;
            if (sender is FrameworkElement input && Validation.GetHasError(input)) return;
            // Move from the inner editor of editable dropdowns/date pickers, skipping disabled controls.
            if (Keyboard.FocusedElement is UIElement focused)
                focused.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }

        private static bool AcceptsInput(TextBox box, string insertedText)
        {
            var candidate = box.Text.Remove(box.SelectionStart, box.SelectionLength).Insert(box.SelectionStart, insertedText);
            if ((string)box.Tag == "BIN") return Regex.IsMatch(candidate, @"\A[0-9]{0,6}\z");
            var separator = Regex.Escape(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
            return Regex.IsMatch(candidate, @"\A[0-9]{0,3}(?:" + separator + @"[0-9]{0,2})?\z");
        }

        private void NumericInput_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !AcceptsInput((TextBox)sender, e.Text);
        }

        private void NumericInput_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(DataFormats.UnicodeText) ||
                !AcceptsInput((TextBox)sender, (string)e.DataObject.GetData(DataFormats.UnicodeText)))
                e.CancelCommand();
        }

        private void NumericInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space) e.Handled = true;
            else MoveFocusOnEnter(sender, e);
        }
    }
}
