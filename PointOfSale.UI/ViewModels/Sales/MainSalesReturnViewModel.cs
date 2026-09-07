using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Sales;
using PointOfSale.Core.Interfaces.Services;
using PointOfSale.Core.Models.Sales;
using PointOfSale.Core.Services;
using PointOfSale.Infrastructure.Repositories.Sales;
using PointOfSale.UI.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PointOfSale.UI.ViewModels.Sales
{
    public class MainSalesReturnViewModel : BaseViewModel
    {

        private readonly ISalesReturnRepository _salesReturnRepository;
        private readonly IUserSessionService _userSessionService;
        private readonly IDialogService _dialogService;

        public ObservableCollection<SalesReturnModel> SalesReturnList { get; }

        public MainSalesReturnViewModel(IUserSessionService userSessionService, IDialogService dialogService, ISalesReturnRepository salesReturnRepository)
        {
            _salesReturnRepository = salesReturnRepository ?? throw new ArgumentNullException(nameof(salesReturnRepository));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));
            _dialogService = dialogService;

            SalesReturnList = new ObservableCollection<SalesReturnModel>();

            SearchCommand = new AsyncRelayCommand(SearchSalesReturnAsync, _ => !IsBusy);

            SearchSalesReturnAsync(null);
        }

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

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    (SearchCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        private decimal _totalRefundAmount;
        public decimal TotalRefundAmount
        {
            get => _totalRefundAmount;
            set => SetProperty(ref _totalRefundAmount, value);
        }

        private async Task SearchSalesReturnAsync(object obj)
        {
            try
            {
                IsBusy = true;
                SalesReturnList.Clear();

                var from = FromDate.Date;
                var to = ToDate.Date.AddDays(1).AddSeconds(-1);

                if (_userSessionService.BranchId <= 0)
                {
                    ErrorMessage = "Current user branch is not assigned.";
                    return;
                }

                var flatResults = await _salesReturnRepository.GetSalesReturnsAsync(from, to);

                var groupedResults = flatResults
                    .GroupBy(r => new { r.ReturnNo, r.InvoiceNo, r.TotalRefundAmount, r.CreateDate })
                    .Select(g => new SalesReturnModel(
                        g.Key.ReturnNo,
                        g.Key.InvoiceNo,
                        g.Key.TotalRefundAmount,
                        g.Key.CreateDate,

                        g.Select(line => new SalesReturnLineModel
                        {
                            ProductName = line.ProductName,
                            QtyReturn = line.QtyReturn,
                            RefundAmount = line.RefundAmount,
                            ReturnReason = line.ReturnReason,
                            IsWastage = line.IsWastage
                        })
                    ));

                foreach (var item in groupedResults)
                {
                    SalesReturnList.Add(item);
                }

                TotalRefundAmount = SalesReturnList.Sum(x => x.TotalRefundAmount);
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

        public ICommand SearchCommand { get; }



    }
}
