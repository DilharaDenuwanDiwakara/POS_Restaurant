using System.Windows;

namespace PointOfSale.UI.Views.Restaurant.Dialogs
{
    using System.Windows.Controls;
    using PointOfSale.UI.ViewModels.Restaurant;
    /// <summary>
    /// Interaction logic for PriceUpdateWindow.xaml
    /// </summary>
    public partial class PriceUpdateWindow : Window
    {
        public PriceUpdateWindow()
        {
            InitializeComponent();
        }

        private void SaveChanges_Click(object sender, RoutedEventArgs e)
        {
            // Commit the active editor before saving, including keyboard-triggered saves.
            if (!VariantsGrid.CommitEdit(DataGridEditingUnit.Cell, true) ||
                !VariantsGrid.CommitEdit(DataGridEditingUnit.Row, true))
            {
                MessageBox.Show("Correct the invalid value before saving.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (DataContext is PriceUpdateViewModel viewModel && viewModel.SaveCommand.CanExecute(null))
                viewModel.SaveCommand.Execute(null);
        }
    }
}
