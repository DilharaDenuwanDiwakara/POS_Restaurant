using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.Models.Restaurant;

namespace PointOfSale.Core.Interfaces.Repositories.Restaurant
{
    public interface IMealPeriodRepository
    {
        Task<int> CreateAsync(MealPeriod mealPeriod);
        Task<IEnumerable<MealPeriod>> GetAllAsync();
        Task UpdateAsync(MealPeriod mealPeriod);
    }
}
