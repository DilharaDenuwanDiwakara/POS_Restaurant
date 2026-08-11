using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.Models.Inventory;

namespace PointOfSale.Core.Interfaces.Repositories.Inventory
{
    public interface IWastageRepository
    {
        Task<IEnumerable<WastageReason>> GetReasonsAsync();
        Task<long> CreateWastageAsync(Wastage wastage);
        Task<List<PendingWastageModel>> GetPendingWastageAsync(int branchId);
        Task<List<WastageDetailModel>> GetWastageDetailsAsync(long wastageId);
        Task ApproveWastageAsync(long wastageId, int approvedBy);
        Task RejectWastageAsync(long wastageId, int rejectedBy, string remarks);
    }
}
