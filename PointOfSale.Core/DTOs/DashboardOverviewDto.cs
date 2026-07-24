using System;
using System.Collections.Generic;

namespace PointOfSale.Core.DTOs
{
    public class DashboardOverviewDto
    {
        public decimal TotalSales { get; set; }
        public int OrderCount { get; set; }
        public decimal AverageOrderValue { get; set; }
        public decimal TotalDiscount { get; set; }
        public decimal DiscountRatePercent { get; set; }
        public double? AverageTableTurnMinutes { get; set; }

        public decimal CashSales { get; set; }
        public decimal CardSales { get; set; }
        public decimal CreditSales { get; set; }
        public decimal OtherSales { get; set; }
        public decimal TodaySales { get; set; }
        public int TodayOrderCount { get; set; }

        public int LowStockItemCount { get; set; }
        public int OutOfStockItemCount { get; set; }
        public int Expiring7DaysCount { get; set; }
        public int ExpiredItemCount { get; set; }

        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }

        public List<DashboardDailySalesPointDto> DailySales { get; } = new List<DashboardDailySalesPointDto>();
        public List<DashboardHourlySalesPointDto> HourlySalesToday { get; } = new List<DashboardHourlySalesPointDto>();
        public List<DashboardCategorySalesDto> CategorySales { get; } = new List<DashboardCategorySalesDto>();
        public List<DashboardTopItemDto> TopItems { get; } = new List<DashboardTopItemDto>();
        public List<DashboardAlertDto> Alerts { get; } = new List<DashboardAlertDto>();
    }

    public class DashboardDailySalesPointDto
    {
        public DateTime Day { get; set; }
        public decimal SalesAmount { get; set; }
        public int OrderCount { get; set; }
    }

    public class DashboardTopItemDto
    {
        public int ProductId { get; set; }
        public string Name { get; set; }
        public decimal Quantity { get; set; }
        public decimal Revenue { get; set; }
    }

    public class DashboardHourlySalesPointDto
    {
        public int HourOfDay { get; set; }
        public decimal SalesAmount { get; set; }
        public int OrderCount { get; set; }
    }

    public class DashboardCategorySalesDto
    {
        public string CategoryName { get; set; }
        public decimal SalesAmount { get; set; }
        public decimal Quantity { get; set; }
    }

    public class DashboardAlertDto
    {
        public string AlertType { get; set; }
        public string Severity { get; set; }
        public string ItemName { get; set; }
        public string Message { get; set; }
    }
}
