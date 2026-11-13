using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Restaurant;

namespace PointOfSale.Infrastructure.Repositories.Restaurant
{
    public class RestaurantReportRepository : BaseRepository, IRestaurantReportRepository
    {
        public RestaurantReportRepository(DatabaseConnection databaseConnection) : base(databaseConnection)
        {
        }

        public async Task<RestaurantDashboardDto> GetDashboardAsync(int? branchId, DateTime startDate, DateTime endDate)
        {
            if (endDate.Date < startDate.Date)
                throw new ArgumentException("End date cannot be earlier than start date.", nameof(endDate));

            try
            {
                var summaryTask = GetSummaryAsync(branchId, startDate, endDate);
                var repeatCustomersTask = GetRepeatCustomersAsync(branchId, startDate, endDate);
                var stewardPerformanceTask = GetStewardPerformanceAsync(branchId, startDate, endDate);

                await Task.WhenAll(summaryTask, repeatCustomersTask, stewardPerformanceTask);

                return new RestaurantDashboardDto
                {
                    Summary = await summaryTask,
                    RepeatCustomers = await repeatCustomersTask,
                    StewardPerformance = await stewardPerformanceTask
                };
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"Failed to generate Restaurant Reports dashboard. SQL error: {ex.Message}", ex);
            }
        }

        private async Task<RestaurantDashboardSummaryDto> GetSummaryAsync(int? branchId, DateTime startDate, DateTime endDate)
        {
            using (var connection = GetConnection())
            using (var command = CreateCommand(connection, "[Restaurant].[uspGetRestaurantDashboardSummary]"))
            {
                AddReportParameters(command, branchId, startDate, endDate);
                await connection.OpenAsync();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync())
                        return new RestaurantDashboardSummaryDto();

                    return new RestaurantDashboardSummaryDto
                    {
                        TotalGuestsServed = GetValue<int>(reader, "TotalGuestsServed"),
                        TotalTableVisits = GetValue<int>(reader, "TotalTableVisits"),
                        RepeatCustomerCount = GetValue<int>(reader, "RepeatCustomerCount"),
                        HighestRepeatCustomerName = GetValue<string>(reader, "HighestRepeatCustomerName"),
                        HighestRepeatVisitCount = GetValue<int>(reader, "HighestRepeatVisitCount"),
                        TopPerformingStewardName = GetValue<string>(reader, "TopPerformingStewardName"),
                        TopPerformingStewardServingCount = GetValue<int>(reader, "TopPerformingStewardServingCount"),
                        TotalNetSales = GetValue<decimal>(reader, "TotalNetSales"),
                        AveragePerHeadValue = GetValue<decimal>(reader, "AveragePerHeadValue")
                    };
                }
            }
        }

        private async Task<List<RepeatCustomerDetailDto>> GetRepeatCustomersAsync(int? branchId, DateTime startDate, DateTime endDate)
        {
            var results = new List<RepeatCustomerDetailDto>();

            using (var connection = GetConnection())
            using (var command = CreateCommand(connection, "[Restaurant].[uspGetRepeatCustomerDetails]"))
            {
                AddReportParameters(command, branchId, startDate, endDate);
                await connection.OpenAsync();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        results.Add(new RepeatCustomerDetailDto
                        {
                            CustomerId = GetValue<int>(reader, "CustomerId"),
                            CustomerName = GetValue<string>(reader, "CustomerName"),
                            ContactNumber = GetValue<string>(reader, "ContactNumber"),
                            VisitCount = GetValue<int>(reader, "VisitCount"),
                            TotalGuests = GetValue<int>(reader, "TotalGuests"),
                            FirstVisit = GetValue<DateTime>(reader, "FirstVisit"),
                            LastVisit = GetValue<DateTime>(reader, "LastVisit")
                        });
                    }
                }
            }

            return results;
        }

        private async Task<List<StewardPerformanceDto>> GetStewardPerformanceAsync(int? branchId, DateTime startDate, DateTime endDate)
        {
            var results = new List<StewardPerformanceDto>();

            using (var connection = GetConnection())
            using (var command = CreateCommand(connection, "[Restaurant].[uspGetStewardPerformance]"))
            {
                AddReportParameters(command, branchId, startDate, endDate);
                await connection.OpenAsync();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        results.Add(new StewardPerformanceDto
                        {
                            StewardId = GetValue<int>(reader, "StewardId"),
                            StewardName = GetValue<string>(reader, "StewardName"),
                            ServingCount = GetValue<int>(reader, "ServingCount"),
                            TotalGuestsServed = GetValue<int>(reader, "TotalGuestsServed"),
                            NetSales = GetValue<decimal>(reader, "NetSales"),
                            AverageSalesPerGuest = GetValue<decimal>(reader, "AverageSalesPerGuest")
                        });
                    }
                }
            }

            return results;
        }

        private static void AddReportParameters(SqlCommand command, int? branchId, DateTime startDate, DateTime endDate)
        {
            command.CommandTimeout = 120;
            command.Parameters.Add("@BranchId", SqlDbType.Int).Value =
                branchId.HasValue && branchId.Value > 0 ? (object)branchId.Value : DBNull.Value;
            command.Parameters.Add("@StartDate", SqlDbType.Date).Value = startDate.Date;
            command.Parameters.Add("@EndDate", SqlDbType.Date).Value = endDate.Date;
        }
    }
}
