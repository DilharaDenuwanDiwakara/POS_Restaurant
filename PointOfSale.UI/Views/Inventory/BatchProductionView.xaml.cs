using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PointOfSale.Core.Models.Inventory;
using PointOfSale.UI.ViewModels.Inventory;

namespace PointOfSale.UI.Views.Inventory
{
    public partial class BatchProductionView : UserControl
    {
        public BatchProductionView()
        {
            InitializeComponent();
            DataContextChanged += (_, e) =>
            {
                if (e.OldValue is BatchProductionViewModel previous) previous.ProductionInputReset -= OnProductionInputReset;
                if (e.NewValue is BatchProductionViewModel current) current.ProductionInputReset += OnProductionInputReset;
            };
        }

        private void OnProductionInputReset(object sender, EventArgs e)
        {
            ProductComboBox.SetCurrentValue(ComboBox.TextProperty, string.Empty);
            // Restore focus once processing has re-enabled the inputs.
            Dispatcher.BeginInvoke(new Action(() => LocationComboBox.Focus()),
                System.Windows.Threading.DispatcherPriority.Input);
        }

        private void ProductionInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            (DataContext as BatchProductionViewModel)?.EnableInputValidation();

            if (sender is ComboBox comboBox)
            {
                if (ReferenceEquals(comboBox, ProductComboBox))
                {
                    // Editable text must identify a real product, even after pasting.
                    var text = comboBox.Text.Trim();
                    var product = comboBox.Items.Cast<Product>().FirstOrDefault(item =>
                        string.Equals(item.ProductName, text, StringComparison.CurrentCultureIgnoreCase));
                    comboBox.SetCurrentValue(ComboBox.SelectedItemProperty, product);
                }
                comboBox.IsDropDownOpen = false;
                comboBox.GetBindingExpression(ComboBox.SelectedItemProperty)?.UpdateSource();
                comboBox.GetBindingExpression(ComboBox.SelectedValueProperty)?.UpdateSource();
                if (comboBox.SelectedItem == null || Validation.GetHasError(comboBox)) return;

                // Closing the popup restores its previous focus; advance after that completes.
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (ReferenceEquals(comboBox, LocationComboBox)) ProductComboBox.Focus();
                    else YieldQuantityTextBox.Focus();
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
            else if (ReferenceEquals(sender, YieldQuantityTextBox))
            {
                YieldQuantityTextBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                if (!Validation.GetHasError(YieldQuantityTextBox) && ProcessBatchButton.IsEnabled)
                    ProcessBatchButton.Focus();
            }
        }
    }
}
