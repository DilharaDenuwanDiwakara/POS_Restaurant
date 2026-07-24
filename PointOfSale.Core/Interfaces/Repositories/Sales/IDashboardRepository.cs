using System;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;

namespace PointOfSale.Core.Interfaces.Repositories.Sales
{
    public interface IDashboardRepository
    {
        Task<DashboardOverviewDto> GetOverviewAsync(int? branchId, DateTime fromDate, DateTime toDate);
    }
}
