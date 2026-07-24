using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PointOfSale.UI.ViewModels.Sales;

namespace PointOfSale.UI.Views.Sales.Dialogs
{
    public partial class CloseShiftDialogView : Window
    {
        public CloseShiftDialogView()
        {
            InitializeComponent();
            DataContextChanged += CloseShiftDialogView_DataContextChanged;
        }

        private void CloseShiftDialogView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is CloseShiftDialogViewModel oldViewModel)
            {
                oldViewModel.CloseRequested -= OnCloseRequested;
            }

            if (e.NewValue is CloseShiftDialogViewModel newViewModel)
            {
                newViewModel.CloseRequested += OnCloseRequested;
            }
        }

        private void OnCloseRequested(bool? dialogResult)
        {
            DialogResult = dialogResult;
            Close();
        }

        private void MoveFocusOnEnter(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            if (sender is TextBox textBox)
            {
                textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            }

            if (sender is FrameworkElement element && Validation.GetHasError(element))
            {
                e.Handled = true;
                return;
            }

            if (Keyboard.FocusedElement is UIElement focusedElement)
            {
                focusedElement.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }

            e.Handled = true;
        }

        private void DecimalOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsValidDecimalInput(sender as TextBox, e.Text);
        }

        private void DecimalOnly_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(DataFormats.Text))
            {
                e.CancelCommand();
                return;
            }

            var text = e.DataObject.GetData(DataFormats.Text) as string;
            if (!IsValidDecimalInput(sender as TextBox, text))
            {
                e.CancelCommand();
            }
        }

        private static bool IsValidDecimalInput(TextBox textBox, string input)
        {
            if (textBox == null || string.IsNullOrEmpty(input))
            {
                return false;
            }

            var text = textBox.Text ?? string.Empty;
            var proposed = text.Remove(textBox.SelectionStart, textBox.SelectionLength)
                               .Insert(textBox.SelectionStart, input);

            return decimal.TryParse(proposed, NumberStyles.Number, CultureInfo.CurrentCulture, out var value) && value >= 0;
        }
    }
}
