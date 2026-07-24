using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.Models.Purchasing;

namespace PointOfSale.Core.Interfaces.Purchasing
{
    public interface ISupplierRepository
    {
        Task<int> CreateAsync(Supplier supplier);
        Task<IEnumerable<Supplier>> GetAllAsync();
        Task<Supplier> GetByIdAsync(int supplierId);
        Task UpdateAsync(Supplier supplier);

        Task<IEnumerable<SupplierDocument>> GetDocumentsBySupplierIdAsync(int supplierId);
        Task<long> AddDocumentAsync(SupplierDocument document);
        Task DeleteDocumentAsync(long documentId);
    }
}
