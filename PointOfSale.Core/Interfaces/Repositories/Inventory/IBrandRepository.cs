using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.Models.Inventory;

namespace PointOfSale.Core.Interfaces.Repositories.Inventory
{
    public interface IBrandRepository
    {
        Task<int> CreateAsync(Brand brand);
        Task<IEnumerable<Brand>> GetAllAsync();
        Task UpdateAsync(Brand brand);
        Task DeleteAsync(int id);
    }
}
