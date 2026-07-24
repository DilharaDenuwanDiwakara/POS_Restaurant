using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.Models.Inventory;

namespace PointOfSale.Core.Interfaces.Repositories.Inventory
{
    public interface IProductBatchRepository
    {
        Task<IEnumerable<ProductBatch>> GetAvailableBatchesAsync(int productId, int locationId);

    }
}
