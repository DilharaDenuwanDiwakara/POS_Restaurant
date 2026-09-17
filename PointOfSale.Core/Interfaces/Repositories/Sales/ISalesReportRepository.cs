using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;

namespace PointOfSale.Core.Interfaces.Repositories.Sales
{
    public interface ISalesReportRepository
    {
        Task<DataTable> GetSalesSummaryReportAsync(int userId, int? branchId, DateTime startDate, DateTime endDate);
        Task<DataTable> GetSalesDetailReportAsync(int userId, int? branchId, DateTime startDate, DateTime endDate);
        Task<DataTable> GetPaymentModeWiseSalesReportAsync(int userId, int? branchId, DateTime startDate, DateTime endDate);
        Task<IEnumerable<SalesReturnReportDto>> GetSalesReturnDetailReportAsync(int? branchId, DateTime startDate, DateTime endDate);
    }
}
