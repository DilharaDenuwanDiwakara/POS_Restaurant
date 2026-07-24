using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.Models.Accounts.Entities;

namespace PointOfSale.Core.Interfaces.Repositories.Accounts
{
    public interface IExpensesCategoryRepository
    {
        Task<int> CreateAsync(ExpensesCategory expensesCategory);
        Task<IEnumerable<ExpensesCategory>> GetAllAsync();
        Task UpdateAsync(ExpensesCategory expensesCategory);
        Task DeleteAsync(int id);
    }
}
