using System;
using System.Data;
using System.Threading.Tasks;
using System.Windows;
using PointOfSale.Core.Interfaces.Repositories.Sales;
using PointOfSale.Core.Interfaces.Services;
using PointOfSale.UI.Reports;
using PointOfSale.UI.Views.Sales;

namespace PointOfSale.UI.Services
{
    public class SalesInvoiceReportPreviewService : ISalesInvoiceReportPreviewService
    {
        private readonly ISalesRepository _salesRepository;

        public SalesInvoiceReportPreviewService(ISalesRepository salesRepository)
        {
            _salesRepository = salesRepository ?? throw new ArgumentNullException(nameof(salesRepository));
        }

        public async Task ShowPreviewAsync(long salesId)
        {
            if (salesId <= 0)
                throw new ArgumentOutOfRangeException(nameof(salesId), "Sales id must be greater than zero.");

            DataTable invoiceData = await Task.Run(() => _salesRepository.GetInvoiceData(salesId));

            if (invoiceData == null || invoiceData.Rows.Count == 0)
                throw new InvalidOperationException("No invoice data was found for the selected bill.");

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var reportDocument = new SalesInvoice();
                try
                {
                    reportDocument.SetDataSource(invoiceData);

                    var viewerWindow = new ZReportViewerWindow(reportDocument, disposeReportOnClose: true)
                    {
                        Title = "Invoice Reprint Preview"
                    };

                    var owner = Application.Current.MainWindow;
                    if (owner != null && owner != viewerWindow)
                    {
                        viewerWindow.Owner = owner;
                        viewerWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    }
                    else
                    {
                        viewerWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    }

                    viewerWindow.ShowDialog();
                    reportDocument = null;
                }
                finally
                {
                    if (reportDocument != null)
                    {
                        reportDocument.Close();
                        reportDocument.Dispose();
                    }
                }
            });
        }
    }
}
