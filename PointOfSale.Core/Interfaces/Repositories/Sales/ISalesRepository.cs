using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Models.Sales;

namespace PointOfSale.Core.Interfaces.Repositories.Sales
{
    public interface ISalesRepository
    {
        Task<long> CreateAsync(Sale sale);

        DataTable GetInvoiceData(long salesId);

        Task<List<SalesListDto>> GetSalesListAsync(DateTime from, DateTime to, int? branchId, string paymentType);

        Task<List<SalesLineItemDto>> GetSalesLineItemsAsync(long salesId);
    }
}
