using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Linq;
using PointOfSale.Core.Models.Purchasing;

namespace PointOfSale.UI.Views.Purchasing
{
    /// <summary>
    /// Interaction logic for GoodsReceiveNoteView.xaml
    /// </summary>
    public partial class GoodsReceiveNoteView : UserControl
    {
        private const string InvoiceNumberPattern = @"^[a-zA-Z0-9_/-]+$";
        private const string DigitsOnlyPattern = @"^\d+$";

        public GoodsReceiveNoteView()
        {
            InitializeComponent();
        }

        private void InvoiceNumber_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, InvoiceNumberPattern);
        }

        private void InvoiceNumber_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(DataFormats.Text))
            {
                e.CancelCommand();
                return;
            }

            var text = e.DataObject.GetData(DataFormats.Text) as string;
            if (string.IsNullOrEmpty(text) || !Regex.IsMatch(text, InvoiceNumberPattern))
            {
                e.CancelCommand();
            }
        }

        private void DigitsOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, DigitsOnlyPattern);
        }

        private void DigitsOnly_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(DataFormats.Text))
            {
                e.CancelCommand();
                return;
            }

            var text = e.DataObject.GetData(DataFormats.Text) as string;
            if (string.IsNullOrEmpty(text) || !Regex.IsMatch(text, DigitsOnlyPattern))
            {
                e.CancelCommand();
            }
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

        private void CommitGridEditOnEnter(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || !(sender is TextBox textBox))
            {
                return;
            }

            textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();

            if (Validation.GetHasError(textBox))
            {
                e.Handled = true;
                return;
            }

            var grid = FindAncestor<DataGrid>(textBox);
            grid?.CommitEdit(DataGridEditingUnit.Cell, true);
            grid?.CommitEdit(DataGridEditingUnit.Row, true);

            textBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            e.Handled = true;
        }

        private void QuantityReceivedTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsValidQuantityInput(sender as TextBox, e.Text);
        }

        private void QuantityReceivedTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(DataFormats.Text))
            {
                e.CancelCommand();
                return;
            }

            var text = e.DataObject.GetData(DataFormats.Text) as string;
            if (!IsValidQuantityInput(sender as TextBox, text))
            {
                e.CancelCommand();
            }
        }

        private void QuantityReceivedTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitQuantityReceivedInput(sender as TextBox);
        }

        private void QuantityReceivedTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || !(sender is TextBox textBox))
            {
                return;
            }

            CommitQuantityReceivedInput(textBox);

            var grid = FindAncestor<DataGrid>(textBox);
            grid?.CommitEdit(DataGridEditingUnit.Cell, true);
            grid?.CommitEdit(DataGridEditingUnit.Row, true);

            textBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            e.Handled = true;
        }

        private static void CommitQuantityReceivedInput(TextBox textBox)
        {
            textBox?.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();

            if (textBox?.DataContext is GoodsReceiveNoteLine line)
            {
                line.CommitQuantityReceivedInput();
            }
        }

        private static bool IsValidQuantityInput(TextBox textBox, string input)
        {
            if (textBox == null || string.IsNullOrEmpty(input))
            {
                return false;
            }

            var currentText = textBox.Text ?? string.Empty;
            var proposedText = currentText.Remove(textBox.SelectionStart, textBox.SelectionLength)
                                          .Insert(textBox.SelectionStart, input);

            if (string.IsNullOrWhiteSpace(proposedText))
            {
                return true;
            }

            var separatorCount = proposedText.Count(c => c == '.' || c == ',');
            if (separatorCount > 1)
            {
                return false;
            }

            var separatorIndex = proposedText.IndexOfAny(new[] { '.', ',' });
            if (separatorIndex >= 0)
            {
                var decimalPlaces = proposedText.Length - separatorIndex - 1;
                if (decimalPlaces > 3)
                {
                    return false;
                }
            }

            return proposedText.All(c => char.IsDigit(c) || c == '.' || c == ',');
        }

        private static T FindAncestor<T>(DependencyObject current)
            where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T match)
                {
                    return match;
                }

                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }

            return null;
        }

    }
}
