using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using PointOfSale.Core.Interfaces.Repositories.Sales;
using PointOfSale.Core.Interfaces.Services;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Sales
{
    public class SalesListViewModel : BaseViewModel
    {
        private readonly ISalesRepository _salesRepository;
        private readonly IUserSessionService _userSessionService;
        private readonly ISalesInvoiceReportPreviewService _salesInvoiceReportPreviewService;
        private readonly IDialogService _dialogService;

        public SalesListViewModel(
            ISalesRepository salesRepository,
            IUserSessionService userSessionService,
            ISalesInvoiceReportPreviewService salesInvoiceReportPreviewService,
            IDialogService dialogService)
        {
            _salesRepository = salesRepository;
            _userSessionService = userSessionService;
            _salesInvoiceReportPreviewService = salesInvoiceReportPreviewService;
            _dialogService = dialogService;

            SalesList = new ObservableCollection<InvoiceSummaryModel>();
            PaymentTypes = new ObservableCollection<string> { "All", "Cash", "Credit", "Card", "Bank Transfer" };

            SearchCommand = new AsyncRelayCommand(SearchSalesAsync);
            ReprintInvoiceCommand = new AsyncRelayCommand(ReprintInvoiceAsync);
            _ = SearchSalesAsync(null);
        }

        #region Properties
        public ObservableCollection<InvoiceSummaryModel> SalesList { get; }
        public ObservableCollection<string> PaymentTypes { get; }

        private DateTime _fromDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        public DateTime FromDate
        {
            get => _fromDate;
            set => SetProperty(ref _fromDate, value);
        }

        private DateTime _toDate = DateTime.Today.AddDays(1).AddSeconds(-1);
        public DateTime ToDate
        {
            get => _toDate;
            set => SetProperty(ref _toDate, value);
        }

        private string _selectedPaymentType = "All";
        public string SelectedPaymentType
        {
            get => _selectedPaymentType;
            set => SetProperty(ref _selectedPaymentType, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    (ReprintInvoiceCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        private decimal _totalRevenue;
        public decimal TotalRevenue
        {
            get => _totalRevenue;
            set => SetProperty(ref _totalRevenue, value);
        }
        #endregion

        #region Commands
        public ICommand SearchCommand { get; }
        public ICommand ReprintInvoiceCommand { get; }
        #endregion

        #region Command Implementation
        private async Task SearchSalesAsync(object obj)
        {
            try
            {
                IsBusy = true;
                SalesList.Clear();

                var from = FromDate.Date;
                var to = ToDate.Date.AddDays(1).AddSeconds(-1);
                if (_userSessionService.BranchId <= 0)
                {
                    ErrorMessage = "Current user branch is not assigned.";
                    return;
                }

                int? branchId = _userSessionService.BranchId;
                var results = await _salesRepository.GetSalesListAsync(from, to, branchId, SelectedPaymentType);

                foreach (var item in results)
                {
                    SalesList.Add(new InvoiceSummaryModel(
                        item,
                        _salesRepository.GetSalesLineItemsAsync,
                        ex => ErrorMessage = ex.Message));
                }

                TotalRevenue = SalesList.Sum(x => x.NetAmount);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ReprintInvoiceAsync(object parameter)
        {
            if (IsBusy || !(parameter is InvoiceSummaryModel invoice) || invoice.SalesId <= 0)
                return;

            try
            {
                IsBusy = true;
                ErrorMessage = null;

                await _salesInvoiceReportPreviewService.ShowPreviewAsync(invoice.SalesId);
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Failed to reprint sales invoice {invoice.SalesId}: {ex}");

                const string message = "Unable to load the bill preview. Please try again or contact support.";
                ErrorMessage = message;
                _dialogService.ShowMessage(message, "Reprint Bill", DialogMessageType.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }
        #endregion
    }
}
