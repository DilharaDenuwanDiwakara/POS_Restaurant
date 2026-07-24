using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.Models.Inventory;

namespace PointOfSale.Core.Interfaces.Repositories.Inventory
{
    public interface IWastageReasonRepository
    {
        Task<int> CreateAsync(WastageReason wastageReason);
        Task<IEnumerable<WastageReason>> GetAllAsync();
        Task UpdateAsync(WastageReason wastageReason);
        Task DeleteAsync(int id);
    }
}
