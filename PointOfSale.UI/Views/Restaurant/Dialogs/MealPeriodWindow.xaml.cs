using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PointOfSale.UI.Views.Restaurant.Dialogs
{
    public partial class MealPeriodWindow : Window
    {
        private static readonly Regex TimeTextRegex = new Regex(
            @"^(?:[0-2]?|(?:[01][0-9]|2[0-3])(?::(?:[0-5](?:[0-9])?)?)?)$",
            RegexOptions.Compiled);

        public MealPeriodWindow()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            MealPeriodNameTextBox.Focus();
        }

        private void TimeTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                var proposedText = GetProposedText(textBox, e.Text);
                e.Handled = !IsValidPartialTimeText(proposedText);
            }
        }

        private void TimeTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!(sender is TextBox textBox) || !e.DataObject.GetDataPresent(DataFormats.Text))
            {
                e.CancelCommand();
                return;
            }

            var pastedText = e.DataObject.GetData(DataFormats.Text) as string;
            var proposedText = GetProposedText(textBox, pastedText ?? string.Empty);

            if (!IsValidPartialTimeText(proposedText))
            {
                e.CancelCommand();
            }
        }

        private static string GetProposedText(TextBox textBox, string input)
        {
            var currentText = textBox.Text ?? string.Empty;

            if (textBox.SelectionLength > 0)
            {
                currentText = currentText.Remove(textBox.SelectionStart, textBox.SelectionLength);
            }

            return currentText.Insert(textBox.SelectionStart, input);
        }

        private static bool IsValidPartialTimeText(string value)
        {
            return value.Length <= 5 && TimeTextRegex.IsMatch(value);
        }

        private void MoveFocusOnEnter(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (sender is ComboBox cmb)
                {
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
    }
}
