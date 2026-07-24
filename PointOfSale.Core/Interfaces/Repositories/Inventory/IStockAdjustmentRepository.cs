using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Models.Inventory;

namespace PointOfSale.Core.Interfaces.Repositories.Inventory
{
    public interface IStockAdjustmentRepository
    {
        Task<int> CreateAsync(StockAdjustment stockAdjustment);
        Task<IEnumerable<StockAdjustment>> GetAllAsync();
        Task<IEnumerable<AvailableQuantityDto>> GetAvailableQty(int locationId, int productId);
    }
}
