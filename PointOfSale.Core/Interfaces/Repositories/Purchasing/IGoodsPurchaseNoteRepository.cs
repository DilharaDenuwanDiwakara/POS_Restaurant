using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using PointOfSale.Core.Models.Purchasing;

namespace PointOfSale.Core.Interfaces.Repositories.Purchasing
{
    public interface IGoodsPurchaseNoteRepository
    {
        Task<long> CreateAsync(GoodPurchaseNote goodsPurchaseNote);
        Task<IEnumerable<GoodPurchaseNote>> GetAllAsync(int branchId, int? supplierId, DateTime? dateFrom, DateTime? dateTo);

        Task<IEnumerable<GoodPurchaseNote>> GetPendingReceiptPOsAsync();
        Task<IEnumerable<GoodsPurchaseNoteLine>> GetPOLinesAsync(long purchaseNoteId);
        Task<DataTable> GetPurchaseOrderReportDataAsync(long purchaseNoteId);

        Task<IEnumerable<GoodPurchaseNote>> GetPOsForApprovalAsync(int branchId, int? supplierId, DateTime? dateFrom, DateTime? dateTo);
        Task ApproveRejectPOAsync(long purchaseNoteId, bool isApproved, int actionBy, string remarks);
        Task SoftDeletePOAsync(long purchaseNoteId, int deletedBy);
    }
}
