using System;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;

namespace PointOfSale.Core.Interfaces
{
    public interface IAccountReportRepository
    {
        Task<AccountInquiryResponseDto> GetAccountInquiryAsync(int accountId, DateTime fromDate, DateTime toDate);
    }
}
