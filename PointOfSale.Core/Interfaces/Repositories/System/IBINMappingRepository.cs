using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.Models.System;

namespace PointOfSale.Core.Interfaces.Repositories.System
{
    public interface IBINMappingRepository
    {
        Task<IEnumerable<BankBINMapping>> GetByBankIdAsync(int bankId);
        Task<int> InsertAsync(BankBINMapping bin);
        Task UpdateAsync(BankBINMapping bin);
        Task DeleteAsync(int id, int bankId);
    }
}
