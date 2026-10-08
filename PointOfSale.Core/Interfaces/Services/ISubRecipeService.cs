using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;

namespace PointOfSale.Core.Interfaces.Services
{
    public interface ISubRecipeService
    {
        Task<IList<SubRecipeLineDto>> GetSubRecipeAsync(int outputProductId);

        Task<IList<SubRecipeLineDto>> GetByOutputProductAsync(int outputProductId);
        Task<bool> IsUsedAsMenuIngredientAsync(int outputProductId);
        Task SaveAsync(int outputProductId, IEnumerable<SubRecipeLineDto> recipeLines);
    }
}
