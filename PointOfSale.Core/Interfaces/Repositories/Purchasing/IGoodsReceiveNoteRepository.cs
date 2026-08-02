using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using PointOfSale.Core.Models.Purchasing;

namespace PointOfSale.Core.Interfaces.Repositories.Purchasing
{
    public interface IGoodsReceiveNoteRepository
    {
        Task<long> CreateAsync(GoodsReceiveNote goodsReceiveNote);

        Task<IEnumerable<GoodsReceiveNote>> GetAllAsync(int? supplierId, DateTime? dateFrom, DateTime? dateTo);
        Task<IEnumerable<GoodsReceiveNote>> GetPendingApprovalsAsync(int branchId, int? supplierId, DateTime? dateFrom, DateTime? dateTo);
        Task<IEnumerable<GoodsReceiveNoteLine>> GetLinesByGRNIdAsync(long goodsReceiveNoteId);
        Task<DataTable> GetGoodsReceiveNoteReportDataAsync(long goodsReceiveNoteId);
        Task ApproveRejectAsync(long goodsReceiveNoteId, bool isApproved, int actionBy, string remarks);
        Task ResubmitRejectedAsync(GoodsReceiveNote goodsReceiveNote);
        Task<decimal> GetLastGrnCostPriceByProductIdAsync(int productId);
    }
}
