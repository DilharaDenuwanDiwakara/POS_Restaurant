using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PointOfSale.UI.Views.Accounts
{
    /// <summary>
    /// Interaction logic for FundTransferView.xaml
    /// </summary>
    public partial class FundTransferView : UserControl
    {
        private static readonly Regex AmountInputRegex = new Regex(@"^[0-9]*(\.[0-9]*)?$");
        private static readonly Regex DescriptionInputRegex = new Regex(@"^[a-zA-Z0-9\s\-\(\)]*$");

        public FundTransferView()
        {
            InitializeComponent();
        }

        private void MoveFocusOnEnter(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Push latest value into ViewModel - triggers validation
                if (sender is ComboBox cmb)
                {
                    // If the dropdown is open, Enter should select the item first.
                    // Don't move focus yet.
                    if (cmb.IsDropDownOpen) return;

                    cmb.GetBindingExpression(ComboBox.TextProperty)?.UpdateSource();

                    var bindingExpr = cmb.GetBindingExpression(ComboBox.SelectedItemProperty);
                    bindingExpr?.UpdateSource();
                }
                else if (sender is TextBox txt)
                {
                    var bindingExpr = txt.GetBindingExpression(TextBox.TextProperty);
                    bindingExpr?.UpdateSource();
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

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SourceAccountCombo.Focus();
        }

        private void SelectAllText_GotFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is TextBox txt)
            {
                Dispatcher.BeginInvoke(new Action(() => txt.SelectAll()));
            }
        }

        private void AmountTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                e.Handled = !IsValidAmountText(GetTextAfterInput(textBox, e.Text));
            }
        }

        private void AmountTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!(sender is TextBox textBox) || !e.DataObject.GetDataPresent(typeof(string)))
            {
                e.CancelCommand();
                return;
            }

            var pasteText = e.DataObject.GetData(typeof(string)) as string;
            if (!IsValidAmountText(GetTextAfterInput(textBox, pasteText)))
            {
                e.CancelCommand();
            }
        }

        private void DescriptionTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !DescriptionInputRegex.IsMatch(e.Text);
        }

        private void DescriptionTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(typeof(string)))
            {
                e.CancelCommand();
                return;
            }

            var pasteText = e.DataObject.GetData(typeof(string)) as string;
            if (pasteText == null || !DescriptionInputRegex.IsMatch(pasteText))
            {
                e.CancelCommand();
            }
        }

        private static bool IsValidAmountText(string text)
        {
            return string.IsNullOrEmpty(text) || AmountInputRegex.IsMatch(text);
        }

        private static string GetTextAfterInput(TextBox textBox, string input)
        {
            input = input ?? string.Empty;

            var currentText = textBox.Text ?? string.Empty;
            return currentText.Remove(textBox.SelectionStart, textBox.SelectionLength)
                              .Insert(textBox.SelectionStart, input);
        }
    }
}
