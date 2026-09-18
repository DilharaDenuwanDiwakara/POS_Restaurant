using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using PointOfSale.Core.Models.Inventory;

namespace PointOfSale.Core.Interfaces.Repositories.Inventory
{
    public interface IWastageRepository
    {
        Task<IEnumerable<WastageReason>> GetReasonsAsync();
        Task<long> CreateWastageAsync(Wastage wastage);
        Task<List<WastageModel>> GetWastageHistory(int branchId, DateTime fromDate, DateTime toDate, int locationId);
        Task<List<WastageLineModel>> GetWastageLines(int wastageId);
        Task<List<PendingWastageModel>> GetPendingWastageAsync(int branchId);
        Task<List<WastageDetailModel>> GetWastageDetailsAsync(long wastageId);
        Task ApproveWastageAsync(long wastageId, int approvedBy);
        Task RejectWastageAsync(long wastageId, int rejectedBy, string remarks);
        Task<DataTable> GetWastageReportDataAsync(long wastageId);
    }
}
