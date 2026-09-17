using System;
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
        private readonly IUserSessionService _userSessionService;
        private readonly AsyncRelayCommand _generateReportCommand;
        private readonly AsyncRelayCommand _printReportCommand;

        public SalesReportsViewModel(ISalesReportService salesReportService, IBranchRepository branchRepository, IReportService reportService, IUserSessionService userSessionService)
        {
            _salesReportService = salesReportService;
            _branchRepository = branchRepository;
            _reportService = reportService;
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
                }
            }
        }

        public ICommand GenerateReportCommand { get; }
        public ICommand PrintReportCommand { get; }

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

                var trendSource = SelectedReportType.Key == SalesReportService.SalesSummaryKey
                    ? selectedReport
                    : await _salesReportService.GenerateReportAsync(SalesReportService.SalesSummaryKey, request);

                var paymentSource = SelectedReportType.Key == SalesReportService.PaymentModeWiseKey
                    ? selectedReport
                    : await _salesReportService.GenerateReportAsync(SalesReportService.PaymentModeWiseKey, request);

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
