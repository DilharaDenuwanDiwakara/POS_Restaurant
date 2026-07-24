using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;

namespace PointOfSale.Core.Interfaces.Repositories.Restaurant
{
    public interface IOrderRepository
    {
        Task<IEnumerable<ServedOrderDto>> GetServedOrdersAsync(int branchId);
        Task<IEnumerable<OrderItemDto>> GetOrderItemsAsync(long orderId);
    }
}
