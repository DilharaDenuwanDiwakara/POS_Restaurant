using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.Models.Sales;

namespace PointOfSale.Core.Interfaces.Repositories.Sales
{
    public interface ISalesHoldRepository
    {
        Task SalesHoldAsync(SalesHold hold);
        Task<List<SalesHold>> GetAllHoldsAsync(int locationId);
        Task<SalesHold> RecallHoldAsync(long holdId);
    }
}
