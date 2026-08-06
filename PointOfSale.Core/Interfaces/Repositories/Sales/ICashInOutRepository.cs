using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using PointOfSale.Core.Models.Sales;

namespace PointOfSale.Core.Interfaces.Repositories.Sales
{
    public interface ICashInOutRepository
    {
        Task<long> CreateAsync(CashInOut cashInOut);
        Task<IEnumerable<CashInOut>> GetAllAsync(int locationId);
        DataTable GetCashReceiptData(long transactionId);
    }
}
