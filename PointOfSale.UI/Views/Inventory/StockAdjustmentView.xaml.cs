using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PointOfSale.UI.Views.Inventory
{
    public partial class StockAdjustmentView : UserControl
    {
        public StockAdjustmentView()
        {
            InitializeComponent();
        }

        private void MoveFocusOnEnter(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            if (sender is ComboBox cmb)
            {
                if (cmb.IsDropDownOpen)
                    return;

                cmb.GetBindingExpression(ComboBox.TextProperty)?.UpdateSource();
                cmb.GetBindingExpression(ComboBox.SelectedItemProperty)?.UpdateSource();
            }
            else if (sender is TextBox txt)
            {
                txt.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            }
            else if (sender is DatePicker datePicker)
            {
                datePicker.GetBindingExpression(DatePicker.SelectedDateProperty)?.UpdateSource();
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

        private void SelectAllText_GotFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is TextBox txt)
            {
                Dispatcher.BeginInvoke(new Action(() => txt.SelectAll()));
            }
        }

        private void QtyTextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox != null && !textBox.IsKeyboardFocusWithin)
            {
                e.Handled = true;
                textBox.Focus();
            }
        }

        private void QtyTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var regex = new Regex("[^0-9.]+");
            if (regex.IsMatch(e.Text))
            {
                e.Handled = true;
                return;
            }

            if (e.Text == ".")
            {
                var textBox = sender as TextBox;
                if (textBox != null && textBox.Text.Contains(".") && !textBox.SelectedText.Contains("."))
                {
                    e.Handled = true;
                }
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            ProductCombobox.Focus();
        }
    }
}
