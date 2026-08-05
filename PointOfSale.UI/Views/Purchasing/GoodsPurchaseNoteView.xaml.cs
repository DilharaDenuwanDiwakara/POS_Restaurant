using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PointOfSale.UI.ViewModels.Purchasing;

namespace PointOfSale.UI.Views.Purchasing
{
    /// <summary>
    /// Interaction logic for GoodsPurchaseNoteView.xaml
    /// </summary>
    public partial class GoodsPurchaseNoteView : UserControl
    {
        public GoodsPurchaseNoteView()
        {
            InitializeComponent();

            this.DataContextChanged += (s, e) =>
            {
                if (this.DataContext is GoodsPurchaseNoteViewModel viewModel)
                {
                    // Hook up the NEW Quantity focus event
                    viewModel.RequestQuantityFocus += () =>
                    {
                        // Delaying focus slightly ensures the UI has finished updating 
                        // the SelectedProduct before the cursor moves.
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            QuantityTextBox.Focus();
                            QuantityTextBox.SelectAll(); // Highlights existing text (if any)
                        }), System.Windows.Threading.DispatcherPriority.Input);
                    };

                    viewModel.RequestProductFocus += () =>
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            ProductComboBox.Focus();

                            // Optional but highly recommended: Automatically open the dropdown 
                            // so the user can just start typing the product name!
                            ProductComboBox.IsDropDownOpen = true;

                        }), System.Windows.Threading.DispatcherPriority.Input);
                    };

                    viewModel.RequestBarcodeFocus += () =>
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            BarcodeTextBox.Focus();
                            BarcodeTextBox.SelectAll();
                        }), System.Windows.Threading.DispatcherPriority.Input);
                    };
                }
            };
        }

        private void BarcodeTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            BarcodeTextBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();

            if (DataContext is GoodsPurchaseNoteViewModel viewModel && viewModel.SelectedProduct != null)
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    QuantityTextBox.Focus();
                    QuantityTextBox.SelectAll();
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
            else
            {
                ProductComboBox.Focus();
                ProductComboBox.IsDropDownOpen = true;
            }

            e.Handled = true;
        }

        private void MoveFocusOnEnter(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (sender is TextBox multilineTextBox && multilineTextBox.AcceptsReturn)
                {
                    return;
                }

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

        private void AddItemButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                BarcodeTextBox.Focus();
                BarcodeTextBox.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void SelectAllText_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox txt)
            {
                Dispatcher.BeginInvoke(new Action(() => txt.SelectAll()));
            }
        }

        private void AmountTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsValidAmountInput(sender as TextBox, e.Text);
        }

        private void AmountTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(DataFormats.Text))
            {
                e.CancelCommand();
                return;
            }

            var text = e.DataObject.GetData(DataFormats.Text) as string;
            if (!IsValidAmountInput(sender as TextBox, text))
            {
                e.CancelCommand();
            }
        }

        private void AmountTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox &&
                decimal.TryParse(NormalizeAmountText(textBox.Text), NumberStyles.Number, CultureInfo.CurrentCulture, out var amount))
            {
                textBox.Text = amount.ToString("N2", CultureInfo.CurrentCulture);
                textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            }
        }

        private static bool IsValidAmountInput(TextBox textBox, string input)
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
                if (decimalPlaces > 2)
                {
                    return false;
                }
            }

            return proposedText.All(c => char.IsDigit(c) || c == '.' || c == ',');
        }

        private static string NormalizeAmountText(string text)
        {
            var decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            var normalizedText = (text ?? string.Empty).Trim()
                .Replace(".", decimalSeparator)
                .Replace(",", decimalSeparator);

            if (normalizedText == decimalSeparator)
            {
                normalizedText = "0" + decimalSeparator;
            }

            return normalizedText;
        }
    }
}
