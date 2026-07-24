using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.Models.Inventory;

namespace PointOfSale.Core.Interfaces.Repositories.Inventory
{
    public interface IUnitMeasureRepository
    {
        Task<int> CreateAsync(UnitMeasure unitMeasure);
        Task<IEnumerable<UnitMeasure>> GetAllAsync();
        Task UpdateAsync(UnitMeasure unitMeasure);
        Task DeleteAsync(int id);
    }
}
