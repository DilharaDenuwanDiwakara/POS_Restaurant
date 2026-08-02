using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.Models.Inventory;
using PointOfSale.Core.Models.Purchasing;

namespace PointOfSale.Core.Interfaces.Services
{
    public interface IExcelService
    {
        Task<string> ImportOpeningStockAsync(string filePath, int userId);

        void ExportSuppliers(IEnumerable<Supplier> suppliers, string filePath);

        void ExportProducts(IEnumerable<Product> products, string filePath);
    }
}
