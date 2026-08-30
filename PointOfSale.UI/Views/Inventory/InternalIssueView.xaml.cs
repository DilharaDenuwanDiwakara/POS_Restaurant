using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PointOfSale.UI.ViewModels.Inventory;

namespace PointOfSale.UI.Views.Inventory
{
    /// <summary>
    /// Interaction logic for InternalIssueView.xaml
    /// </summary>
    public partial class InternalIssueView : UserControl
    {
        public InternalIssueView()
        {
            InitializeComponent();

            DataContextChanged += (s, e) =>
            {
                if (DataContext is InternalIssueViewModel viewModel)
                {
                    viewModel.RequestQuantityFocus += () =>
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            QtyTextBox.Focus();
                            QtyTextBox.SelectAll();
                        }), System.Windows.Threading.DispatcherPriority.Input);
                    };

                    viewModel.RequestSearchItemFocus += () =>
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            SearchItemCombobox.Focus();
                            SearchItemCombobox.IsDropDownOpen = true;
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
                if (sender is ComboBox cmb)
                {
                    if (cmb.IsDropDownOpen) return;

                    cmb.GetBindingExpression(ComboBox.TextProperty)?.UpdateSource();
                    var comboBindingExpr = cmb.GetBindingExpression(ComboBox.SelectedItemProperty);
                    comboBindingExpr?.UpdateSource();
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

                if (sender is FrameworkElement barcodeElement && barcodeElement.Name == "BarcodeTextBox")
                {
                    if (DataContext is InternalIssueViewModel vm)
                    {
                        vm.SearchAndSelectItem(vm.Barcode);
                        e.Handled = true;
                        return;
                    }
                }

                if (sender is FrameworkElement searchElement && searchElement.Name == "SearchItemCombobox")
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        QtyTextBox.Focus();
                        QtyTextBox.SelectAll();
                    }), System.Windows.Threading.DispatcherPriority.Input);
                    e.Handled = true;
                    return;
                }

                if (sender is FrameworkElement qtyElement && qtyElement.Name == "QtyTextBox")
                {
                    if (DataContext is InternalIssueViewModel vm)
                    {
                        vm.AddLineCommand.CanExecute(null);

                        if (vm.AddLineCommand.CanExecute(null))
                        {
                            vm.AddLineCommand.Execute(null);
                            e.Handled = true;
                            return;
                        }
                    }
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

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            BarcodeTextBox.Focus();
        }
    }
}
