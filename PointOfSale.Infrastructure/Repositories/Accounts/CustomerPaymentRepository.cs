using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Repositories.Accounts;
using PointOfSale.Core.Models.Accounts;

namespace PointOfSale.Infrastructure.Repositories.Accounts
{
    public class CustomerPaymentRepository : ICustomerPaymentRepository
    {
        private readonly DatabaseConnection _dbConnection;
        public CustomerPaymentRepository(DatabaseConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        #region Public Methods
        public async Task<IEnumerable<CustomerReceivable>> GetCustomerReceivableAsync(int customerId)
        {
            var receivables = new List<CustomerReceivable>();

            try
            {
                using (var connection = _dbConnection.GetConnection())
                using (var command = CreateCommand(connection, "[Accounts].[uspGetCustomerReceivable]"))
                {
                    command.Parameters.Add("@CustomerId", SqlDbType.Int).Value = customerId;

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            receivables.Add(MapCustomerReceivable(reader));
                        }
                    }
                }
                return receivables;
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occured while retrieving the customer receivables.", ex);
            }
        }

        public async Task<long> CreateAsync(CustomerPayment customerPayment)
        {
            try
            {
                using (var connection = _dbConnection.GetConnection())
                using (var command = CreateCommand(connection, "[Accounts].[uspInsertCustomerPayment]"))
                {

                    AddCustomerPaymentParameters(command, customerPayment);

                    // Tvp for Payment Lines
                    var dt = new DataTable();
                    dt.Columns.Add("CustomerReceivableId", typeof(long));
                    dt.Columns.Add("AmountApplied", typeof(decimal));

                    foreach (var line in customerPayment.PaymentLines)
                        dt.Rows.Add(line.CustomerReceivableId, line.AmountApplied);

                    var tvpParam = command.Parameters.Add("@Lines", SqlDbType.Structured);
                    tvpParam.TypeName = "[Accounts].[CustomerPaymentLineType]";
                    tvpParam.Value = dt;

                    var outputParam = command.Parameters.Add("@CustomerPaymentId", SqlDbType.BigInt);
                    outputParam.Direction = ParameterDirection.Output;

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();

                    return (long)outputParam.Value;
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occured while creating payment for CustomerId {customerPayment.CustomerId}.", ex);
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
        private void AddCustomerPaymentParameters(SqlCommand command, CustomerPayment payment)
        {
            command.Parameters.Add("@CustomerId", SqlDbType.Int).Value = payment.CustomerId;
            command.Parameters.Add("@PaymentDate", SqlDbType.DateTime).Value = payment.PaymentDate;
            command.Parameters.Add("@PaymentMethod", SqlDbType.NVarChar, 50).Value = payment.PaymentMethod ?? (object)DBNull.Value;
            command.Parameters.Add("@ReferenceNumber", SqlDbType.NVarChar, 50).Value = payment.ReferenceNumber ?? (object)DBNull.Value;
            command.Parameters.Add("@PaidAmount", SqlDbType.Decimal).Value = payment.PaidAmount;

            // Move these ABOVE @CreatedBy to match the Stored Procedure order
            command.Parameters.AddWithValue("@BankName",
                string.IsNullOrWhiteSpace(payment.BankName) ? (object)DBNull.Value : payment.BankName);

            command.Parameters.AddWithValue("@AccountNumber",
                string.IsNullOrWhiteSpace(payment.AccountNumber) ? (object)DBNull.Value : payment.AccountNumber);

            command.Parameters.AddWithValue("@AccountName",
                string.IsNullOrWhiteSpace(payment.AccountName) ? (object)DBNull.Value : payment.AccountName);

            // Now add @CreatedBy
            command.Parameters.Add("@CreatedBy", SqlDbType.Int).Value = payment.CreatedBy;
        }
        private CustomerReceivable MapCustomerReceivable(IDataRecord record)
        {
            return new CustomerReceivable
            {
                CustomerReceivableId = record["CustomerReceivableId"] as long? ?? 0,
                CustomerId = record["CustomerId"] as int? ?? 0,
                InvoiceNumber = record["InvoiceNumber"]?.ToString(),
                ReferenceId = record["ReferenceId"] as long? ?? 0,
                TransactionDate = record["TransactionDate"] as DateTime? ?? DateTime.MinValue,
                DueDate = record["DueDate"] as DateTime? ?? DateTime.MinValue,
                InitialAmount = record["InitialAmount"] as decimal? ?? 0m,
                AmountSettled = record["AmountSettled"] as decimal? ?? 0m,
                BalanceAmount = record["BalanceAmount"] as decimal? ?? 0m,
                Status = record["Status"]?.ToString() ?? "Unknown"
            };
        }
        #endregion
    }
}
