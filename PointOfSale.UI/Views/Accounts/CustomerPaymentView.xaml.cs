using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Input;

namespace PointOfSale.UI.Views.Accounts
{
    /// <summary>
    /// Interaction logic for CustomerPaymentView.xaml
    /// </summary>
    public partial class CustomerPaymentView : UserControl
    {
        public CustomerPaymentView()
        {
            InitializeComponent();
        }

        private void PaymentAmount_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, @"^[0-9.]$");
        }

        private void PaymentAmount_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Prevent entering multiple dots
            if (e.Key == Key.OemPeriod || e.Key == Key.Decimal)
            {
                var textBox = (TextBox)sender;
                if (textBox.Text.Contains('.'))
                    e.Handled = true;
            }
        }
    }
}
