using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;

namespace PointOfSale.Core.Interfaces.Repositories.Inventory
{
    public interface ISubRecipeRepository
    {
        /// <summary>
        /// Gets the bill of material ingredient lines for a semi-finished output product.
        /// </summary>
        Task<IList<SubRecipeLineDto>> GetSubRecipeAsync(int outputProductId);

        Task<IList<SubRecipeLineDto>> GetByOutputProductAsync(int outputProductId);

        Task<bool> IsUsedAsMenuIngredientAsync(int outputProductId);

        /// <summary>
        /// Replaces the bill of material ingredient lines for a semi-finished output product.
        /// </summary>
        Task SaveAsync(int outputProductId, IEnumerable<SubRecipeLineDto> recipeLines);
    }
}
