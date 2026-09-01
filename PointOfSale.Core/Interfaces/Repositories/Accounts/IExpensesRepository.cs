using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using PointOfSale.Core.Models.Accounts.Entities;

namespace PointOfSale.Core.Interfaces.Repositories.Accounts
{
    public interface IExpensesRepository
    {
        Task<int> CreateAsync(ExpenseHeader expense);
        Task<IEnumerable<Expenses>> GetAllAsync(int branchId);
        Task<IEnumerable<Expenses>> GetAllAsync(int branchId, DateTime fromDate, DateTime toDate);
        Task<DataTable> GetExpenseVoucherAsync(int expensesId);
    }
}
