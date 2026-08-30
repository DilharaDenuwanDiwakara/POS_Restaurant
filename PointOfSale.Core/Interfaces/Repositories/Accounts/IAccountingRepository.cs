using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;

namespace PointOfSale.Core.Interfaces
{
    public interface IAccountingRepository
    {
        Task<IEnumerable<AccountDto>> GetAccountsAsync();
        Task<IEnumerable<AccountDto>> GetPaymentAccountsAsync();
        Task<int> CreateAccountAsync(AccountDto account);
        Task UpdateAccountAsync(AccountDto account);
        Task DeactivateAccountAsync(int accountId);
        Task<int> CreateAccountTypeAsync(AccountType accountType);
        Task UpdateAccountTypeAsync(AccountType accountType);
        Task DeactivateAccountTypeAsync(int accountTypeId);

        Task<bool> AccountCodeExistsAsync(string code, int? excludeAccountId = null);

        Task<IEnumerable<AccountType>> GetAccountTypesAsync();
    }
}
