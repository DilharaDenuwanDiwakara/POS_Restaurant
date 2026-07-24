using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.Models.Accounts;

namespace PointOfSale.Core.Interfaces.Repositories.Accounts
{
    public interface ICustomerAdvanceRepository
    {
        Task<long> CreateAsync(CustomerAdvance customerAdvance);

        Task<IEnumerable<CustomerAdvanceList>> GetUnappliedAdvanceAsync(int? customerId);

        Task CancelAdvanceAsync(long advanceId, int cancelledByUserId);
    }
}
