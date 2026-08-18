namespace PointOfSale.Core.Interfaces.Services
{
    public interface IReportService
    {
        void PrintSalesInvoice(long salesId);
        void PrintSettlementReceipt(long salesId);
    }
}
