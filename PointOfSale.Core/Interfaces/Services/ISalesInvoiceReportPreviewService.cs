using System.Threading.Tasks;

namespace PointOfSale.Core.Interfaces.Services
{
    public interface ISalesInvoiceReportPreviewService
    {
        Task ShowPreviewAsync(long salesId);
    }
}
