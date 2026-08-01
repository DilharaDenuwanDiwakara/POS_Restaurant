using System;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Services;

namespace PointOfSale.UI.Services
{
    public class UnavailableSalesInvoiceReportPreviewService : ISalesInvoiceReportPreviewService
    {
        public Task ShowPreviewAsync(long salesId)
        {
            throw new InvalidOperationException(
                "Crystal Reports is not installed on this machine. Install the SAP Crystal Reports runtime or developer components to preview invoices.");
        }
    }
}
