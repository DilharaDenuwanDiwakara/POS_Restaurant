using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
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
