using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.Models.Accounts.Entities;

namespace PointOfSale.Core.Interfaces.Repositories.Accounts
{
    public interface IOpeningBalanceRepository
    {
        Task<bool> OpeningBalanceExistsAsync(int locationId);

        Task<long> PostOpeningBalanceAsync(
            int locationId,
            DateTime asOfDate,
            int equityAccountId,
            int createdBy,
            IEnumerable<OpeningBalanceLine> lines);
    }
}
