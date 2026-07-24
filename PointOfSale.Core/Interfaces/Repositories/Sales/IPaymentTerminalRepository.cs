using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.Models.Sales;

namespace PointOfSale.Core.Interfaces.Repositories.Sales
{
    public interface IPaymentTerminalRepository
    {
        Task<IEnumerable<PaymentTerminal>> GetAllAsync();
        Task<int> CreateAsync(PaymentTerminal terminal);
        Task UpdateAsync(PaymentTerminal terminal);
        Task DeleteAsync(int id);
    }
}
