using global::System.Windows;
using global::System.Windows.Controls;
using global::System.Windows.Input;

namespace PointOfSale.UI.Views.Settings
{
    /// <summary>
    /// Interaction logic for CompanySettingsView.xaml
    /// </summary>
    public partial class CompanySettingsView : UserControl
    {
        public CompanySettingsView()
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
    }
}
