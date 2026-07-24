using System.Windows;
using System.Windows.Input;

namespace PointOfSale.UI.Views.Inventory.Dialogs
{
    /// <summary>
    /// Interaction logic for WastageReason.xaml
    /// </summary>
    public partial class WastageReason : Window
    {
        public WastageReason()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // Helper to move focus on Enter key (as used in your View)
        private void MoveFocusOnEnter(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var request = new TraversalRequest(FocusNavigationDirection.Next);
                request.Wrapped = true;
                ((UIElement)sender).MoveFocus(request);
            }
        }
    }
}
