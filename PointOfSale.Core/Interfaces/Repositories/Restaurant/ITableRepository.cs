using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.Models.Restaurant;

namespace PointOfSale.Core.Interfaces.Repositories.Restaurant
{
    public interface ITableRepository
    {
        Task<int> CreateAsync(Table table);
        Task<IEnumerable<Table>> GetAllAsync(int branchId);
        Task UpdateAsync(Table table);
    }
}
