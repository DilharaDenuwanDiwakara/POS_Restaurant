using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Models.Restaurant;

namespace PointOfSale.Core.Interfaces.Repositories.Restaurant
{
    public interface IMenuItemRepository
    {
        Task<int> CreateAsync(MenuItem menuItem, IEnumerable<int> taxIds, int createdBy);
        Task<IEnumerable<MenuVariantDto>> GetAllVariantsForSalesAsync();
        Task<IEnumerable<MenuItemDto>> GetAllWithDetailsAsync();

        Task<IEnumerable<VariantPriceDto>> GetVariantsByItemIdAsync(int menuItemId);
        Task UpdateVariantPricesAsync(IEnumerable<VariantPriceDto> variants);

        Task<MenuItem> GetByIdAsync(int id);
        Task<IEnumerable<int>> GetSelectedTaxIdsAsync(int menuItemId);
        Task UpdateAsync(MenuItem menuItem, IEnumerable<int> taxIds, int updatedBy);
        Task DeleteAsync(int id);
    }
}
