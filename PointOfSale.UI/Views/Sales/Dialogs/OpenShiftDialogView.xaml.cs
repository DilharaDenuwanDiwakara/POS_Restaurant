using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PointOfSale.UI.ViewModels.Sales;

namespace PointOfSale.UI.Views.Sales.Dialogs
{
    public partial class OpenShiftDialogView : Window
    {
        public OpenShiftDialogView()
        {
            InitializeComponent();
            DataContextChanged += OpenShiftDialogView_DataContextChanged;
        }

        private void OpenShiftDialogView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is OpenShiftDialogViewModel oldViewModel)
            {
                oldViewModel.CloseRequested -= OnCloseRequested;
            }

            if (e.NewValue is OpenShiftDialogViewModel newViewModel)
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
            if (e.Key == Key.Enter)
            {
                if (sender is ComboBox comboBox)
                {
                    if (comboBox.IsDropDownOpen)
                    {
                        return;
                    }

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

                if (Keyboard.FocusedElement is UIElement focusedElement)
                {
                    focusedElement.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                }

                e.Handled = true;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
