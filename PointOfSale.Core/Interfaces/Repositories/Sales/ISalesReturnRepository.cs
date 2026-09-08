using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Models.Sales;

namespace PointOfSale.Core.Interfaces.Repositories.Sales
{
    public interface ISalesReturnRepository
    {
        Task<SalesReturnLookupDto> GetOrderForReturnAsync(string invoiceNumber);

        Task<IEnumerable<ReturnReasonModel>> GetActiveReturnReasonsAsync();

        Task<long> ProcessSalesReturnAsync(
            long salesId,
            int branchId,
            int shiftId,
            int authorizedBy,
            int createdBy,
            List<SalesReturnItemModel> returnItems);

        Task<List<SalesReturnFlatDto>> GetSalesReturnsAsync(DateTime from, DateTime to);
    }
}
