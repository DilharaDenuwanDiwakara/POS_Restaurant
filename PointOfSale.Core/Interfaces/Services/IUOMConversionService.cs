using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.Models.Inventory;

namespace PointOfSale.Core.Interfaces.Services
{
    public interface IUOMConversionService
    {
        decimal GetConvertedQuantity(int productId, int fromUomId, int toUomId, decimal quantity);
        Task<decimal> GetConvertedQuantityAsync(int productId, int fromUomId, int toUomId, decimal quantity);
        Task<int> GetProductBaseUnitMeasureIdAsync(int productId);
        Task<IEnumerable<ProductUnitMeasureOption>> GetDistinctUOMsForProductAsync(int productId);
        Task<IEnumerable<ProductUnitMeasureOption>> GetAvailableUnitMeasuresAsync(int productId);
    }
}
