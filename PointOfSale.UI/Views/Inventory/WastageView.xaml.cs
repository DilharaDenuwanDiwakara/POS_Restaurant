using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PointOfSale.UI.ViewModels.Inventory;

namespace PointOfSale.UI.Views.Inventory
{
    /// <summary>
    /// Interaction logic for WastageView.xaml
    /// </summary>
    public partial class WastageView : UserControl
    {
        public WastageView()
        {
            InitializeComponent();

            this.DataContextChanged += (s, e) =>
            {
                if (this.DataContext is WastageViewModel viewModel)
                {
                    viewModel.RequestQuantityFocus += () =>
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            QtyTextBox.Focus();
                            QtyTextBox.SelectAll();
                        }), System.Windows.Threading.DispatcherPriority.Input);
                    };

                    viewModel.RequestProductFocus += () =>
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            ProductCombobox.Focus();
                            ProductCombobox.IsDropDownOpen = true;
                        }), System.Windows.Threading.DispatcherPriority.Input);
                    };
                }
            };
        }

        private async void MoveFocusOnEnter(object sender, KeyEventArgs e)
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

                    if (cmb == ProductCombobox && DataContext is WastageViewModel viewModel)
                    {
                        e.Handled = true;
                        await viewModel.ConfirmSelectedProductAsync();
                        return;
                    }
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
            // Only focus if we aren't already focused, to allow user to click TWICE to move cursor
            if (textBox != null && !textBox.IsKeyboardFocusWithin)
            {
                e.Handled = true;
                textBox.Focus();
            }
        }

        private void QtyTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // 1. Check for invalid characters (Allow only 0-9 and .)
            // Regex checks: Is the entered character NOT a number and NOT a dot?
            Regex regex = new Regex("[^0-9.]+");
            if (regex.IsMatch(e.Text))
            {
                e.Handled = true; // Block input
                return;
            }

            // 2. Check for duplicate dots
            // If the user types a dot, check if one already exists
            if (e.Text == ".")
            {
                TextBox textBox = sender as TextBox;

                // If the box already has a dot...
                if (textBox.Text.Contains("."))
                {
                    // ...allow it ONLY if the user has selected the old dot to replace it.
                    if (!textBox.SelectedText.Contains("."))
                    {
                        e.Handled = true; // Block duplicate dot
                    }
                }
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            BarcodeTextBox.Focus();
        }
    }
}
