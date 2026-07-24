using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.Models.Accounts;

namespace PointOfSale.Core.Interfaces.Repositories.Accounts
{
    public interface ISupplierAdvanceRepository
    {
        Task<long> CreateAsync(SupplierAdvance supplierAdvance);

        Task<IEnumerable<SupplierAdvanceList>> GetUnappliedAdvanceAsync(int? supplierId);

        Task CancelAdvanceAsync(long advanceId, int cancelledByUserId);
    }
}
