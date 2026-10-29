using System.Windows;
using System.Windows.Controls;
using PointOfSale.UI.ViewModels.Accounts;

namespace PointOfSale.UI.Views.Accounts
{
    public partial class AccountInquiryView : UserControl
    {
        public AccountInquiryView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is AccountInquiryViewModel viewModel)
                await viewModel.LoadAccountsAsync();
        }
    }
}
