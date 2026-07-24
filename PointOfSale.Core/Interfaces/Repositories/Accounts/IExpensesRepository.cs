using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.Models.Accounts.Entities;

namespace PointOfSale.Core.Interfaces.Repositories.Accounts
{
    public interface IExpensesRepository
    {
        Task<int> CreateAsync(Expenses expenses);
        Task<IEnumerable<Expenses>> GetAllAsync(int locationId);
    }
}
