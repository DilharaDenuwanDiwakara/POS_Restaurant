using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.Models.Restaurant;

namespace PointOfSale.Core.Interfaces.Repositories.Restaurant
{
    public interface IMenuCategoryRepository
    {
        Task<int> CreateAsync(MenuCategory menuCategory);
        Task<IEnumerable<MenuCategory>> GetAllAsync();
        Task UpdateAsync(MenuCategory menuCategory);
        Task UpdateMenuCategoryAsync(MenuCategory menuCategory);
        Task DeleteAsync(int id);
    }
}
