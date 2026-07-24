using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PointOfSale.UI.Views.Inventory
{
    /// <summary>
    /// Interaction logic for ProductListView.xaml
    /// </summary>
    public partial class ProductListView : UserControl
    {
        public ProductListView()
        {
            InitializeComponent();
        }

        private void AlphaNumericWithSpace_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, "^[a-zA-Z0-9\\s]+$");
        }

        private void AlphaNumericWithSpace_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            ValidatePaste(e, "^[a-zA-Z0-9\\s]+$");
        }

        private static void ValidatePaste(DataObjectPastingEventArgs e, string pattern)
        {
            if (!e.DataObject.GetDataPresent(DataFormats.Text))
            {
                e.CancelCommand();
                return;
            }

            var text = e.DataObject.GetData(DataFormats.Text) as string;
            if (string.IsNullOrEmpty(text) || !Regex.IsMatch(text, pattern))
            {
                e.CancelCommand();
            }
        }
    }
}
