using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.Models.Inventory;

namespace PointOfSale.Core.Interfaces.Repositories.Inventory
{
    public interface ILocationRepository
    {
        Task<IEnumerable<Location>> GetByBranchIdAsync(int branchId);
    }
}
