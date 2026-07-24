using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.Models.Sales;

namespace PointOfSale.Core.Interfaces.Repositories.Sales
{
    public interface ICustomerRepository
    {
        Task<int> CreateAsync(Customer customer);
        Task<IEnumerable<Customer>> GetAllAsync();
        Task<Customer> GetByIdAsync(int id);
        Task<Customer> GetCustomerByPhoneAsync(string phone);
        Task UpdateAsync(Customer customer);
        Task<int> GetLoyaltyPointsAsync(int customerId);
        Task<int> AdjustLoyaltyPointsAsync(int customerId, int pointsDelta, string reason, long? salesId, int createdBy, string remarks = null);
    }
}
