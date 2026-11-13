using System;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;

namespace PointOfSale.Core.Interfaces.Repositories.Restaurant
{
    public interface IRestaurantReportRepository
    {
        Task<RestaurantDashboardDto> GetDashboardAsync(int? branchId, DateTime startDate, DateTime endDate);
    }
}
