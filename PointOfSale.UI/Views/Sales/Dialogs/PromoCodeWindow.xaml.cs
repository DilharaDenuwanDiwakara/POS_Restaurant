using System.Windows;

namespace PointOfSale.UI.Views.Sales.Dialogs
{
    public partial class PromoCodeWindow : Window
    {
        public PromoCodeWindow()
        {
            InitializeComponent();

            this.Loaded += (s, e) =>
            {
                TxtPromoCode?.Focus();
                TxtPromoCode?.SelectAll();
            };
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
