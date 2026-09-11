using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Restaurant;
using PointOfSale.Core.Interfaces.Services;

namespace PointOfSale.Infrastructure.Service
{
    public class DiscountProductCatalogService : IDiscountProductCatalogService
    {
        private readonly IMenuItemRepository _menuItemRepository;

        public DiscountProductCatalogService(IMenuItemRepository menuItemRepository)
        {
            _menuItemRepository = menuItemRepository ?? throw new ArgumentNullException(nameof(menuItemRepository));
        }

        public async Task<IEnumerable<ProductLiteDto>> GetByMenuCategoryAsync(int menuCategoryId)
        {
            if (menuCategoryId <= 0)
                return Enumerable.Empty<ProductLiteDto>();

            var products = await _menuItemRepository.GetAllVariantsForSalesAsync();

            return (products ?? Enumerable.Empty<MenuVariantDto>())
                .Where(product => product != null &&
                                  product.VariantId > 0 &&
                                  product.MenuCategoryId == menuCategoryId)
                .GroupBy(product => product.VariantId)
                .Select(group => group.First())
                .OrderBy(product => product.ItemCode)
                .ThenBy(product => product.DisplayName)
                .Select(product => new ProductLiteDto
                {
                    ProductId = product.VariantId,
                    ProductCode = product.ItemCode,
                    ProductName = product.DisplayName
                })
                .ToList();
        }
    }
}
