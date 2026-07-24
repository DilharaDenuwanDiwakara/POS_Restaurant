using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.Models.System;

namespace PointOfSale.Core.Interfaces.Repositories.System
{
    public interface IBankRepository
    {
        Task<IEnumerable<Bank>> GetAllAsync();
        Task<int> CreateAsync(Bank bank);
        Task UpdateAsync(Bank bank);
        Task DeleteAsync(int id);
    }
}
