using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.Models.Accounts.DTOs;

namespace PointOfSale.Core.Interfaces.Repositories.Accounts
{
    public interface ISupplierCreditRepository
    {
        Task<IEnumerable<SupplierCredit>> GetAvailableCreditsAsync(int supplierId);
    }
}
