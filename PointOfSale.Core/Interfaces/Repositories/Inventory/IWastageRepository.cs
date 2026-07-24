using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.Models.Inventory;

namespace PointOfSale.Core.Interfaces.Repositories.Inventory
{
    public interface IWastageRepository
    {
        Task<IEnumerable<WastageReason>> GetReasonsAsync();
        Task<long> CreateWastageAsync(Wastage wastage);
    }
}
