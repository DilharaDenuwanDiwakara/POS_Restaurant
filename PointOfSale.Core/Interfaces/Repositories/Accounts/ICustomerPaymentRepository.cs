using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.Models.Accounts;

namespace PointOfSale.Core.Interfaces.Repositories.Accounts
{
    public interface ICustomerPaymentRepository
    {
        Task<IEnumerable<CustomerReceivable>> GetCustomerReceivableAsync(int customerId);

        Task<long> CreateAsync(CustomerPayment customerPayment);
    }
}
