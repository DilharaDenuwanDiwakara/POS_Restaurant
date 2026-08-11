using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using PointOfSale.UI.ViewModels.Sales;

namespace PointOfSale.UI.Views.Sales
{
    /// <summary>
    /// Interaction logic for SalesView.xaml
    /// </summary>
    public partial class SalesView : Window
    {
        private SalesViewModel ViewModel => DataContext as SalesViewModel;
        private TextBox _lastFocusedTextBox;

        public SalesView()
        {
            InitializeComponent();

            this.Loaded += SalesView_Loaded;
            this.Unloaded += SalesView_Unloaded;
            this.DataContextChanged += SalesView_DataContextChanged;
            this.PreviewGotKeyboardFocus += SalesView_PreviewGotKeyboardFocus;
        }

        #region Initialization & Cleanup
        private async void SalesView_Loaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                await ViewModel.InitializeAsync();
            }

            // Always start with focus on the barcode scanner input
            SetInitialFocus();
        }
        private void SalesView_Unloaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.RequestCashFocus -= OnRequestCashFocus;
                ViewModel.RequestBillDiscountFocus -= OnRequestBillDiscountFocus;
                ViewModel.RequestBarcodeFocus -= OnRequestBarcodeFocus;
            }
        }
        private void SalesView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Unsubscribe from old ViewModel events
            if (e.OldValue is SalesViewModel oldViewModel)
            {
                oldViewModel.RequestCashFocus -= OnRequestCashFocus;
                oldViewModel.RequestBillDiscountFocus -= OnRequestBillDiscountFocus;
                oldViewModel.RequestBarcodeFocus -= OnRequestBarcodeFocus;
            }

            // Subscribe to new ViewModel events
            if (e.NewValue is SalesViewModel newViewModel)
            {
                newViewModel.RequestCashFocus += OnRequestCashFocus;
                newViewModel.RequestBillDiscountFocus += OnRequestBillDiscountFocus;
                newViewModel.RequestBarcodeFocus += OnRequestBarcodeFocus;
            }
        }
        private void SalesView_PreviewGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (e.NewFocus is TextBox tb)
            {
                _lastFocusedTextBox = tb;
            }
        }
        #endregion

        #region Dialog Openers
        private void PromoCodeButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Dialogs.PromoCodeWindow
            {
                DataContext = this.DataContext,
                Owner = this
            };
            dialog.ShowDialog();
        }
        #endregion

        #region Focus Management
        private void SetInitialFocus()
        {
            FocusControl(BarcodeTextBox);
        }
        // Event Handlers called by ViewModel
        private void OnRequestBarcodeFocus() => FocusControl(BarcodeTextBox);
        private void OnRequestCashFocus() => FocusControl(TxtAmountGiven);
        private void OnRequestBillDiscountFocus() => FocusControl(TxtLineDiscountPercent);
        private void FocusControl(Control control)
        {
            if (control == null) return;

            // Use Dispatcher to ensure UI is ready before focusing
            Dispatcher.BeginInvoke(new Action(() =>
            {
                control.Focus();
                if (control is TextBox tb) tb.SelectAll();
            }), DispatcherPriority.Input);
        }
        #endregion

        #region Input Event Handlers
        private void BarcodeTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            HandleBarcodeEnter(sender as TextBox ?? BarcodeTextBox);
        }
        private void ProductComboBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            HandleProductComboEnter(sender as ComboBox ?? ProductComboBox);
        }

        private void DigitsOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!IsDigitsOnly(e.Text))
            {
                e.Handled = true;
                return;
            }

            if (sender == CardReferenceNumberTextBox && WouldExceedMaxLength(CardReferenceNumberTextBox, e.Text, 4))
            {
                e.Handled = true;
            }
        }

        private void DigitsOnly_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(DataFormats.Text))
            {
                e.CancelCommand();
                return;
            }

            var text = e.DataObject.GetData(DataFormats.Text) as string;
            if (!IsDigitsOnly(text))
            {
                e.CancelCommand();
                return;
            }

            if (sender == CardReferenceNumberTextBox && WouldExceedMaxLength(CardReferenceNumberTextBox, text, 4))
            {
                e.CancelCommand();
            }
        }

        private void DecimalAmount_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsDecimalAmountInput(e.Text);
        }

        private void DecimalAmount_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(DataFormats.Text))
            {
                e.CancelCommand();
                return;
            }

            var text = e.DataObject.GetData(DataFormats.Text) as string;
            if (!IsDecimalAmountInput(text))
                e.CancelCommand();
        }

        private void AlphanumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsAlphanumericOnly(e.Text);
        }

        private void AlphanumericOnly_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(DataFormats.Text))
            {
                e.CancelCommand();
                return;
            }

            var text = e.DataObject.GetData(DataFormats.Text) as string;
            if (!IsAlphanumericOnly(text))
                e.CancelCommand();
        }

        private void HandleBarcodeEnter(TextBox sourceTextBox)
        {
            var submittedCode = sourceTextBox?.Text;
            var wasAdded = ViewModel?.SubmitBarcodeEntry(submittedCode) == true;

            if (sourceTextBox != null)
            {
                sourceTextBox.Clear();
                sourceTextBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            }

            if (wasAdded)
            {
                FocusControl(BarcodeTextBox);
                return;
            }

            // No exact code match: keep focus ready for the next scan/manual retry.
            FocusControl(BarcodeTextBox);
        }

        private void HandleProductComboEnter(ComboBox sourceComboBox)
        {
            // 1. Force the ComboBox to commit the selection
            var binding = sourceComboBox?.GetBindingExpression(ComboBox.SelectedItemProperty);
            binding?.UpdateSource();

            // 2. Execute Add Command
            if (ViewModel?.AddProductCommand.CanExecute(null) == true)
            {
                ViewModel.AddProductCommand.Execute(null);

                // 3. UX: Clear search text and return focus to barcode for next item
                if (ProductComboBox != null) ProductComboBox.Text = string.Empty;
                FocusControl(BarcodeTextBox);
            }
        }
        #endregion

        #region Keyboard Shortcuts
        protected override void OnKeyDown(KeyEventArgs e)
        {
            // Intercept global keys before they bubble up
            if (HandleShortcuts(e))
            {
                e.Handled = true;
                return;
            }
            base.OnKeyDown(e);
        }
        private bool HandleShortcuts(KeyEventArgs e)
        {
            if (ViewModel == null) return false;

            switch (e.Key)
            {
                // --- Operations ---
                case Key.F5: // Save Only
                    ExecuteIfCan(ViewModel.SaveSaleCommand);
                    return true;

                case Key.F6: // Refresh / Save & Print
                    ExecuteIfCan(ViewModel.RefreshOrdersCommand);
                    // Or ViewModel.SaveAndPrintCommand if you have one
                    return true;

                case Key.Delete: // Remove Item
                    ExecuteIfCan(ViewModel.RemoveItemCommand);
                    return true;

                case Key.Escape: // Cancel / Back
                    HandleEscape();
                    return true;
            }

            return false;
        }
        private void ExecuteIfCan(ICommand command)
        {
            if (command != null && command.CanExecute(null))
                command.Execute(null);
        }
        private void HandleEscape()
        {
            if (ViewModel == null) return;

            if (ViewModel.SelectedPaymentMethod != null)
            {
                // If Payment panel is open, close it
                ViewModel.SelectedPaymentMethod = null;
            }
            else if (ViewModel.CartItems.Count > 0)
            {
                // Optional: Ask "Cancel entire invoice?"
                // For now, just ensure focus is reset to Barcode
                FocusControl(BarcodeTextBox);
            }
        }
        #endregion

        #region Touch Keypad Support
        private void NumberButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn)) return;
            string value = btn.Content?.ToString();
            if (string.IsNullOrEmpty(value)) return;

            HandleKeypadInput(value);
        }
        private void ClearKeyButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. Clear active textbox target (focused or last-focused)
            var targetTextBox = GetTargetTextBoxForKeypad();
            if (targetTextBox != null)
            {
                targetTextBox.Clear();
                targetTextBox.Focus();
                targetTextBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                return;
            }

            // 2. If nothing is focused, but we are in Cash Mode, clear that amount
            if (ViewModel?.IsPaymentTypeCash == true)
            {
                ViewModel.CustomerGaveAmount = 0;
            }
        }
        private void BackspaceButton_Click(object sender, RoutedEventArgs e)
        {
            var targetTextBox = GetTargetTextBoxForKeypad();
            if (targetTextBox != null)
            {
                ApplyBackspaceToTextBox(targetTextBox);
                return;
            }

            if (ViewModel?.IsPaymentTypeCash == true)
            {
                var currentStr = ViewModel.CustomerGaveAmount.ToString("0.##");
                if (string.IsNullOrEmpty(currentStr) || currentStr == "0")
                {
                    ViewModel.CustomerGaveAmount = 0;
                    return;
                }

                var newStr = currentStr.Substring(0, currentStr.Length - 1);
                if (string.IsNullOrWhiteSpace(newStr) || newStr == ".")
                {
                    ViewModel.CustomerGaveAmount = 0;
                    return;
                }

                if (decimal.TryParse(newStr, out decimal newAmount))
                {
                    ViewModel.CustomerGaveAmount = newAmount;
                }
                else
                {
                    ViewModel.CustomerGaveAmount = 0;
                }
            }
        }
        private void EnterKeyButton_Click(object sender, RoutedEventArgs e)
        {
            // If product combo is active, keypad Enter should add selected product.
            if (ProductComboBox?.IsKeyboardFocusWithin == true || Keyboard.FocusedElement is ComboBox)
            {
                HandleProductComboEnter(ProductComboBox);
                return;
            }

            // If barcode textbox is active/last-active, keypad Enter should run barcode flow.
            var targetTextBox = GetTargetTextBoxForKeypad();
            if (targetTextBox == BarcodeTextBox)
            {
                HandleBarcodeEnter(BarcodeTextBox);
                return;
            }

            // If a product is already selected manually, keypad Enter should add it even after focus moved to keypad.
            if (ViewModel?.SelectedProduct != null)
            {
                HandleProductComboEnter(ProductComboBox);
                return;
            }

            // Generic Enter behavior for other fields: commit and move to next field.
            if (targetTextBox != null)
            {
                targetTextBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                targetTextBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                return;
            }

            // Nothing targeted; keep focus on barcode input for next scan.
            FocusControl(BarcodeTextBox);
        }
        private void HandleKeypadInput(string value)
        {
            // 1. Try to append to textbox target (focused or last-focused)
            var targetTextBox = GetTargetTextBoxForKeypad();
            if (targetTextBox != null)
            {
                if (targetTextBox == CardReferenceNumberTextBox)
                {
                    if (!IsDigitsOnly(value) || WouldExceedMaxLength(targetTextBox, value, 4))
                        return;
                }

                int caret = targetTextBox.CaretIndex;
                targetTextBox.Text = targetTextBox.Text.Insert(caret, value);
                targetTextBox.CaretIndex = caret + value.Length; // Move cursor forward
                targetTextBox.Focus();
                targetTextBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                return;
            }

            // 2. Fallback: If nothing is focused, and we are in Cash Mode, type directly into "Customer Gave"
            if (ViewModel?.IsPaymentTypeCash == true)
            {
                decimal current = ViewModel.CustomerGaveAmount;
                string currentStr = current == 0 ? "" : current.ToString("0.##"); // avoid "05" issue

                // Prevent double decimals
                if (value == "." && currentStr.Contains(".")) return;

                string newStr = currentStr + value;
                if (decimal.TryParse(newStr, out decimal newAmount))
                {
                    ViewModel.CustomerGaveAmount = newAmount;
                }
            }
        }
        private TextBox GetTargetTextBoxForKeypad()
        {
            if (Keyboard.FocusedElement is TextBox focusedTextBox)
            {
                return focusedTextBox;
            }

            if (_lastFocusedTextBox != null && _lastFocusedTextBox.IsVisible && _lastFocusedTextBox.IsEnabled)
            {
                return _lastFocusedTextBox;
            }

            return null;
        }

        private static bool IsDigitsOnly(string text)
        {
            return !string.IsNullOrEmpty(text) && text.All(IsAsciiDigit);
        }

        private static bool WouldExceedMaxLength(TextBox textBox, string value, int maxLength)
        {
            if (textBox == null || value == null)
                return false;

            return textBox.Text.Length - textBox.SelectionLength + value.Length > maxLength;
        }

        private static bool IsAsciiDigit(char value)
        {
            return value >= '0' && value <= '9';
        }

        private static readonly Regex NonDecimalAmountCharacters = new Regex("[^0-9.]+", RegexOptions.Compiled);
        private static readonly Regex NonAlphanumericCharacters = new Regex("[^a-zA-Z0-9]+", RegexOptions.Compiled);

        private static bool IsDecimalAmountInput(string text)
        {
            return !string.IsNullOrEmpty(text) && !NonDecimalAmountCharacters.IsMatch(text);
        }

        private static bool IsAlphanumericOnly(string text)
        {
            return !string.IsNullOrEmpty(text) && !NonAlphanumericCharacters.IsMatch(text);
        }

        private static void ApplyBackspaceToTextBox(TextBox textBox)
        {
            if (textBox == null) return;

            var text = textBox.Text ?? string.Empty;
            int selectionStart = textBox.SelectionStart;
            int selectionLength = textBox.SelectionLength;

            if (selectionLength > 0)
            {
                textBox.Text = text.Remove(selectionStart, selectionLength);
                textBox.CaretIndex = selectionStart;
            }
            else if (selectionStart > 0)
            {
                textBox.Text = text.Remove(selectionStart - 1, 1);
                textBox.CaretIndex = selectionStart - 1;
            }

            textBox.Focus();
            textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        }
        #endregion

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
