using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;

namespace PointOfSale.Core.Interfaces.Services
{
    public interface IDiscountProductCatalogService
    {
        Task<IEnumerable<ProductLiteDto>> GetByMenuCategoryAsync(int menuCategoryId);
    }
}
