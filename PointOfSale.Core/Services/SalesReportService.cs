using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
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
        public const string SalesReturnDetailKey = "SALES_RETURN_DETAIL";

        private readonly ISalesReportRepository _repository;
        private readonly IReadOnlyList<SalesReportTypeDto> _reportTypes;

        public SalesReportService(ISalesReportRepository repository)
        {
            _repository = repository;
            _reportTypes = new List<SalesReportTypeDto>
            {
                new SalesReportTypeDto { Key = SalesSummaryKey, DisplayName = "Sales Summary" },
                new SalesReportTypeDto { Key = SalesDetailKey, DisplayName = "Sales Detail" },
                new SalesReportTypeDto { Key = PaymentModeWiseKey, DisplayName = "Payment Mode Wise" },
                new SalesReportTypeDto { Key = SalesReturnDetailKey, DisplayName = "Sales Return Detail" }
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
                case SalesReturnDetailKey:
                    return GenerateSalesReturnDetailReportAsync(request);
                default:
                    var names = string.Join(", ", _reportTypes.Select(r => r.DisplayName));
                    throw new InvalidOperationException($"Unknown sales report type. Available reports: {names}.");
            }
        }

        private async Task<DataTable> GenerateSalesReturnDetailReportAsync(SalesReportRequestDto request)
        {
            var rows = await _repository.GetSalesReturnDetailReportAsync(request.BranchId, request.StartDate, request.EndDate);
            return ToDataTable(rows, "uspGetSalesReturnReport");
        }

        private static DataTable ToDataTable<T>(IEnumerable<T> rows, string tableName)
        {
            var table = new DataTable(tableName);
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties)
            {
                var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                table.Columns.Add(property.Name, propertyType);
            }

            if (rows == null)
                return table;

            foreach (var row in rows)
            {
                var values = properties
                    .Select(property => property.GetValue(row, null) ?? DBNull.Value)
                    .ToArray();

                table.Rows.Add(values);
            }

            return table;
        }
    }
}
