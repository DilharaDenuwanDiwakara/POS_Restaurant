using System;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Input;
using PointOfSale.UI.ViewModels.Sales;

namespace PointOfSale.UI.Views.Sales
{
    public partial class SalesReturnView : UserControl
    {
        public SalesReturnView()
        {
            InitializeComponent();
        }

        private void InvoiceNumberTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            if (DataContext is SalesReturnViewModel viewModel && viewModel.SearchInvoiceCommand.CanExecute(null))
            {
                viewModel.SearchInvoiceCommand.Execute(null);
            }

            e.Handled = true;
        }

        private void QtyTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var regex = new Regex("[^0-9.]+");
            if (regex.IsMatch(e.Text))
            {
                e.Handled = true;
                return;
            }

            if (e.Text == "." && sender is TextBox textBox && textBox.Text.Contains("."))
            {
                if (!textBox.SelectedText.Contains("."))
                {
                    e.Handled = true;
                }
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
            if (sender is TextBox textBox && !textBox.IsKeyboardFocusWithin)
            {
                e.Handled = true;
                textBox.Focus();
            }
        }
    }
}
