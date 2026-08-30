using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Repositories.Accounts;
using PointOfSale.Core.Models.Accounts;

namespace PointOfSale.Infrastructure.Repositories.Accounts
{
    public class SupplierAdvanceRepository : ISupplierAdvanceRepository
    {
        private readonly DatabaseConnection _dbConnection;
        public SupplierAdvanceRepository(DatabaseConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        #region Public Methods
        public async Task<long> CreateAsync(SupplierAdvance advance)
        {
            try
            {
                using (var connection = _dbConnection.GetConnection())
                using (var command = CreateCommand(connection, "[Accounts].[uspInsertSupplierAdvance]"))
                {
                    AddAdvanceParameters(command, advance);

                    var outputParam = command.Parameters.Add("@AdvanceId", SqlDbType.BigInt);
                    outputParam.Direction = ParameterDirection.Output;

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();

                    return (long)outputParam.Value;
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while creating supplier advance.", ex);
            }
        }
        public async Task<IEnumerable<SupplierAdvanceList>> GetUnappliedAdvanceAsync(int? supplierId)
        {
            var advances = new List<SupplierAdvanceList>();

            try
            {
                using (var connection = _dbConnection.GetConnection())
                using (var command = CreateCommand(connection, "[Accounts].[uspGetSupplierUnappliedAdvance]"))
                {
                    command.Parameters.AddWithValue("@SupplierId", (object)supplierId ?? DBNull.Value);

                    await connection.OpenAsync();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            advances.Add(MapAdvance(reader));
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while retrieving unapplied supplier advances.", ex);
            }

            return advances;
        }
        public async Task CancelAdvanceAsync(long advanceId, int cancelledByUserId)
        {
            try
            {
                using (var connection = _dbConnection.GetConnection())
                using (var command = CreateCommand(connection, "[Accounts].[uspCancelSupplierAdvance]"))
                {
                    command.Parameters.Add("@AdvanceId", SqlDbType.BigInt).Value = advanceId;
                    command.Parameters.Add("@CancelledBy", SqlDbType.Int).Value = cancelledByUserId;

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occurred while cancelling supplier advance with ID {advanceId}.", ex);
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
        private void AddAdvanceParameters(SqlCommand command, SupplierAdvance advance)
        {
            command.Parameters.Add("@BranchId", SqlDbType.Int).Value = advance.BranchId;
            command.Parameters.Add("@SupplierId", SqlDbType.Int).Value = advance.SupplierId;
            command.Parameters.Add("@PaymentDate", SqlDbType.DateTime).Value = advance.PaymentDate;
            command.Parameters.Add("@PaymentMethod", SqlDbType.NVarChar, 50).Value = advance.PaymentMethod ?? (object)DBNull.Value;
            command.Parameters.Add("@ReferenceNumber", SqlDbType.NVarChar, 50).Value = string.IsNullOrWhiteSpace(advance.ReferenceNumber)
                ? (object)DBNull.Value
                : advance.ReferenceNumber;
            command.Parameters.Add("@PaymentAccountId", SqlDbType.Int).Value = advance.PaymentAccountId;
            command.Parameters.Add("@Amount", SqlDbType.Decimal).Value = advance.Amount;
            command.Parameters.Add("@CreatedBy", SqlDbType.Int).Value = advance.CreatedBy;
        }
        private SupplierAdvanceList MapAdvance(IDataRecord record)
        {
            return new SupplierAdvanceList
            {
                AdvanceId = Convert.ToInt64(record["AdvanceId"]),
                SupplierName = record["SupplierName"] == DBNull.Value ? string.Empty : record["SupplierName"].ToString(),
                TransactionNumber = record["TransactionNumber"].ToString(),
                PaymentDate = record.GetDateTime(record.GetOrdinal("PaymentDate")),
                PaidAmount = Convert.ToDecimal(record["PaidAmount"]),
                UnappliedAmount = Convert.ToDecimal(record["UnappliedAmount"])
            };
        }
        #endregion
    }
}
