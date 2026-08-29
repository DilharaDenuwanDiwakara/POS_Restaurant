using System;
using System.Windows.Controls;
using System.Windows.Data;

namespace PointOfSale.UI.Views.Sales
{
    /// <summary>
    /// Interaction logic for SalesReportsView.xaml
    /// </summary>
    public partial class SalesReportsView : UserControl
    {
        private static readonly string[] WholeQuantityColumns = { "TotalQuantity", "Qty" };
        private static readonly string[] HiddenGridColumns = { "CompanyName", "CompanyAddress", "CompanyContact", "CompanyContactNumber" };

        public SalesReportsView()
        {
            InitializeComponent();
        }

        private void ReportGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            var columnName = e.PropertyName;

            if (Array.Exists(HiddenGridColumns, name => string.Equals(name, columnName, StringComparison.OrdinalIgnoreCase)))
            {
                e.Cancel = true;
                return;
            }

            if (!(e.Column is DataGridTextColumn textColumn) || !(textColumn.Binding is Binding binding))
                return;

            if (Array.Exists(WholeQuantityColumns, name => string.Equals(name, columnName, StringComparison.OrdinalIgnoreCase)))
            {
                binding.StringFormat = "N0";
            }
            else if (columnName.EndsWith("Kg", StringComparison.OrdinalIgnoreCase))
            {
                binding.StringFormat = "N3";
            }
            else if (columnName.IndexOf("Amount", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                binding.StringFormat = "N2";
            }
        }
    }
}
