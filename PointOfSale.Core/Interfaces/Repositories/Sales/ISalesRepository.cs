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

        Task<int> HoldSaleAsync(HoldSaleRequestDto dto);

        Task<bool> FinalizeSaleAsync(FinalizeSaleRequestDto dto);

        Task<List<SalesListDto>> GetUnpaidSalesAsync(int branchId);

        Task<RecalledSaleDto> GetSaleForRecallAsync(long salesId);

        DataTable GetInvoiceData(long salesId);

        DataTable GetSettlementReceiptData(long salesId);

        Task<List<SalesListDto>> GetSalesListAsync(DateTime from, DateTime to, int? branchId, string paymentType);

        Task<List<SalesLineItemDto>> GetSalesLineItemsAsync(long salesId);
    }
}
