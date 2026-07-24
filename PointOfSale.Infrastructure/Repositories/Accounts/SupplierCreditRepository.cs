using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Repositories.Accounts;
using PointOfSale.Core.Models.Accounts.DTOs;

namespace PointOfSale.Infrastructure.Repositories.Accounts
{
    public class SupplierCreditRepository : ISupplierCreditRepository
    {
        private readonly DatabaseConnection _dbConnection;

        public SupplierCreditRepository(DatabaseConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        #region Public Methods
        public async Task<IEnumerable<SupplierCredit>> GetAvailableCreditsAsync(int supplierId)
        {
            var credits = new List<SupplierCredit>();

            try
            {
                using (var connection = _dbConnection.GetConnection())
                using (var command = CreateCommand(connection, "[Accounts].[uspGetSupplierAvailableCredits]"))
                {
                    command.Parameters.Add("@SupplierId", SqlDbType.Int).Value = supplierId;

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            credits.Add(MapSupplierCredit(reader));
                        }
                    }
                }
                return credits;
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while retrieving supplier credits.", ex);
            }
        }
        #endregion

        #region Private Helpers
        private SqlCommand CreateCommand(SqlConnection connection, string storedProcedure)
        {
            var command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = storedProcedure;
            return command;
        }
        private SupplierCredit MapSupplierCredit(IDataRecord record)
        {
            return new SupplierCredit
            {
                SourceType = record["SourceType"] as string ?? "Unknown",
                SourceId = record["SourceId"] as long? ?? 0,
                ReferenceNumber = record["ReferenceNo"] as string,
                CreditDate = record["Date"] as DateTime? ?? DateTime.MinValue,
                CreditAmount = record["TotalAmount"] as decimal? ?? 0m,
                AppliedAmount = (record["TotalAmount"] as decimal? ?? 0m) - (record["RemainingAmount"] as decimal? ?? 0m),
                RemainingAmount = record["RemainingAmount"] as decimal? ?? 0m,
                SelectedAmount = 0m
            };
        }
        #endregion
    }
}
