using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;

namespace PointOfSale.Core.Interfaces.Repositories.Restaurant
{
    public interface IMenuProfitabilityRepository
    {
        Task<List<MenuProfitabilityDto>> GetMenuProfitabilityAsync(int? categoryId);
    }
}
