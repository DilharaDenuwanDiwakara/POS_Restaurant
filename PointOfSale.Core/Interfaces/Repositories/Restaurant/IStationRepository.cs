using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.Models.Restaurant;

namespace PointOfSale.Core.Interfaces.Repositories.Restaurant
{
    public interface IStationRepository
    {
        Task<int> CreateAsync(Station station);
        Task<IEnumerable<Station>> GetAllAsync(int branchId);
        Task UpdateAsync(Station station);
    }
}
