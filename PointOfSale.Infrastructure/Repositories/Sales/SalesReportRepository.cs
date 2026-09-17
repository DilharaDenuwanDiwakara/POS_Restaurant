using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Sales;

namespace PointOfSale.Infrastructure.Repositories.Sales
{
    public class SalesReportRepository : BaseRepository, ISalesReportRepository
    {
        public SalesReportRepository(DatabaseConnection databaseConnection) : base(databaseConnection)
        {
        }

        public Task<DataTable> GetSalesSummaryReportAsync(int userId, int? branchId, DateTime startDate, DateTime endDate)
        {
            return ExecuteSalesReportAsync("[Sales].[uspGetSalesSummaryReport]", userId, branchId, startDate, endDate,
                "Failed to generate Sales Summary report.");
        }

        public Task<DataTable> GetSalesDetailReportAsync(int userId, int? branchId, DateTime startDate, DateTime endDate)
        {
            return ExecuteSalesReportAsync("[Sales].[uspGetSalesDetailReport]", userId, branchId, startDate, endDate,
                "Failed to generate Sales Detail report.");
        }

        public Task<DataTable> GetPaymentModeWiseSalesReportAsync(int userId, int? branchId, DateTime startDate, DateTime endDate)
        {
            return ExecuteSalesReportAsync("[Sales].[uspGetPaymentModeWiseSalesReport]", userId, branchId, startDate, endDate,
                "Failed to generate Payment Mode Wise sales report.");
        }

        public async Task<IEnumerable<SalesReturnReportDto>> GetSalesReturnDetailReportAsync(int? branchId, DateTime startDate, DateTime endDate)
        {
            var rows = new List<SalesReturnReportDto>();

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Sales].[uspGetSalesReturnReport]"))
                {
                    command.CommandTimeout = 120;
                    command.Parameters.Add("@StartDate", SqlDbType.DateTime).Value = startDate.Date;
                    command.Parameters.Add("@EndDate", SqlDbType.DateTime).Value = endDate.Date.AddDays(1).AddTicks(-1);
                    command.Parameters.Add("@BranchId", SqlDbType.Int).Value =
                        branchId.HasValue && branchId.Value > 0 ? (object)branchId.Value : DBNull.Value;

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            rows.Add(new SalesReturnReportDto
                            {
                                ReportTitle = GetValue<string>(reader, "ReportTitle"),
                                ReportFromDate = GetValue<string>(reader, "ReportFromDate"),
                                ReportToDate = GetValue<string>(reader, "ReportToDate"),
                                CompanyName = GetValue<string>(reader, "CompanyName"),
                                CompanyAddress = GetValue<string>(reader, "CompanyAddress"),
                                CompanyContact = GetValue<string>(reader, "CompanyContact"),
                                ReturnDate = GetValue<DateTime>(reader, "ReturnDate"),
                                ReturnNumber = GetValue<string>(reader, "ReturnNumber"),
                                InvoiceNumber = GetValue<string>(reader, "InvoiceNumber"),
                                ProductName = GetValue<string>(reader, "ProductName"),
                                ReturnQuantity = GetValue<decimal>(reader, "ReturnQuantity"),
                                Reason = GetValue<string>(reader, "Reason"),
                                IsWastage = GetValue<bool>(reader, "IsWastage"),
                                LineRefundAmount = GetValue<decimal>(reader, "LineRefundAmount"),
                                HeaderTotalRefund = GetValue<decimal>(reader, "HeaderTotalRefund")
                            });
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"Failed to generate Sales Return Detail report. SQL error: {ex.Message}", ex);
            }

            return rows;
        }

        private async Task<DataTable> ExecuteSalesReportAsync(
            string storedProcedure,
            int userId,
            int? branchId,
            DateTime startDate,
            DateTime endDate,
            string errorMessage)
        {
            var dataTable = new DataTable();

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, storedProcedure))
                {
                    command.CommandTimeout = 120;
                    command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                    command.Parameters.Add("@BranchId", SqlDbType.Int).Value =
                        branchId.HasValue && branchId.Value > 0 ? (object)branchId.Value : DBNull.Value;
                    command.Parameters.Add("@StartDate", SqlDbType.DateTime2).Value = startDate.Date;
                    command.Parameters.Add("@EndDate", SqlDbType.DateTime2).Value = endDate.Date.AddDays(1).AddTicks(-1);

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        dataTable.Load(reader);
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException(errorMessage, ex);
            }

            return dataTable;
        }
    }
}
