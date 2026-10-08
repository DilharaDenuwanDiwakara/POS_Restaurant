using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Models.Inventory;
using PointOfSale.Core.Models.Purchasing;

namespace PointOfSale.Core.Interfaces.Services
{
    public interface IExcelService
    {
        Task<List<OpeningStockItemDto>> ImportOpeningStockAsync(string filePath, int userId);

        void ExportSuppliers(IEnumerable<Supplier> suppliers, string filePath);

        void ExportProducts(IEnumerable<Product> products, string filePath);

        void ExportMenuProfitability(IEnumerable<MenuProfitabilityDto> items, string filePath);

        void ExportSalesSummaryReport(DataTable data, string companyName, DateTime fromDate, DateTime toDate, string filePath);
    }
}
