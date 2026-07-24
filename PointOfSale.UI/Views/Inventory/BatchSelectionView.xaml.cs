using System.Windows;
using System.Windows.Input;

namespace PointOfSale.UI.Views.Inventory
{
    /// <summary>
    /// Interaction logic for BatchSelectionView.xaml
    /// </summary>
    public partial class BatchSelectionView : Window
    {
        public BatchSelectionView()
        {
            InitializeComponent();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Set the DialogResult to true to indicate a successful selection
            this.DialogResult = true;
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Set the DialogResult to false to indicate a cancellation
            this.DialogResult = false;
            this.Close();
        }

        private void BatchesDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // This prevents the default DataGrid behavior for the Enter key.
                e.Handled = true;

                // Check if an item is actually selected in the DataGrid
                if (BatchesDataGrid.SelectedItem != null)
                {
                    // Execute the OK button logic, which closes the dialog
                    OkButton_Click(null, null);
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
