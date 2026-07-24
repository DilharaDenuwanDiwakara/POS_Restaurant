using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PointOfSale.UI.ViewModels.Accounts;

namespace PointOfSale.UI.Views.Accounts
{
    public partial class AccountRegisterView : UserControl
    {
        public AccountRegisterView()
        {
            InitializeComponent();
        }

        private void EditableCodePartTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            (DataContext as AccountRegisterViewModel)?.CommitEditableCodePart();
        }

        private void SelectAllText_GotFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is TextBox txt)
            {
                Dispatcher.BeginInvoke(new Action(() => txt.SelectAll()));
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

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            AccountTypeComboBox.Focus();
        }
    }
}
