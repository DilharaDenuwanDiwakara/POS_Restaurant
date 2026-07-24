using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;

namespace PointOfSale.Core.Interfaces.Repositories.Inventory
{
    public interface IInventoryReportRepository
    {
        Task<DataTable> GetStockReportAsync(DateTime date, int? categoryId, int locationId);
        Task<DataTable> GetReorderListAsync(int locationId);
        Task<DataTable> GetExpiryListAsync(int locationId);

        Task<List<CategoryValueDto>> GetInventoryValueByCategoryAsync(int locationId);
        Task<DataTable> GetStockMovementLedgerAsync(DateTime startDate, DateTime endDate, int productId, int locationId);
    }
}
