namespace PointOfSale.UI.ViewModels.Accounts
{
    public class CustomerPaymentTabViewModel : BaseViewModel
    {
        public CustomerPaymentTabViewModel(CustomerPaymentViewModel payment,
                                           CustomerAdvanceViewModel advance,
                                           CustomerSettlementViewModel settlement)
        {
            CustomerPaymentViewModel = payment;
            CustomerAdvanceViewModel = advance;
            CustomerSettlementViewModel = settlement;
        }

        public CustomerPaymentViewModel CustomerPaymentViewModel { get; }
        public CustomerAdvanceViewModel CustomerAdvanceViewModel { get; }
        public CustomerSettlementViewModel CustomerSettlementViewModel { get; }

        private int _selectedTabIndex;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetProperty(ref _selectedTabIndex, value);
        }
    }
}
