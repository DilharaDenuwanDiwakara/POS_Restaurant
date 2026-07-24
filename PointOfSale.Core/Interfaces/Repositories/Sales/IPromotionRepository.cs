using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.Models.Sales;

namespace PointOfSale.Core.Interfaces.Repositories.Sales
{
    public interface IPromotionRepository
    {
        Task<IEnumerable<PromotionRule>> GetAllAsync();
        Task<IEnumerable<PromotionRule>> GetActiveAsync(int branchId, DateTime when);
        Task<int> CreateAsync(PromotionRule rule);
        Task UpdateAsync(PromotionRule rule);
    }
}
