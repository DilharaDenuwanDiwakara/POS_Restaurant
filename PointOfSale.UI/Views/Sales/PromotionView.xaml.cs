using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PointOfSale.UI.Views.Sales
{
    public partial class PromotionView : UserControl
    {
        public PromotionView()
        {
            InitializeComponent();
        }

        private void MoveFocusOnEnter(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            if (sender is ComboBox comboBox)
            {
                if (comboBox.IsDropDownOpen)
                    return;

                comboBox.GetBindingExpression(ComboBox.TextProperty)?.UpdateSource();
                comboBox.GetBindingExpression(ComboBox.SelectedItemProperty)?.UpdateSource();
                comboBox.GetBindingExpression(ComboBox.SelectedValueProperty)?.UpdateSource();
            }
            else if (sender is DatePicker datePicker)
            {
                datePicker.GetBindingExpression(DatePicker.SelectedDateProperty)?.UpdateSource();
            }
            else if (sender is TextBox textBox)
            {
                textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            }

            if (sender is FrameworkElement element && Validation.GetHasError(element))
            {
                e.Handled = true;
                return;
            }

            if (Keyboard.FocusedElement is UIElement currentFocus)
                currentFocus.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));

            e.Handled = true;
        }

        private void SelectAllText_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
                Dispatcher.BeginInvoke(new Action(() => textBox.SelectAll()));
        }
    }
}
