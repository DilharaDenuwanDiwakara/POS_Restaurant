using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using PointOfSale.Core.Models.Accounts;
using PointOfSale.Core.Models.Accounts.Entities;

namespace PointOfSale.Core.Interfaces.Repositories.Accounts
{
    public interface ISupplierPaymentRepository
    {
        Task<IEnumerable<SupplierPayable>> GetSupplierPayableAsync(int supplierId);

        Task<long> CreateAsync(SupplierPayment supplierPayment);
        Task<DataSet> GetSupplierPaymentVoucherDataSetAsync(long supplierPaymentId);
        Task ProcessBulkPaymentAsync(int supplierId, string paymentMethod, DateTime paymentDate, decimal totalCash, int userId,
                                 List<SupplierSettlement> settlements, // <--- Used your Model
                                 List<SupplierPaymentLine> paymentLines);
    }
}
