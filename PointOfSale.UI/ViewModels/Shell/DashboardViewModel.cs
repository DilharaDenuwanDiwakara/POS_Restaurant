using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using PointOfSale.Core.Interfaces.Repositories.Sales;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Shell
{
    public class DashboardViewModel : BaseViewModel
    {
        private readonly IDashboardRepository _dashboardRepository;
        private readonly IUserSessionService _userSessionService;

        public DashboardViewModel(IDashboardRepository dashboardRepository, IUserSessionService userSessionService)
        {
            _dashboardRepository = dashboardRepository;
            _userSessionService = userSessionService;

            PeriodOptions = new ObservableCollection<string>
            {
                "Today",
                "Last 7 Days",
                "Last 30 Days",
                "This Month"
            };
            _selectedPeriod = "This Month";

            TopItems = new ObservableCollection<TopItemDto>();
            WeeklySales = new ObservableCollection<ChartBar>();
            HourlySales = new ObservableCollection<ChartBar>();
            CategorySales = new ObservableCollection<CategoryChartDto>();
            PaymentBreakdown = new ObservableCollection<PaymentMetricDto>();
            Alerts = new ObservableCollection<DashboardAlertUiDto>();

            RefreshCommand = new AsyncRelayCommand(LoadDashboardAsync);
            _ = LoadDashboardAsync(null);
        }

        public ObservableCollection<string> PeriodOptions { get; }
        public ObservableCollection<TopItemDto> TopItems { get; }
        public ObservableCollection<ChartBar> WeeklySales { get; }
        public ObservableCollection<ChartBar> HourlySales { get; }
        public ObservableCollection<CategoryChartDto> CategorySales { get; }
        public ObservableCollection<PaymentMetricDto> PaymentBreakdown { get; }
        public ObservableCollection<DashboardAlertUiDto> Alerts { get; }

        private string _selectedPeriod;
        public string SelectedPeriod
        {
            get => _selectedPeriod;
            set
            {
                if (SetProperty(ref _selectedPeriod, value))
                    _ = LoadDashboardAsync(null);
            }
        }

        private string _dateRangeText = string.Empty;
        public string DateRangeText
        {
            get => _dateRangeText;
            set => SetProperty(ref _dateRangeText, value);
        }

        private decimal _totalSales;
        public decimal TotalSales
        {
            get => _totalSales;
            set => SetProperty(ref _totalSales, value);
        }

        private int _orderCount;
        public int OrderCount
        {
            get => _orderCount;
            set => SetProperty(ref _orderCount, value);
        }

        private decimal _averageOrderValue;
        public decimal AverageOrderValue
        {
            get => _averageOrderValue;
            set => SetProperty(ref _averageOrderValue, value);
        }

        private decimal _discountRatePercent;
        public decimal DiscountRatePercent
        {
            get => _discountRatePercent;
            set => SetProperty(ref _discountRatePercent, value);
        }

        private string _avgTurnTime = "N/A";
        public string AvgTurnTime
        {
            get => _avgTurnTime;
            set => SetProperty(ref _avgTurnTime, value);
        }

        private decimal _todaySales;
        public decimal TodaySales
        {
            get => _todaySales;
            set => SetProperty(ref _todaySales, value);
        }

        private int _todayOrderCount;
        public int TodayOrderCount
        {
            get => _todayOrderCount;
            set => SetProperty(ref _todayOrderCount, value);
        }

        private int _lowStockItemCount;
        public int LowStockItemCount
        {
            get => _lowStockItemCount;
            set => SetProperty(ref _lowStockItemCount, value);
        }

        private int _outOfStockItemCount;
        public int OutOfStockItemCount
        {
            get => _outOfStockItemCount;
            set => SetProperty(ref _outOfStockItemCount, value);
        }

        private int _expiring7DaysCount;
        public int Expiring7DaysCount
        {
            get => _expiring7DaysCount;
            set => SetProperty(ref _expiring7DaysCount, value);
        }

        private int _expiredItemCount;
        public int ExpiredItemCount
        {
            get => _expiredItemCount;
            set => SetProperty(ref _expiredItemCount, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public ICommand RefreshCommand { get; }

        private async Task LoadDashboardAsync(object obj)
        {
            try
            {
                IsBusy = true;
                ErrorMessage = string.Empty;

                var (fromDate, toDate) = GetDateRange(SelectedPeriod);

                var rawBranchId = _userSessionService.BranchId;
                var branchId = rawBranchId > 0 ? (int?)rawBranchId : null;

                DateRangeText = $"Loading… BranchId={rawBranchId}, From {fromDate:dd MMM yyyy} to {toDate:dd MMM yyyy}";

                var dashboard = await _dashboardRepository.GetOverviewAsync(branchId, fromDate, toDate);

                TotalSales = dashboard.TotalSales;
                OrderCount = dashboard.OrderCount;
                AverageOrderValue = dashboard.AverageOrderValue;
                DiscountRatePercent = dashboard.DiscountRatePercent;
                AvgTurnTime = dashboard.AverageTableTurnMinutes.HasValue
                    ? $"{Math.Round(dashboard.AverageTableTurnMinutes.Value, 0):N0}m"
                    : "N/A";

                TodaySales = dashboard.TodaySales;
                TodayOrderCount = dashboard.TodayOrderCount;
                LowStockItemCount = dashboard.LowStockItemCount;
                OutOfStockItemCount = dashboard.OutOfStockItemCount;
                Expiring7DaysCount = dashboard.Expiring7DaysCount;
                ExpiredItemCount = dashboard.ExpiredItemCount;

                DateRangeText = $"From {fromDate:dd MMM yyyy} to {toDate:dd MMM yyyy}";

                BindWeeklySales(dashboard);
                BindHourlySales(dashboard);
                BindTopItems(dashboard);
                BindCategorySales(dashboard);
                BindPaymentBreakdown(dashboard);
                BindAlerts(dashboard);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Dashboard failed: {ex.Message}";
                System.Windows.MessageBox.Show(
                    $"Dashboard Load Error:\n\n{ex.Message}\n\n--- Stack Trace ---\n{ex.StackTrace}",
                    "Dashboard Diagnostic",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void BindWeeklySales(Core.DTOs.DashboardOverviewDto dashboard)
        {
            WeeklySales.Clear();

            var maxAmount = dashboard.DailySales.Any() ? dashboard.DailySales.Max(x => x.SalesAmount) : 0m;
            var maxAmountSafe = maxAmount <= 0 ? 1 : maxAmount;

            foreach (var point in dashboard.DailySales)
            {
                var ratio = (double)(point.SalesAmount / maxAmountSafe) * 100d;
                var color = ratio >= 85 ? "#2E7D32" : ratio >= 60 ? "#4CAF50" : "#A5D6A7";

                WeeklySales.Add(new ChartBar
                {
                    Label = point.Day.ToString("ddd"),
                    Value = ratio,
                    Color = color,
                    ToolTipText = $"LKR {point.SalesAmount:N0} ({point.OrderCount} orders)"
                });
            }
        }

        private void BindHourlySales(Core.DTOs.DashboardOverviewDto dashboard)
        {
            HourlySales.Clear();

            var activeHours = dashboard.HourlySalesToday.Where(x => x.SalesAmount > 0).ToList();
            var maxAmount = activeHours.Any() ? activeHours.Max(x => x.SalesAmount) : 0m;
            var maxAmountSafe = maxAmount <= 0 ? 1 : maxAmount;

            foreach (var point in dashboard.HourlySalesToday.Where(x => x.HourOfDay >= 8 && x.HourOfDay <= 23))
            {
                var ratio = (double)(point.SalesAmount / maxAmountSafe) * 100d;
                HourlySales.Add(new ChartBar
                {
                    Label = point.HourOfDay.ToString("00"),
                    Value = ratio,
                    Color = "#1565C0",
                    ToolTipText = $"{point.HourOfDay:00}:00 - LKR {point.SalesAmount:N0} ({point.OrderCount} orders)"
                });
            }
        }

        private void BindTopItems(Core.DTOs.DashboardOverviewDto dashboard)
        {
            TopItems.Clear();

            var maxQty = dashboard.TopItems.Any() ? dashboard.TopItems.Max(x => x.Quantity) : 0m;
            var maxQtySafe = maxQty <= 0 ? 1 : maxQty;

            foreach (var item in dashboard.TopItems)
            {
                TopItems.Add(new TopItemDto
                {
                    Name = item.Name,
                    Qty = item.Quantity,
                    Revenue = item.Revenue,
                    Percentage = (int)Math.Round((item.Quantity / maxQtySafe) * 100, 0)
                });
            }
        }

        private void BindCategorySales(Core.DTOs.DashboardOverviewDto dashboard)
        {
            CategorySales.Clear();

            var maxAmount = dashboard.CategorySales.Any() ? dashboard.CategorySales.Max(x => x.SalesAmount) : 0m;
            var maxAmountSafe = maxAmount <= 0 ? 1 : maxAmount;

            foreach (var category in dashboard.CategorySales)
            {
                CategorySales.Add(new CategoryChartDto
                {
                    CategoryName = category.CategoryName,
                    SalesAmount = category.SalesAmount,
                    Quantity = category.Quantity,
                    Percentage = (int)Math.Round((category.SalesAmount / maxAmountSafe) * 100, 0)
                });
            }
        }

        private void BindPaymentBreakdown(Core.DTOs.DashboardOverviewDto dashboard)
        {
            PaymentBreakdown.Clear();

            var total = dashboard.CashSales + dashboard.CardSales + dashboard.CreditSales + dashboard.OtherSales;
            var divisor = total <= 0 ? 1 : total;

            PaymentBreakdown.Add(new PaymentMetricDto
            {
                Method = "Cash",
                Amount = dashboard.CashSales,
                Percentage = (int)Math.Round((dashboard.CashSales / divisor) * 100, 0),
                Color = "#2E7D32"
            });
            PaymentBreakdown.Add(new PaymentMetricDto
            {
                Method = "Card",
                Amount = dashboard.CardSales,
                Percentage = (int)Math.Round((dashboard.CardSales / divisor) * 100, 0),
                Color = "#1565C0"
            });
            PaymentBreakdown.Add(new PaymentMetricDto
            {
                Method = "Credit",
                Amount = dashboard.CreditSales,
                Percentage = (int)Math.Round((dashboard.CreditSales / divisor) * 100, 0),
                Color = "#EF6C00"
            });

            if (dashboard.OtherSales > 0)
            {
                PaymentBreakdown.Add(new PaymentMetricDto
                {
                    Method = "Other",
                    Amount = dashboard.OtherSales,
                    Percentage = (int)Math.Round((dashboard.OtherSales / divisor) * 100, 0),
                    Color = "#6A1B9A"
                });
            }
        }

        private void BindAlerts(Core.DTOs.DashboardOverviewDto dashboard)
        {
            Alerts.Clear();

            foreach (var alert in dashboard.Alerts)
            {
                Alerts.Add(new DashboardAlertUiDto
                {
                    AlertType = alert.AlertType,
                    Severity = alert.Severity,
                    ItemName = alert.ItemName,
                    Message = alert.Message,
                    SeverityColor = alert.Severity == "HIGH" ? "#C62828" : alert.Severity == "MEDIUM" ? "#EF6C00" : "#1565C0"
                });
            }
        }

        private (DateTime fromDate, DateTime toDate) GetDateRange(string period)
        {
            var today = DateTime.Today;

            switch (period)
            {
                case "Today":
                    return (today, today);
                case "Last 30 Days":
                    return (today.AddDays(-29), today);
                case "This Month":
                    return (new DateTime(today.Year, today.Month, 1), today);
                case "Last 7 Days":
                default:
                    return (today.AddDays(-6), today);
            }
        }
    }

    public class TopItemDto
    {
        public string Name { get; set; }
        public decimal Qty { get; set; }
        public decimal Revenue { get; set; }
        public int Percentage { get; set; }
    }

    public class ChartBar
    {
        public string Label { get; set; }
        public double Value { get; set; }
        public string Color { get; set; }
        public string ToolTipText { get; set; }
    }

    public class PaymentMetricDto
    {
        public string Method { get; set; }
        public decimal Amount { get; set; }
        public int Percentage { get; set; }
        public string Color { get; set; }
    }

    public class CategoryChartDto
    {
        public string CategoryName { get; set; }
        public decimal SalesAmount { get; set; }
        public decimal Quantity { get; set; }
        public int Percentage { get; set; }
    }

    public class DashboardAlertUiDto
    {
        public string AlertType { get; set; }
        public string Severity { get; set; }
        public string ItemName { get; set; }
        public string Message { get; set; }
        public string SeverityColor { get; set; }
    }
}
