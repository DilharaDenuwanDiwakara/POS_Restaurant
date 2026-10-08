using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Microsoft.Win32;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.System;
using PointOfSale.Core.Interfaces.Services;
using PointOfSale.Core.Models.System;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Sales
{
    public class SalesReportsViewModel : BaseViewModel
    {
        private readonly ISalesReportService _salesReportService;
        private readonly IBranchRepository _branchRepository;
        private readonly IReportService _reportService;
        private readonly IExcelService _excelService;
        private readonly IUserSessionService _userSessionService;
        private readonly AsyncRelayCommand _generateReportCommand;
        private readonly AsyncRelayCommand _printReportCommand;
        private readonly AsyncRelayCommand _exportCommand;
        private DataTable _lastPaymentReport;

        public SalesReportsViewModel(
            ISalesReportService salesReportService,
            IBranchRepository branchRepository,
            IReportService reportService,
            IExcelService excelService,
            IUserSessionService userSessionService)
        {
            _salesReportService = salesReportService;
            _branchRepository = branchRepository;
            _reportService = reportService;
            _excelService = excelService;
            _userSessionService = userSessionService;

            foreach (var reportType in _salesReportService.GetAvailableReportTypes())
                ReportTypes.Add(reportType);

            SelectedReportType = ReportTypes.FirstOrDefault();
            StartDate = DateTime.Today;
            EndDate = DateTime.Today;
            ReportTitle = "Sales Reports";

            _generateReportCommand = new AsyncRelayCommand(_ => GenerateReportAsync(), _ => !IsProcessing);
            GenerateReportCommand = _generateReportCommand;

            _printReportCommand = new AsyncRelayCommand(_ => PrintReportAsync(), _ => !IsProcessing);
            PrintReportCommand = _printReportCommand;

            _exportCommand = new AsyncRelayCommand(_ => ExportAsync(), _ => !IsProcessing && ReportRows != null && ReportRows.Count > 0);
            ExportCommand = _exportCommand;

            _ = LoadInitialDataAsync();
        }

        public ObservableCollection<Branch> Branches { get; } = new ObservableCollection<Branch>();
        public ObservableCollection<SalesReportTypeDto> ReportTypes { get; } = new ObservableCollection<SalesReportTypeDto>();

        private Branch _selectedBranch;
        public Branch SelectedBranch
        {
            get => _selectedBranch;
            set => SetProperty(ref _selectedBranch, value);
        }

        private DateTime _startDate;
        public DateTime StartDate
        {
            get => _startDate;
            set => SetProperty(ref _startDate, value);
        }

        private DateTime _endDate;
        public DateTime EndDate
        {
            get => _endDate;
            set => SetProperty(ref _endDate, value);
        }

        private SalesReportTypeDto _selectedReportType;
        public SalesReportTypeDto SelectedReportType
        {
            get => _selectedReportType;
            set
            {
                if (SetProperty(ref _selectedReportType, value))
                    ReportTitle = value?.DisplayName ?? "Sales Reports";
            }
        }

        private DataView _reportRows;
        public DataView ReportRows
        {
            get => _reportRows;
            set => SetProperty(ref _reportRows, value);
        }

        private int _recordCount;
        public int RecordCount
        {
            get => _recordCount;
            set => SetProperty(ref _recordCount, value);
        }

        private decimal _totalSalesAmount;
        public decimal TotalSalesAmount
        {
            get => _totalSalesAmount;
            set => SetProperty(ref _totalSalesAmount, value);
        }

        private string _reportTitle;
        public string ReportTitle
        {
            get => _reportTitle;
            set => SetProperty(ref _reportTitle, value);
        }

        private ISeries[] _dailySalesSeries = new ISeries[0];
        public ISeries[] DailySalesSeries
        {
            get => _dailySalesSeries;
            set => SetProperty(ref _dailySalesSeries, value);
        }

        private Axis[] _dailySalesXAxes = new Axis[0];
        public Axis[] DailySalesXAxes
        {
            get => _dailySalesXAxes;
            set => SetProperty(ref _dailySalesXAxes, value);
        }

        private ISeries[] _paymentBreakdownSeries = new ISeries[0];
        public ISeries[] PaymentBreakdownSeries
        {
            get => _paymentBreakdownSeries;
            set => SetProperty(ref _paymentBreakdownSeries, value);
        }

        private bool _isProcessing;
        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                if (SetProperty(ref _isProcessing, value))
                {
                    _generateReportCommand?.RaiseCanExecuteChanged();
                    _printReportCommand?.RaiseCanExecuteChanged();
                    _exportCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        public ICommand GenerateReportCommand { get; }
        public ICommand PrintReportCommand { get; }
        public ICommand ExportCommand { get; }

        private async Task LoadInitialDataAsync()
        {
            try
            {
                IsProcessing = true;
                ErrorMessage = string.Empty;

                Branches.Clear();
                Branches.Add(new Branch { Id = 0, Name = "ALL BRANCHES", IsActive = true });

                var branches = await _branchRepository.GetAllAsync();
                foreach (var branch in branches.Where(b => b.IsActive).OrderBy(b => b.Name))
                    Branches.Add(branch);

                SelectedBranch = Branches.FirstOrDefault(b => b.Id == _userSessionService.BranchId)
                    ?? Branches.FirstOrDefault();
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Sales reports initialization failed: {ex}");
                ErrorMessage = $"Init Failed: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task GenerateReportAsync()
        {
            if (!ValidateFilters())
                return;

            try
            {
                IsProcessing = true;
                ErrorMessage = string.Empty;

                var request = CreateRequest();
                var selectedReport = await _salesReportService.GenerateReportAsync(SelectedReportType.Key, request);

                ReportRows = selectedReport.DefaultView;
                UpdateSummaryMetrics(selectedReport);
                _exportCommand?.RaiseCanExecuteChanged();

                var trendSource = SelectedReportType.Key == SalesReportService.SalesSummaryKey
                    ? selectedReport
                    : await _salesReportService.GenerateReportAsync(SalesReportService.SalesSummaryKey, request);

                var paymentSource = SelectedReportType.Key == SalesReportService.PaymentModeWiseKey
                    ? selectedReport
                    : await _salesReportService.GenerateReportAsync(SalesReportService.PaymentModeWiseKey, request);
                _lastPaymentReport = paymentSource;

                BuildDailySalesTrend(trendSource);
                BuildPaymentBreakdown(paymentSource);

                if (selectedReport.Rows.Count == 0)
                    MessageBox.Show("No records found for the selected sales report filters.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Sales report generation failed: {ex}");
                ErrorMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task PrintReportAsync()
        {
            if (!ValidateFilters())
                return;

            if (RecordCount == 0)
            {
                MessageBox.Show("Please generate a report before printing.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf",
                FileName = $"{SelectedReportType.Key}_{DateTime.Now:yyyyMMdd_HHmm}.pdf",
                Title = $"Save {SelectedReportType.DisplayName}"
            };

            if (saveDialog.ShowDialog() != true)
                return;

            try
            {
                IsProcessing = true;
                ErrorMessage = string.Empty;

                var request = CreateRequest();
                var reportKey = SelectedReportType.Key;
                var filePath = saveDialog.FileName;

                await Task.Run(() => _reportService.PrintSalesReport(reportKey, request.UserId, request.BranchId, request.StartDate, request.EndDate, filePath));

                if (MessageBox.Show("Report saved. Open now?", "Success", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Report Not Ready", MessageBoxButton.OK, MessageBoxImage.Information);
                ErrorMessage = "Report template missing.";
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Sales report print failed: {ex}");
                ErrorMessage = $"Print Error: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task ExportAsync()
        {
            if (!ValidateFilters())
                return;

            if (SelectedReportType?.Key != SalesReportService.SalesSummaryKey)
            {
                MessageBox.Show("Excel export is available for the Sales Summary report.", "Export Excel", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (ReportRows?.Table == null || ReportRows.Count == 0)
            {
                MessageBox.Show("Please generate a Sales Summary report before exporting.", "Export Excel", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                FileName = $"SalesSummary_{StartDate:yyyyMMdd}_{EndDate:yyyyMMdd}.xlsx",
                Title = "Export Sales Summary to Excel"
            };

            if (saveDialog.ShowDialog() != true)
                return;

            try
            {
                IsProcessing = true;
                ErrorMessage = string.Empty;

                var table = CreateSalesSummaryExportTable(ReportRows.Table);
                var companyName = GetFirstString(table, "CompanyName")
                    ?? (SelectedBranch != null && SelectedBranch.Id > 0 ? SelectedBranch.Name : "Nexora");
                var filePath = saveDialog.FileName;

                await Task.Run(() => _excelService.ExportSalesSummaryReport(table, companyName, StartDate, EndDate, filePath));

                if (MessageBox.Show("Excel report saved. Open now?", "Export Excel", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                    Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Sales summary Excel export failed: {ex}");
                MessageBox.Show($"Failed to export Sales Summary report: {ex.Message}", "Export Excel", MessageBoxButton.OK, MessageBoxImage.Error);
                ErrorMessage = $"Export Error: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private bool ValidateFilters()
        {
            if (SelectedBranch == null)
            {
                MessageBox.Show("Please select a Branch.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (SelectedReportType == null)
            {
                MessageBox.Show("Please select a Report Type.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (StartDate > EndDate)
            {
                MessageBox.Show("Start Date cannot be after End Date.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private SalesReportRequestDto CreateRequest()
        {
            return new SalesReportRequestDto
            {
                UserId = _userSessionService.UserId,
                BranchId = SelectedBranch != null && SelectedBranch.Id > 0 ? (int?)SelectedBranch.Id : null,
                StartDate = StartDate,
                EndDate = EndDate
            };
        }

        private DataTable CreateSalesSummaryExportTable(DataTable source)
        {
            var table = CreateWritableCopy(source);
            if (table.Rows.Count == 0)
                return table;

            var hasSalesSummaryPayModeColumns =
                FindColumn(table, "PayModeCashTotal", "CashTotal", "CashAmount") != null ||
                FindColumn(table, "PayModeCardTotal", "CardTotal", "CardAmount") != null ||
                FindColumn(table, "PayModeBankTransferTotal", "BankTransferTotal", "BankTransferAmount") != null;

            decimal cashTotal;
            decimal cardTotal;
            decimal bankTransferTotal;

            if (hasSalesSummaryPayModeColumns)
            {
                ResolvePaymentTotals(table, out cashTotal, out cardTotal, out bankTransferTotal);
            }
            else if (_lastPaymentReport != null && _lastPaymentReport.Rows.Count > 0)
            {
                ResolvePaymentTotals(_lastPaymentReport, out cashTotal, out cardTotal, out bankTransferTotal);
            }
            else
            {
                cashTotal = 0m;
                cardTotal = 0m;
                bankTransferTotal = 0m;
            }

            EnsureColumn(table, "PayModeCashTotal");
            EnsureColumn(table, "PayModeCardTotal");
            EnsureColumn(table, "PayModeBankTransferTotal");

            table.Rows[0]["PayModeCashTotal"] = cashTotal;
            table.Rows[0]["PayModeCardTotal"] = cardTotal;
            table.Rows[0]["PayModeBankTransferTotal"] = bankTransferTotal;

            return table;
        }

        private static DataTable CreateWritableCopy(DataTable source)
        {
            var table = source.Clone();

            foreach (DataColumn column in table.Columns)
            {
                if (string.IsNullOrEmpty(column.Expression))
                    column.ReadOnly = false;
            }

            foreach (DataRow row in source.Rows)
                table.ImportRow(row);

            return table;
        }

        private static void EnsureColumn(DataTable table, string columnName)
        {
            if (!table.Columns.Contains(columnName))
                table.Columns.Add(columnName, typeof(decimal));
        }

        private static string GetFirstString(DataTable table, string columnName)
        {
            if (table == null || string.IsNullOrEmpty(columnName) || !table.Columns.Contains(columnName))
                return null;

            foreach (DataRow row in table.Rows)
            {
                var value = Convert.ToString(row[columnName]);
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }

            return null;
        }

        private static void ResolvePaymentTotals(DataTable table, out decimal cashTotal, out decimal cardTotal, out decimal bankTransferTotal)
        {
            cashTotal = 0m;
            cardTotal = 0m;
            bankTransferTotal = 0m;

            var directCashColumn = FindColumn(table, "PayModeCashTotal", "CashTotal", "CashAmount");
            var directCardColumn = FindColumn(table, "PayModeCardTotal", "CardTotal", "CardAmount");
            var directBankTransferColumn = FindColumn(table, "PayModeBankTransferTotal", "BankTransferTotal", "BankTransferAmount");

            if (directCashColumn != null || directCardColumn != null || directBankTransferColumn != null)
            {
                cashTotal = GetFirstNonZeroDecimal(table, directCashColumn);
                cardTotal = GetFirstNonZeroDecimal(table, directCardColumn);
                bankTransferTotal = GetFirstNonZeroDecimal(table, directBankTransferColumn);
                return;
            }

            var methodColumn = FindColumn(table, "PaymentMethod", "PaymentMode", "PaymentType", "Mode");
            var amountColumn = FindColumn(table, "Amount", "TotalAmount", "NetAmount", "PaidAmount", "SalesAmount");

            if (methodColumn == null || amountColumn == null)
                return;

            foreach (DataRow row in table.Rows)
            {
                var method = Convert.ToString(row[methodColumn]) ?? string.Empty;
                var amount = GetDecimal(row, amountColumn);
                var normalizedMethod = method.Trim().ToUpperInvariant();

                if (normalizedMethod.Contains("CASH"))
                    cashTotal += amount;
                else if (normalizedMethod.Contains("CARD"))
                    cardTotal += amount;
                else if (normalizedMethod.Contains("BANK") || normalizedMethod.Contains("TRANSFER"))
                    bankTransferTotal += amount;
            }
        }

        private static decimal GetFirstNonZeroDecimal(DataTable table, string columnName)
        {
            if (string.IsNullOrEmpty(columnName))
                return 0m;

            foreach (DataRow row in table.Rows)
            {
                var value = GetDecimal(row, columnName);
                if (value != 0m)
                    return value;
            }

            return 0m;
        }

        private void BuildDailySalesTrend(DataTable table)
        {
            var dateColumn = FindColumn(table, "SalesDate", "SaleDate", "ReturnDate", "ReportDate", "Date", "BusinessDate");
            var amountColumn = FindColumn(table, "NetAmount", "TotalSales", "SalesAmount", "TotalAmount", "HeaderTotalRefund", "LineRefundAmount", "Amount", "GrossAmount");

            if (dateColumn == null || amountColumn == null || table.Rows.Count == 0)
            {
                DailySalesSeries = new ISeries[0];
                DailySalesXAxes = new Axis[0];
                return;
            }

            var points = table.Rows.Cast<DataRow>()
                .Where(row => row[dateColumn] != DBNull.Value)
                .GroupBy(row => Convert.ToDateTime(row[dateColumn]).Date)
                .OrderBy(group => group.Key)
                .Select(group => new
                {
                    Date = group.Key,
                    Amount = group.Sum(row => GetDecimal(row, amountColumn))
                })
                .ToList();

            DailySalesSeries = new ISeries[]
            {
                new LineSeries<double>
                {
                    Name = "Daily Sales",
                    Values = points.Select(p => (double)p.Amount).ToArray(),
                    GeometrySize = 8,
                    Fill = null
                }
            };

            DailySalesXAxes = new Axis[]
            {
                new Axis
                {
                    Labels = points.Select(p => p.Date.ToString("dd MMM", CultureInfo.CurrentCulture)).ToArray()
                }
            };
        }

        private void BuildPaymentBreakdown(DataTable table)
        {
            var methodColumn = FindColumn(table, "PaymentMethod", "PaymentMode", "PaymentType", "Mode");
            var amountColumn = FindColumn(table, "Amount", "TotalAmount", "NetAmount", "PaidAmount", "SalesAmount");

            if (methodColumn == null || amountColumn == null || table.Rows.Count == 0)
            {
                PaymentBreakdownSeries = new ISeries[0];
                return;
            }

            PaymentBreakdownSeries = table.Rows.Cast<DataRow>()
                .GroupBy(row => Convert.ToString(row[methodColumn]))
                .Where(group => !string.IsNullOrWhiteSpace(group.Key))
                .Select(group => new PieSeries<double>
                {
                    Name = group.Key,
                    Values = new[] { (double)group.Sum(row => GetDecimal(row, amountColumn)) },
                    ToolTipLabelFormatter = point => $"{point.Context.Series.Name}: {point.Coordinate.PrimaryValue:N2}",
                    DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                    DataLabelsFormatter = point => $"{point.Context.Series.Name}"
                })
                .Cast<ISeries>()
                .ToArray();
        }

        private void UpdateSummaryMetrics(DataTable table)
        {
            if (SelectedReportType?.Key == SalesReportService.SalesReturnDetailKey)
            {
                UpdateSalesReturnSummaryMetrics(table);
                return;
            }

            RecordCount = table?.Rows.Count ?? 0;
            TotalSalesAmount = ResolveTotalAmount(table);
        }

        private void UpdateSalesReturnSummaryMetrics(DataTable table)
        {
            var returnNumberColumn = FindColumn(table, "ReturnNumber");
            var headerRefundColumn = FindColumn(table, "HeaderTotalRefund");

            if (table == null || table.Rows.Count == 0 || returnNumberColumn == null)
            {
                RecordCount = 0;
                TotalSalesAmount = 0m;
                return;
            }

            var returnGroups = table.Rows.Cast<DataRow>()
                .Where(row => row[returnNumberColumn] != DBNull.Value)
                .GroupBy(row => Convert.ToString(row[returnNumberColumn]))
                .Where(group => !string.IsNullOrWhiteSpace(group.Key))
                .ToList();

            RecordCount = returnGroups.Count;

            if (headerRefundColumn != null)
            {
                TotalSalesAmount = returnGroups.Sum(group => GetDecimal(group.First(), headerRefundColumn));
                return;
            }

            var lineRefundColumn = FindColumn(table, "LineRefundAmount");
            TotalSalesAmount = lineRefundColumn == null
                ? 0m
                : table.Rows.Cast<DataRow>().Sum(row => GetDecimal(row, lineRefundColumn));
        }

        private decimal ResolveTotalAmount(DataTable table)
        {
            var amountColumn = FindColumn(table, "NetAmount", "TotalSales", "SalesAmount", "TotalAmount", "HeaderTotalRefund", "LineRefundAmount", "Amount", "GrossAmount");
            return amountColumn == null
                ? 0m
                : table.Rows.Cast<DataRow>().Sum(row => GetDecimal(row, amountColumn));
        }

        private static string FindColumn(DataTable table, params string[] candidates)
        {
            if (table == null)
                return null;

            foreach (var candidate in candidates)
            {
                foreach (DataColumn column in table.Columns)
                {
                    if (string.Equals(column.ColumnName, candidate, StringComparison.OrdinalIgnoreCase))
                        return column.ColumnName;
                }
            }

            return null;
        }

        private static decimal GetDecimal(DataRow row, string columnName)
        {
            if (row == null || string.IsNullOrEmpty(columnName) || row[columnName] == DBNull.Value)
                return 0m;

            decimal value;
            return decimal.TryParse(Convert.ToString(row[columnName]), NumberStyles.Any, CultureInfo.CurrentCulture, out value)
                ? value
                : 0m;
        }
    }
}
