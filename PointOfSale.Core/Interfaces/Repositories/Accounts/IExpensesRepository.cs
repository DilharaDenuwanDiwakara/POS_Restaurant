using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using PointOfSale.Core.Models.Accounts.Entities;

namespace PointOfSale.Core.Interfaces.Repositories.Accounts
{
    public interface IExpensesRepository
    {
        Task<int> CreateAsync(Expenses expenses);
        Task<IEnumerable<Expenses>> GetAllAsync(int locationId);
        Task<IEnumerable<Expenses>> GetAllAsync(int locationId, DateTime fromDate, DateTime toDate);
        Task<DataTable> GetExpenseVoucherAsync(int expensesId);
    }
}
