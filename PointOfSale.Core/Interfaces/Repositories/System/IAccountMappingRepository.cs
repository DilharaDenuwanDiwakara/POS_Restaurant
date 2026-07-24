using System.Collections.Generic;
using System.Threading.Tasks;

namespace PointOfSale.Core.Interfaces.Repositories.System
{
    public interface IAccountMappingRepository
    {
        Task<Dictionary<string, int?>> GetSystemAccountMappingsAsync();

        Task UpdateSystemAccountMappingsAsync(
            int? cashAccountId,
            int? cardAccountId,
            int? arAccountId,
            int? cashVarianceAccountId,
            int? customerAdvanceAccountId,
            int? apAccountId,
            int? loyaltyPayableAccountId,
            int? loyaltyExpenseAccountId,
            int? freeIssueExpenseAccountId,
            int updatedBy);
    }
}
