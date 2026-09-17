using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Models.Purchasing;

namespace PointOfSale.Core.Interfaces.Repositories.Purchasing
{
    public interface ISupplierReturnRepository
    {
        Task<int> CreateAsync(SupplierReturn supplierReturn);

        Task<IEnumerable<SupplierReturn>> GetAllAsync(int? supplierId, DateTime? dateFrom, DateTime? dateTo);

        Task<IEnumerable<SupplierReturn>> GetPendingApprovalsAsync(int branchId, int? supplierId, DateTime? dateFrom, DateTime? dateTo);

        Task<IEnumerable<SupplierReturnLine>> GetLinesByReturnIdAsync(int supplierReturnId);

        Task<IEnumerable<SupplierReturnLineDto>> GetLineDetailsAsync(int supplierReturnId);

        Task ApproveAsync(int supplierReturnId, int approvedBy);

        Task RejectAsync(int supplierReturnId, string approverRemark, int rejectedBy);

        Task<DataTable> GetSupplierReturnReportDataAsync(int supplierReturnId);

        Task<IEnumerable<ReturnReasonModel>> GetActiveReturnReasonsAsync();

        Task<OriginalGrnReturnLine> GetOriginalGrnLineForBatchAsync(long batchId);

    }
}
