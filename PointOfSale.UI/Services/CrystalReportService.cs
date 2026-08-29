using System;
using System.Data;
using System.Data.SqlClient;
using CrystalDecisions.CrystalReports.Engine;
using CrystalDecisions.Shared;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Sales;
using PointOfSale.Core.Interfaces.Services;
using PointOfSale.Core.Services;
using PointOfSale.Infrastructure;
using PointOfSale.UI.Reports;

namespace PointOfSale.UI.Services
{
    public class CrystalReportService : IReportService
    {
        private readonly ISalesRepository _salesRepository;
        private readonly ISalesReportService _salesReportService;
        private readonly IConfigurationService _configurationService;
        private readonly DatabaseConnection _databaseConnection;

        public CrystalReportService(
            ISalesRepository salesRepository,
            ISalesReportService salesReportService,
            IConfigurationService configurationService,
            DatabaseConnection databaseConnection)
        {
            _salesRepository = salesRepository ?? throw new ArgumentNullException(nameof(salesRepository));
            _salesReportService = salesReportService ?? throw new ArgumentNullException(nameof(salesReportService));
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            _databaseConnection = databaseConnection ?? throw new ArgumentNullException(nameof(databaseConnection));
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
                // BUG FIX 1: Apply Credentials! Even when using a DataTable, if there is a 
                // subreport inside SalesInvoice.rpt, it needs DB credentials to run.
                ApplyReportCredentials(report);

                report.SetDataSource(invoiceData);

                // BUG FIX 2: Set the parameter just in case the report or subreport expects it.
                SetSalesIdParameter(report, salesId);

                report.PrintOptions.PrinterName = GetLocalPrinterNameOrThrow();
                report.PrintToPrinter(1, false, 1, 0);
            }
        }

        public void PrintSettlementReceipt(long salesId)
        {
            if (salesId <= 0)
                throw new ArgumentOutOfRangeException(nameof(salesId), "Sales id must be greater than zero.");

            DataTable settlementData = _salesRepository.GetSettlementReceiptData(salesId);
            if (settlementData == null || settlementData.Rows.Count == 0)
                throw new InvalidOperationException("No settlement data was found for printing.");

            using (var report = new SettlementReceipt())
            {
                report.SetDataSource(settlementData);
                SetSalesIdParameter(report, salesId);

                report.PrintOptions.PrinterName = GetLocalPrinterNameOrThrow();
                report.PrintToPrinter(1, false, 1, 0);
            }
        }

        public void PrintSalesReport(string reportTypeKey, int userId, int? branchId, DateTime startDate, DateTime endDate, string outputPdfPath)
        {
            if (string.IsNullOrWhiteSpace(outputPdfPath))
                throw new ArgumentException("Output PDF path is required.", nameof(outputPdfPath));

            using (var report = ResolveSalesReport(reportTypeKey))
            {
                // Apply credentials defensively in case the .rpt has a subreport that
                // still needs a live DB connection (mirrors PrintSalesInvoice).
                ApplyReportCredentials(report);

                var request = new SalesReportRequestDto
                {
                    UserId = userId,
                    BranchId = branchId,
                    StartDate = startDate,
                    EndDate = endDate
                };

                DataTable reportData = _salesReportService.GenerateReportAsync(reportTypeKey, request).GetAwaiter().GetResult();
                if (reportData == null)
                    throw new InvalidOperationException("No data was found for the selected sales report.");

                reportData.TableName = GetSalesReportTableName(reportTypeKey);

                report.SetDataSource(reportData);
                SetSalesReportParameters(report, userId, branchId, startDate, endDate);
                report.ExportToDisk(ExportFormatType.PortableDocFormat, outputPdfPath);
            }
        }

        private static string GetSalesReportTableName(string reportTypeKey)
        {
            switch (reportTypeKey)
            {
                case SalesReportService.SalesSummaryKey:
                    return "uspGetSalesSummaryReport";
                case SalesReportService.SalesDetailKey:
                    return "uspGetSalesDetailReport";
                case SalesReportService.PaymentModeWiseKey:
                    return "uspGetPaymentModeWiseSalesReport";
                default:
                    return reportTypeKey;
            }
        }

        /// <summary>
        /// Points every table (main report and subreports) at whichever connection
        /// DatabaseConnection is currently configured for (Local or Server), overriding
        /// whatever connection the .rpt was designed against. This must stay provider/type
        /// neutral - only server, database and credentials change between environments.
        /// </summary>
        private void ApplyReportCredentials(ReportDocument report)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));

            string connectionString;
            using (var connection = _databaseConnection.GetConnection())
            {
                connectionString = connection.ConnectionString;
            }

            var builder = new SqlConnectionStringBuilder(connectionString);

            ApplyReportCredentials(report.Database.Tables, builder);

            foreach (ReportDocument subreport in report.Subreports)
            {
                ApplyReportCredentials(subreport.Database.Tables, builder);
            }
        }

        private static void ApplyReportCredentials(System.Collections.IEnumerable tables, SqlConnectionStringBuilder builder)
        {
            foreach (Table table in tables)
            {
                TableLogOnInfo logOnInfo = table.LogOnInfo;

                // The .rpt bakes in whichever connection (and auth mode) it was designed
                // against - e.g. Windows-integrated LogonProperties from LocalConnection.
                // Clear those before applying new credentials so a stale Trusted_Connection
                // flag can't collide with SQL-auth credentials for ServerConnection (or vice versa).
                logOnInfo.ConnectionInfo.LogonProperties.Clear();

                logOnInfo.ConnectionInfo.ServerName = builder.DataSource;
                logOnInfo.ConnectionInfo.DatabaseName = builder.InitialCatalog;
                logOnInfo.ConnectionInfo.IntegratedSecurity = builder.IntegratedSecurity;
                logOnInfo.ConnectionInfo.UserID = builder.IntegratedSecurity ? string.Empty : builder.UserID;
                logOnInfo.ConnectionInfo.Password = builder.IntegratedSecurity ? string.Empty : builder.Password;

                table.ApplyLogOnInfo(logOnInfo);
                ResetQualifiedTableLocation(table);
            }
        }

        private static void ResetQualifiedTableLocation(Table table)
        {
            if (table == null || string.IsNullOrWhiteSpace(table.Location) || string.IsNullOrWhiteSpace(table.Name))
                return;

            if (table.Location.IndexOf('.') >= 0 || table.Location.IndexOf('\\') >= 0 || table.Location.IndexOf('/') >= 0)
            {
                table.Location = table.Name;
            }
        }

        private static ReportDocument ResolveSalesReport(string reportTypeKey)
        {
            switch (reportTypeKey)
            {
                case SalesReportService.SalesSummaryKey:
                    return new SalesSummaryReport();
                case SalesReportService.SalesDetailKey:
                    throw new InvalidOperationException("A print template for the Sales Detail report has not been added yet.");
                case SalesReportService.PaymentModeWiseKey:
                    throw new InvalidOperationException("A print template for the Payment Mode Wise report has not been added yet.");
                default:
                    throw new InvalidOperationException("Unknown sales report type.");
            }
        }

        private static void SetSalesReportParameters(ReportDocument report, int userId, int? branchId, DateTime startDate, DateTime endDate)
        {
            TrySetParameterValue(report, "UserId", userId);
            TrySetParameterValue(report, "BranchId", branchId ?? 0);
            TrySetParameterValue(report, "StartDate", startDate.Date);
            TrySetParameterValue(report, "EndDate", endDate.Date.AddDays(1).AddTicks(-1));
        }

        private static void TrySetParameterValue(ReportDocument report, string name, object value)
        {
            try
            {
                report.SetParameterValue(name, value);
            }
            catch
            {
                try
                {
                    report.SetParameterValue("@" + name, value);
                }
                catch
                {
                    // Parameter not defined on this report; ignore.
                }
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
            // Pass to the main report
            TrySetParameterValue(report, "@SalesId", salesId);
            TrySetParameterValue(report, "SalesId", salesId);

            // If a subreport expects @SalesId and doesn't get it, Logon Failed is thrown.
            foreach (ReportDocument subreport in report.Subreports)
            {
                TrySetParameterValue(subreport, "@SalesId", salesId);
                TrySetParameterValue(subreport, "SalesId", salesId);
            }
        }
    }
}
