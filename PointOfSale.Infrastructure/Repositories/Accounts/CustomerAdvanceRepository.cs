using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Repositories.Accounts;
using PointOfSale.Core.Models.Accounts;

namespace PointOfSale.Infrastructure.Repositories.Accounts
{
    public class CustomerAdvanceRepository : ICustomerAdvanceRepository
    {
        private readonly DatabaseConnection _dbConnection;
        public CustomerAdvanceRepository(DatabaseConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        #region Public Methods
        public async Task<long> CreateAsync(CustomerAdvance customerAdvance)
        {
            try
            {
                using (var connection = _dbConnection.GetConnection())
                using (var command = CreateCommand(connection, "[Accounts].[uspInsertCustomerAdvance]"))
                {

                    AddAdvanceParameters(command, customerAdvance);

                    var outputParam = command.Parameters.Add("@AdvanceId", SqlDbType.BigInt);
                    outputParam.Direction = ParameterDirection.Output;

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();

                    return (long)outputParam.Value;
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while creating the customer advance.", ex);
            }

        }
        public async Task<IEnumerable<CustomerAdvanceList>> GetUnappliedAdvanceAsync(int? customerId)
        {
            var advances = new List<CustomerAdvanceList>();

            try
            {
                using (var connection = _dbConnection.GetConnection())
                using (var command = CreateCommand(connection, "[Accounts].[uspGetCustomerUnappliedAdvance]"))
                {
                    command.Parameters.AddWithValue("@CustomerId", (object)customerId ?? DBNull.Value);

                    await connection.OpenAsync();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            advances.Add(MapAdvance(reader));
                        }
                    }
                }
                return advances;
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while retrieving unapplied advances.", ex);
            }
        }
        public async Task CancelAdvanceAsync(long advanceId, int cancelledByUserId)
        {
            using (var connection = _dbConnection.GetConnection())
            using (var command = CreateCommand(connection, "[Accounts].[uspCancelCustomerAdvance]"))
            {
                command.Parameters.Add("@AdvanceId", SqlDbType.BigInt).Value = advanceId;
                command.Parameters.Add("@CancelledBy", SqlDbType.Int).Value = cancelledByUserId;

                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();
            }
        }
        #endregion

        #region Private Helper Methods

        private SqlCommand CreateCommand(SqlConnection connection, string storedProcedure)
        {
            var command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = storedProcedure;
            return command;
        }

        private void AddAdvanceParameters(SqlCommand command, CustomerAdvance advance)
        {
            command.Parameters.Add("@CustomerId", SqlDbType.Int).Value = advance.CustomerId;
            command.Parameters.Add("@PaymentDate", SqlDbType.DateTime).Value = advance.PaymentDate;
            command.Parameters.Add("@PaymentMethod", SqlDbType.NVarChar, 50).Value = advance.PaymentMethod ?? (object)DBNull.Value;
            command.Parameters.Add("@ReferenceNumber", SqlDbType.NVarChar, 50).Value = string.IsNullOrWhiteSpace(advance.ReferenceNumber)
                ? (object)DBNull.Value
                : advance.ReferenceNumber;
            command.Parameters.Add("@Amount", SqlDbType.Decimal).Value = advance.Amount;
            command.Parameters.Add("@CreatedBy", SqlDbType.Int).Value = advance.CreatedBy;
        }

        private CustomerAdvanceList MapAdvance(IDataRecord record)
        {
            return new CustomerAdvanceList
            {
                AdvanceId = Convert.ToInt64(record["AdvanceId"]),
                CustomerName = record["CustomerName"] == DBNull.Value ? string.Empty : record["CustomerName"].ToString(),
                TransactionNumber = record["TransactionNumber"].ToString(),
                PaymentDate = record.GetDateTime(record.GetOrdinal("PaymentDate")),
                PaidAmount = Convert.ToDecimal(record["PaidAmount"]),
                UnappliedAmount = Convert.ToDecimal(record["UnappliedAmount"])
            };
        }

        #endregion
    }
}
