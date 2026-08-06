using System.Windows;
using PointOfSale.UI.ViewModels.Sales;

namespace PointOfSale.UI.Views.Sales.Dialogs
{
    public partial class SalesReturnWindow : Window
    {
        public SalesReturnWindow()
        {
            InitializeComponent();

            DataContextChanged += (s, e) =>
            {
                if (e.OldValue is SalesReturnViewModel oldVm)
                {
                    oldVm.ReturnProcessed -= OnReturnProcessed;
                }

                if (e.NewValue is SalesReturnViewModel newVm)
                {
                    newVm.ReturnProcessed += OnReturnProcessed;
                }
            };
        }

        private void OnReturnProcessed()
        {
            DialogResult = true;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
