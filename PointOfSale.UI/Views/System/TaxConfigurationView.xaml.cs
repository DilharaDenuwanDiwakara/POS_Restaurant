using System;
using global::System.Windows;
using global::System.Windows.Controls;
using global::System.Windows.Input;

namespace PointOfSale.UI.Views.Settings
{
    /// <summary>
    /// Interaction logic for TaxConfigurationView.xaml
    /// </summary>
    public partial class TaxConfigurationView : UserControl
    {
        public TaxConfigurationView()
        {
            InitializeComponent();
        }

        private void MoveFocusOnEnter(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (sender is TextBox txt)
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

        private void SelectAllText_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox txt)
            {
                Dispatcher.BeginInvoke(new Action(() => txt.SelectAll()));
            }
        }
    }
}
