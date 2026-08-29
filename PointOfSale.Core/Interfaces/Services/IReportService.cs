using System;

namespace PointOfSale.Core.Interfaces.Services
{
    public interface IReportService
    {
        void PrintSalesInvoice(long salesId);
        void PrintSettlementReceipt(long salesId);
        void PrintSalesReport(string reportTypeKey, int userId, int? branchId, DateTime startDate, DateTime endDate, string outputPdfPath);
    }
}
