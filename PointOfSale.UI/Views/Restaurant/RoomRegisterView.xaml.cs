using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PointOfSale.UI.Views.Restaurant
{
    public partial class RoomRegisterView : UserControl
    {
        public RoomRegisterView()
        {
            InitializeComponent();
        }

        private void MoveFocusOnEnter(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            if (sender is ComboBox comboBox)
            {
                if (comboBox.IsDropDownOpen)
                {
                    return;
                }

                comboBox.GetBindingExpression(ComboBox.TextProperty)?.UpdateSource();
                comboBox.GetBindingExpression(ComboBox.SelectedItemProperty)?.UpdateSource();
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
            {
                currentFocus.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }

            e.Handled = true;
        }

        private void SelectAllText_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                Dispatcher.BeginInvoke(new Action(() => textBox.SelectAll()));
            }
        }

        private void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            RoomNameTextBox.Focus();
        }
    }
}
