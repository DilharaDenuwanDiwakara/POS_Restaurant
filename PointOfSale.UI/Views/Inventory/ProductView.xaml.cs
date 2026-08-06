using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace PointOfSale.UI.Views.Inventory
{
    /// <summary>
    /// Interaction logic for Product.xaml
    /// </summary>
    public partial class ProductView : UserControl
    {
        public ProductView()
        {
            InitializeComponent();
        }

        #region Events
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

                if (ReferenceEquals(sender, UnitMeasureComboBox))
                {
                    ReorderPointTextBox.Focus();
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
        private void SelectAllText_GotFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is TextBox txt)
            {
                Dispatcher.BeginInvoke(new Action(() => txt.SelectAll()));
            }
        }
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            ProductNameTextBox.Focus();
        }

        private void AlphaNumericWithSpace_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, "^[a-zA-Z0-9\\s]+$");
        }

        private void AlphaNumericWithSpace_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            ValidatePaste(e, "^[a-zA-Z0-9\\s]+$");
        }

        private void Decimal_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var txt = sender as TextBox;
            var proposedText = txt.Text.Remove(txt.SelectionStart, txt.SelectionLength)
                                        .Insert(txt.SelectionStart, e.Text);

            e.Handled = !Regex.IsMatch(proposedText, "^\\d*[.,]?\\d*$");
        }

        private void Decimal_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            ValidatePaste(e, "^\\d*[.,]?\\d*$");
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
        #endregion
    }
}
