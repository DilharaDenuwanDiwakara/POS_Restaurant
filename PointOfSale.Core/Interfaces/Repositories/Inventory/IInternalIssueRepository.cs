using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;

namespace PointOfSale.Core.Interfaces.Repositories.Inventory
{
    /// <summary>
    /// Defines persistence operations for internal stock issue transactions.
    /// </summary>
    public interface IInternalIssueRepository
    {
        /// <summary>
        /// Creates a new internal stock issue and returns the generated issue details.
        /// </summary>
        /// <param name="dto">The internal issue data to save.</param>
        /// <returns>The generated internal issue identifier and issue number.</returns>
        Task<InternalIssueSaveResultDto> CreateInternalIssueAsync(InternalIssueSaveDto dto);

        /// <summary>
        /// Gets raw product ingredients for a finished menu item or variant so internal issue lines
        /// can be exploded before calling Inventory.uspInsertInternalIssue.
        /// </summary>
        /// <param name="menuItemId">The menu item identifier. Used when a variant is not supplied.</param>
        /// <param name="variantId">The variant identifier. Preferred for exact recipe selection.</param>
        /// <returns>Recipe ingredients in product stock/base units with current unit cost.</returns>
        Task<IEnumerable<RecipeIngredientDto>> GetRecipeIngredientsForInternalIssueAsync(int? menuItemId, int? variantId);
    }
}
