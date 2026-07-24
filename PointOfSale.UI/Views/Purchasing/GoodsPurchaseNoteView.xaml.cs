using System;
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
    }
}
