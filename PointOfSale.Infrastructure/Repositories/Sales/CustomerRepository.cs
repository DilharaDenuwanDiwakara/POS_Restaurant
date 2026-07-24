using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Repositories.Sales;
using PointOfSale.Core.Models.Sales;

namespace PointOfSale.Infrastructure.Repositories.Sales
{
    public class CustomerRepository : BaseRepository, ICustomerRepository
    {
        public CustomerRepository(DatabaseConnection dbConnection) : base(dbConnection) { }

        #region Public Method
        public async Task<int> CreateAsync(Customer customer)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Sales].[uspInsertCustomer]"))
                    {
                        AddCustomerParameters(command, customer);

                        var customerId = command.Parameters.Add("@CustomerId", SqlDbType.Int);
                        customerId.Direction = ParameterDirection.Output;

                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();

                        return (int)customerId.Value;
                    }
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2627 || ex.Number == 2601 || ex.Number == 50000) // Unique constraint error number
                {
                    throw new InvalidOperationException(ex.Message, ex);
                }

                throw new InvalidOperationException("A database error occured while creating the customer.", ex);
            }
        }
        public async Task<IEnumerable<Customer>> GetAllAsync()
        {
            var customers = new List<Customer>();

            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Sales].[uspGetAllCustomer]"))
                    {
                        await connection.OpenAsync();

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                customers.Add(MapCustomer(reader));
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occured while creating the customer.", ex);
            }
            return customers;
        }
        public async Task<Customer> GetByIdAsync(int id)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Sales].[uspGetCustomerById]"))
                    {
                        AddCustomerIdParameter((SqlCommand)command, id);

                        await connection.OpenAsync();

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                return MapCustomer(reader);
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occured while creating the customer.", ex);
            }
            return null;
        }

        public async Task<Customer> GetCustomerByPhoneAsync(string phone)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    var sql = @"
                        SELECT Id, Name, ContactNumber, IsTaxRegistered, TaxRegistrationNumber, Balance, LoyaltyPoints, IsActive 
                        FROM [Sales].[Customer] 
                        WHERE ContactNumber = @ContactNumber";

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandType = CommandType.Text;
                        command.CommandText = sql;
                        command.Parameters.Add("@ContactNumber", SqlDbType.NVarChar, 15).Value = (object)phone ?? DBNull.Value;

                        await connection.OpenAsync();
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                return MapCustomer(reader);
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                // Improved catch block to always show you the exact SQL error in the future!
                throw new InvalidOperationException($"A database error occurred while selecting customer by phone {phone}. Details: {ex.Message}", ex);
            }
            return null;
        }

        public async Task UpdateAsync(Customer customer)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Sales].[uspUpdateCustomer]"))
                    {
                        AddCustomerIdParameter(command, customer.Id);
                        AddCustomerParameters(command, customer);

                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occurred while updating customer with ID {customer.Id}.", ex);
            }
        }

        public async Task<int> GetLoyaltyPointsAsync(int customerId)
        {
            if (customerId <= 0)
                return 0;

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Sales].[uspGetCustomerLoyaltyPoints]"))
                {
                    command.Parameters.Add("@CustomerId", SqlDbType.Int).Value = customerId;
                    await connection.OpenAsync();

                    var result = await command.ExecuteScalarAsync();
                    return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
                }
            }
            catch (SqlException ex) when (ex.Number == 2812) // Proc missing
            {
                return 0;
            }
        }

        public async Task<int> AdjustLoyaltyPointsAsync(int customerId, int pointsDelta, string reason, long? salesId, int createdBy, string remarks = null)
        {
            using (var connection = GetConnection())
            using (var command = CreateCommand(connection, "[Sales].[uspAdjustCustomerLoyaltyPoints]"))
            {
                command.Parameters.Add("@CustomerId", SqlDbType.Int).Value = customerId;
                command.Parameters.Add("@PointsDelta", SqlDbType.Int).Value = pointsDelta;
                command.Parameters.Add("@Reason", SqlDbType.NVarChar, 50).Value = (object)reason ?? DBNull.Value;
                command.Parameters.Add("@SalesId", SqlDbType.BigInt).Value = (object)salesId ?? DBNull.Value;
                command.Parameters.Add("@CreatedBy", SqlDbType.Int).Value = createdBy;
                command.Parameters.Add("@Remarks", SqlDbType.NVarChar, 250).Value = (object)remarks ?? DBNull.Value;

                await connection.OpenAsync();
                var result = await command.ExecuteScalarAsync();
                return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
            }
        }
        #endregion

        #region Private Methods
        private void AddCustomerParameters(SqlCommand command, Customer customer)
        {
            var taxRegistrationNumber = customer.IsTaxRegistered
                ? customer.TaxRegistrationNumber
                : null;

            command.Parameters.Add("@CustomerName", SqlDbType.NVarChar, 100).Value = customer.CustomerName;
            command.Parameters.Add("@ContactNumber", SqlDbType.NVarChar, 15).Value = customer.ContactNumber;
            command.Parameters.Add("@IsTaxRegistered", SqlDbType.Bit).Value = customer.IsTaxRegistered;
            command.Parameters.Add("@TaxRegistrationNumber", SqlDbType.NVarChar, 50).Value =
                string.IsNullOrWhiteSpace(taxRegistrationNumber)
                    ? (object)DBNull.Value
                    : taxRegistrationNumber.Trim();
            command.Parameters.Add("@CreditLimit", SqlDbType.Decimal).Value = customer.CreditLimit;
            command.Parameters.Add("@IsActive", SqlDbType.Bit).Value = customer.IsActive;
            command.Parameters.Add("@CreatedBy", SqlDbType.Int).Value = customer.CreatedBy; // Assuming a system user for now
        }
        private void AddCustomerIdParameter(SqlCommand command, int customerId)
        {
            command.Parameters.Add("@CustomerId", SqlDbType.Int).Value = customerId;
        }
        private Customer MapCustomer(IDataRecord record)
        {
            return new Customer
            {
                Id = GetOptionalValue(record, "Id", GetOptionalValue(record, "CustomerId", 0)),
                CustomerName = GetOptionalValue<string>(record, "Name", GetOptionalValue<string>(record, "CustomerName", null)),
                ContactNumber = GetValue<string>(record, "ContactNumber"),
                IsTaxRegistered = GetOptionalValue(record, "IsTaxRegistered", false),
                TaxRegistrationNumber = GetOptionalValue<string>(record, "TaxRegistrationNumber", null),
                Balance = GetValue<decimal>(record, "Balance"),
                LoyaltyPoints = GetOptionalValue(record, "LoyaltyPoints", 0),
                IsActive = GetValue<bool>(record, "IsActive")
            };
        }

        private T GetOptionalValue<T>(IDataRecord record, string columnName, T defaultValue)
        {
            for (int i = 0; i < record.FieldCount; i++)
            {
                if (string.Equals(record.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
                    return GetValue<T>(record, columnName);
            }

            return defaultValue;
        }
        #endregion
    }
}
