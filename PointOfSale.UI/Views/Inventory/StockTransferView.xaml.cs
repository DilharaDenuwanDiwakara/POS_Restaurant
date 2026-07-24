using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PointOfSale.UI.ViewModels.Inventory;

namespace PointOfSale.UI.Views.Inventory
{
    /// <summary>
    /// Interaction logic for StockTransferView.xaml
    /// </summary>
    public partial class StockTransferView : UserControl
    {
        public StockTransferView()
        {
            InitializeComponent();

            this.DataContextChanged += (s, e) =>
            {
                if (this.DataContext is StockTransferViewModel viewModel)
                {
                    // Hook up the NEW Quantity focus event
                    viewModel.RequestQuantityFocus += () =>
                    {
                        // Delaying focus slightly ensures the UI has finished updating 
                        // the SelectedProduct before the cursor moves.
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            QtyTextBox.Focus();
                            QtyTextBox.SelectAll(); // Highlights existing text (if any)
                        }), System.Windows.Threading.DispatcherPriority.Input);
                    };

                    viewModel.RequestProductFocus += () =>
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            ProductCombobox.Focus();

                            // Optional but highly recommended: Automatically open the dropdown 
                            // so the user can just start typing the product name!
                            ProductCombobox.IsDropDownOpen = true;

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

        private void MoveFocusOnEnter(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // 1. Handle ComboBox Logic (Your existing working code)
                if (sender is ComboBox cmb)
                {
                    if (cmb.IsDropDownOpen) return; // Returns early, lets ComboBox select item

                    cmb.GetBindingExpression(ComboBox.TextProperty)?.UpdateSource();
                    var bindingExpr = cmb.GetBindingExpression(ComboBox.SelectedItemProperty);
                    bindingExpr?.UpdateSource();
                }
                else if (sender is TextBox txt)
                {
                    var bindingExpr = txt.GetBindingExpression(TextBox.TextProperty);
                    bindingExpr?.UpdateSource();
                }

                // 2. Validation Check (Your existing working code)
                if (sender is FrameworkElement element && Validation.GetHasError(element))
                {
                    e.Handled = true;
                    return;
                }

                // ============================================================
                // 3. NEW LOGIC: Intercept "Quantity" to click "Add"
                // ============================================================
                if (sender is FrameworkElement el && el.Name == "QtyTextBox")
                {
                    if (DataContext is StockTransferViewModel vm)
                    {
                        // Force the command to refresh so it knows data is valid
                        // (Only needed if the automatic refresh setup earlier isn't catching it fast enough)
                        vm.AddLineCommand.CanExecute(null);

                        if (vm.AddLineCommand.CanExecute(null))
                        {
                            vm.AddLineCommand.Execute(null);
                            e.Handled = true; // Stop here

                            // Optional: Focus back to Barcode to scan next item
                            ProductCombobox.Focus();

                            return;
                        }
                    }
                }
                // ============================================================

                // 4. Standard Move Focus (Your existing working code)
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

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            BarcodeTextBox.Focus();
        }
    }
}
