using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Models.Inventory;

namespace PointOfSale.Core.Interfaces.Repositories.Inventory
{
    public interface IInventoryRepository
    {
        Task<long> CreateStockTransferAsync(StockTransfer transfer);
        Task ImportOpeningStockAsync(List<OpenStockItemDto> items, int userId, int locationId = 1);
        Task<IEnumerable<Location>> GetLocationsByBranchAsync(int branchId);
        Task<IEnumerable<StockTransfer>> GetAllStockTransfersAsync(int branchId, DateTime? dateFrom, DateTime? dateTo);
        Task<DataTable> GetStockTransferNoteReportAsync(long transferId);
        Task<decimal> GetRetailItemStockAsync(int variantId, int locationId);
        Task ProcessItemProvisioningAsync(int branchId, int locationId, int inputProductId, int inputUnitId, long? inputBatchId, decimal inputQty, decimal inputUnitCost, int createdBy, List<ProvisioningOutputModel> outputLines);
        Task<List<ProvisioningYieldModel>> GetProvisioningYieldReportAsync(int locationId, DateTime fromDate, DateTime toDate);
        Task<List<ProvisioningYieldDetailModel>> GetProvisioningYieldDetailsAsync(string provisionNumber);
    }
}
