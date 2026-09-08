using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Models.Inventory;

namespace PointOfSale.Core.Interfaces.Repositories.Inventory
{
    public interface IStockAdjustmentRepository
    {
        Task<long> CreateAsync(StockAdjustment stockAdjustment);
        Task<IEnumerable<StockAdjustment>> GetAllAsync();
        Task<IEnumerable<AvailableQuantityDto>> GetAvailableQty(int locationId, int productId);
        Task<List<StockAdjustmentHeaderDto>> GetHistoryAsync(global::System.DateTime? fromDate, global::System.DateTime? toDate, int? locationId);
    }
}
