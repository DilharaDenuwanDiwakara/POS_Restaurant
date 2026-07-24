using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.Models.Inventory;

namespace PointOfSale.Core.Interfaces.Repositories.Inventory
{
    public interface IProductRepository
    {
        Task<int> CreateAsync(Product product);
        Task<IEnumerable<Product>> GetAllAsync();
        Task<IEnumerable<ItemTypeModel>> GetItemTypesAsync();
        Task<IEnumerable<Product>> SearchProductAsync(string searchTerm);
        Task<IEnumerable<Product>> SearchProductWithFilterAsync(int locationId, string searchTerm, int? categoryId = null, int? itemTypeId = null);

        Task<IEnumerable<Product>> SearchProductStockAdjestment(string searchTerm);
        Task<IEnumerable<ProductUnitConversion>> GetUnitConversionsAsync(int productId);
        Task UpdateAsync(Product product);
        Task DeleteAsync(int id);
        Task<long> ReserveSequenceRangeAsync(int count);

        Task UpdateProductCategoryAsync(int productId, int? newCategoryId, int userId);
    }
}
