using System;
using System.Collections.Generic;

namespace PointOfSale.Core.DTOs
{
    public class RestaurantDashboardDto
    {
        public RestaurantDashboardDto()
        {
            Summary = new RestaurantDashboardSummaryDto();
            RepeatCustomers = new List<RepeatCustomerDetailDto>();
            StewardPerformance = new List<StewardPerformanceDto>();
        }

        public RestaurantDashboardSummaryDto Summary { get; set; }
        public List<RepeatCustomerDetailDto> RepeatCustomers { get; set; }
        public List<StewardPerformanceDto> StewardPerformance { get; set; }
    }

    public class RestaurantDashboardSummaryDto
    {
        public int TotalGuestsServed { get; set; }
        public int TotalTableVisits { get; set; }
        public int RepeatCustomerCount { get; set; }
        public string HighestRepeatCustomerName { get; set; }
        public int HighestRepeatVisitCount { get; set; }
        public string TopPerformingStewardName { get; set; }
        public int TopPerformingStewardServingCount { get; set; }
        public decimal TotalNetSales { get; set; }
        public decimal AveragePerHeadValue { get; set; }
    }

    public class RepeatCustomerDetailDto
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string ContactNumber { get; set; }
        public int VisitCount { get; set; }
        public int TotalGuests { get; set; }
        public DateTime FirstVisit { get; set; }
        public DateTime LastVisit { get; set; }
    }

    public class StewardPerformanceDto
    {
        public int StewardId { get; set; }
        public string StewardName { get; set; }
        public int ServingCount { get; set; }
        public int TotalGuestsServed { get; set; }
        public decimal NetSales { get; set; }
        public decimal AverageSalesPerGuest { get; set; }
    }
}
