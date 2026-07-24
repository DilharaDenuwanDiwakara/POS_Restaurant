using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.Models.Inventory;

namespace PointOfSale.Core.Interfaces.Repositories.Inventory
{
    public interface ICategoryRepository
    {
        Task<int> CreateAsync(Category category);
        Task<IEnumerable<Category>> GetAllAsync();
        Task UpdateAsync(Category category);
        Task UpdateProductCategoryAsync(Category productCategory);
        Task DeleteAsync(int id);
    }
}
