using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Inventory;
using PointOfSale.Core.Interfaces.Services;

namespace PointOfSale.Core.Services
{
    public class SubRecipeService : ISubRecipeService
    {
        private readonly ISubRecipeRepository _subRecipeRepository;

        public SubRecipeService(ISubRecipeRepository subRecipeRepository)
        {
            _subRecipeRepository = subRecipeRepository;
        }

        public Task<IList<SubRecipeLineDto>> GetSubRecipeAsync(int outputProductId)
        {
            if (outputProductId <= 0)
            {
                return Task.FromResult<IList<SubRecipeLineDto>>(new List<SubRecipeLineDto>());
            }

            return _subRecipeRepository.GetSubRecipeAsync(outputProductId);
        }

        public Task<IList<SubRecipeLineDto>> GetByOutputProductAsync(int outputProductId)
        {
            return GetSubRecipeAsync(outputProductId);
        }

        public Task<bool> IsUsedAsMenuIngredientAsync(int outputProductId)
        {
            if (outputProductId <= 0)
            {
                return Task.FromResult(false);
            }

            return _subRecipeRepository.IsUsedAsMenuIngredientAsync(outputProductId);
        }

        public Task SaveAsync(int outputProductId, IEnumerable<SubRecipeLineDto> recipeLines)
        {
            if (outputProductId <= 0)
            {
                throw new ArgumentException("Select the prep item before saving the sub-recipe.", nameof(outputProductId));
            }

            var lines = (recipeLines ?? Enumerable.Empty<SubRecipeLineDto>()).ToList();
            if (!lines.Any())
            {
                throw new InvalidOperationException("Add at least one ingredient before saving the sub-recipe.");
            }

            foreach (var line in lines)
            {
                if (line.ProductId <= 0)
                {
                    throw new InvalidOperationException("Each ingredient line must have a valid product.");
                }

                if (line.ProductId == outputProductId)
                {
                    throw new InvalidOperationException("The output prep item cannot be used as its own ingredient.");
                }

                if (line.UnitMeasureId <= 0)
                {
                    throw new InvalidOperationException("Each ingredient line must have a valid unit measure.");
                }

                if (line.Quantity <= 0)
                {
                    throw new InvalidOperationException("Each ingredient quantity must be greater than zero.");
                }
            }

            return _subRecipeRepository.SaveAsync(outputProductId, lines);
        }
    }
}
