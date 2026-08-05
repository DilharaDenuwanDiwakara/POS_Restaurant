using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.Models.System;

namespace PointOfSale.Core.Interfaces.Repositories.System
{
    public interface IBankBranchRepository
    {
        Task<IEnumerable<BankBranch>> GetAllAsync();
        Task<IEnumerable<BankBranch>> GetByBankIdAsync(int bankId);
        Task<int> CreateAsync(BankBranch branch);
        Task UpdateAsync(BankBranch branch);
        Task DeleteAsync(int id);
    }
}
