using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using PointOfSale.Core.Models.Accounts.Entities;

namespace PointOfSale.Core.Interfaces.Repositories.Accounts
{
    public interface IFundTransferRepository
    {
        Task<long> CreateAsync(FundTransfer fundTransfer);
        Task<IEnumerable<FundTransfer>> GetAllAsync(int branchId, DateTime fromDate, DateTime toDate);
        Task<DataTable> GetFundTransferVoucherAsync(long fundTransferId);
    }
}
