namespace PointOfSale.UI.ViewModels.Accounts
{
    public class SupplierPaymentTabViewModel : BaseViewModel
    {
        public SupplierPaymentTabViewModel(
            SupplierPaymentViewModel paymentViewModel,
            SupplierAdvanceViewModel advanceViewModel,
            SupplierSettlementViewModel settlementViewModel)
        {
            PaymentViewModel = paymentViewModel;
            AdvanceViewModel = advanceViewModel;
            SettlementViewModel = settlementViewModel;

        }

        public SupplierPaymentViewModel PaymentViewModel { get; }
        public SupplierAdvanceViewModel AdvanceViewModel { get; }
        public SupplierSettlementViewModel SettlementViewModel { get; }

        private int _selectedTabIndex;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetProperty(ref _selectedTabIndex, value);
        }
    }
}
