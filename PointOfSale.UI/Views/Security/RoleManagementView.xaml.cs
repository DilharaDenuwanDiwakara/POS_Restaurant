using System.Windows;
using System.Windows.Controls;
using PointOfSale.UI.ViewModels.Security;

namespace PointOfSale.UI.Views.Security
{
    public partial class RoleManagementView : UserControl
    {
        public RoleManagementView()
        {
            InitializeComponent();
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is RoleManagementViewModel viewModel)
                await viewModel.InitializeAsync();
        }
    }
}
