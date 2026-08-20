using System;
using System.Data;
using CrystalDecisions.CrystalReports.Engine;
using PointOfSale.Core.Interfaces.Repositories.Sales;
using PointOfSale.Core.Interfaces.Services;
using PointOfSale.UI.Reports;

namespace PointOfSale.UI.Services
{
    public class CrystalReportService : IReportService
    {
        private readonly ISalesRepository _salesRepository;
        private readonly IConfigurationService _configurationService;

        public CrystalReportService(ISalesRepository salesRepository, IConfigurationService configurationService)
        {
            _salesRepository = salesRepository ?? throw new ArgumentNullException(nameof(salesRepository));
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        }

        public void PrintSalesInvoice(long salesId)
        {
            if (salesId <= 0)
                throw new ArgumentOutOfRangeException(nameof(salesId), "Sales id must be greater than zero.");

            DataTable invoiceData = _salesRepository.GetInvoiceData(salesId);
            if (invoiceData == null || invoiceData.Rows.Count == 0)
                throw new InvalidOperationException("No invoice data was found for printing.");

            using (var report = new SalesInvoice())
            {
                report.SetDataSource(invoiceData);
                report.PrintOptions.PrinterName = GetLocalPrinterNameOrThrow();
                report.PrintToPrinter(1, false, 1, 0);
            }
        }

        public void PrintSettlementReceipt(long salesId)
        {
            if (salesId <= 0)
                throw new ArgumentOutOfRangeException(nameof(salesId), "Sales id must be greater than zero.");

            using (var report = new SettlementReceipt())
            {
                SetSalesIdParameter(report, salesId);
                report.PrintOptions.PrinterName = GetLocalPrinterNameOrThrow();
                report.PrintToPrinter(1, false, 1, 0);
            }
        }

        private string GetLocalPrinterNameOrThrow()
        {
            var printerName = _configurationService.GetLocalPrinterName();
            if (string.IsNullOrWhiteSpace(printerName))
                throw new InvalidOperationException("No POS printer configured. Set 'LocalPrinterName' in App.config.");

            return printerName;
        }

        private static void SetSalesIdParameter(ReportDocument report, long salesId)
        {
            try
            {
                report.SetParameterValue("@SalesId", salesId);
            }
            catch
            {
                report.SetParameterValue("SalesId", salesId);
            }
        }
    }
}
