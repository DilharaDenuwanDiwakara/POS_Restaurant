using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Sales;
using PointOfSale.Core.Interfaces.Services;

namespace PointOfSale.Core.Services
{
    public class SalesReportService : ISalesReportService
    {
        public const string SalesSummaryKey = "SALES_SUMMARY";
        public const string SalesDetailKey = "SALES_DETAIL";
        public const string PaymentModeWiseKey = "PAYMENT_MODE_WISE";

        private readonly ISalesReportRepository _repository;
        private readonly IReadOnlyList<SalesReportTypeDto> _reportTypes;

        public SalesReportService(ISalesReportRepository repository)
        {
            _repository = repository;
            _reportTypes = new List<SalesReportTypeDto>
            {
                new SalesReportTypeDto { Key = SalesSummaryKey, DisplayName = "Sales Summary" },
                new SalesReportTypeDto { Key = SalesDetailKey, DisplayName = "Sales Detail" },
                new SalesReportTypeDto { Key = PaymentModeWiseKey, DisplayName = "Payment Mode Wise" }
            };
        }

        public IEnumerable<SalesReportTypeDto> GetAvailableReportTypes()
        {
            return _reportTypes;
        }

        public Task<DataTable> GenerateReportAsync(string reportTypeKey, SalesReportRequestDto request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            switch (reportTypeKey)
            {
                case SalesSummaryKey:
                    return _repository.GetSalesSummaryReportAsync(request.UserId, request.BranchId, request.StartDate, request.EndDate);
                case SalesDetailKey:
                    return _repository.GetSalesDetailReportAsync(request.UserId, request.BranchId, request.StartDate, request.EndDate);
                case PaymentModeWiseKey:
                    return _repository.GetPaymentModeWiseSalesReportAsync(request.UserId, request.BranchId, request.StartDate, request.EndDate);
                default:
                    var names = string.Join(", ", _reportTypes.Select(r => r.DisplayName));
                    throw new InvalidOperationException($"Unknown sales report type. Available reports: {names}.");
            }
        }
    }
}
