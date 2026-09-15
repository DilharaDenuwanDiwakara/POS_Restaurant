using PointOfSale.Core.Interfaces.Repositories.Sales;
using PointOfSale.Core.Models.Sales;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace PointOfSale.Infrastructure.Repositories.Sales
{
    public class SalesPersonRepository : BaseRepository, ISalesPersonRepository
    {
        public SalesPersonRepository(DatabaseConnection databaseConnection) : base(databaseConnection) { }

        #region Public Methods

        public async Task<int> CreateAsync(SalesPerson salesPerson)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Sales].[uspInsertSalesPerson]"))
                    {
                        AddSalesPersonParameters(command, salesPerson);
                        command.Parameters.Add("@CreateBy", SqlDbType.Int).Value = salesPerson.CreateBy;

                        var idParam = command.Parameters.Add("@Id", SqlDbType.Int);
                        idParam.Direction = ParameterDirection.Output;

                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();

                        if (idParam.Value == null || idParam.Value == DBNull.Value)
                            throw new InvalidOperationException("The insert procedure did not return the sales person ID.");

                        return Convert.ToInt32(idParam.Value);
                    }
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2627 || ex.Number == 2601 || ex.Number == 50000)
                {
                    throw new InvalidOperationException(ex.Message, ex);
                }
                throw new InvalidOperationException("A database error occurred while creating the sales person.", ex);
            }
        }

        public async Task<IEnumerable<SalesPerson>> GetAllAsync()
        {
            var salesPersons = new List<SalesPerson>();

            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Sales].[uspGetAllSalesPerson]"))
                    {
                        await connection.OpenAsync();

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                salesPersons.Add(MapSalesPerson(reader));
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while retrieving sales persons.", ex);
            }

            return salesPersons;
        }

        public async Task UpdateAsync(SalesPerson salesPerson)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Sales].[uspUpdateSalesPerson]"))
                    {
                        command.Parameters.Add("@Id", SqlDbType.Int).Value = salesPerson.Id;
                        command.Parameters.Add("@UpdateBy", SqlDbType.Int).Value = salesPerson.UpdateBy ?? salesPerson.CreateBy;

                        AddSalesPersonParameters(command, salesPerson);

                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2627 || ex.Number == 2601 || ex.Number == 50000)
                {
                    throw new InvalidOperationException(ex.Message, ex);
                }
                throw new InvalidOperationException($"A database error occurred while updating sales person with ID {salesPerson.Id}.", ex);
            }
        }

        #endregion

        #region Private Methods

        private void AddSalesPersonParameters(SqlCommand command, SalesPerson salesPerson)
        {
            command.Parameters.Add("@Name", SqlDbType.NVarChar, 100).Value = salesPerson.Name?.Trim();
            command.Parameters.Add("@Email", SqlDbType.NVarChar, 150).Value = string.IsNullOrWhiteSpace(salesPerson.Email) ? (object)DBNull.Value : salesPerson.Email.Trim();
            command.Parameters.Add("@NIC", SqlDbType.NVarChar, 20).Value = string.IsNullOrWhiteSpace(salesPerson.NIC) ? (object)DBNull.Value : salesPerson.NIC.Trim();
            command.Parameters.Add("@ContactNo", SqlDbType.NVarChar, 15).Value = string.IsNullOrWhiteSpace(salesPerson.ContactNo) ? (object)DBNull.Value : salesPerson.ContactNo.Trim();
            command.Parameters.Add("@Address", SqlDbType.NVarChar, 255).Value = string.IsNullOrWhiteSpace(salesPerson.Address) ? (object)DBNull.Value : salesPerson.Address.Trim();
            command.Parameters.Add("@IsActive", SqlDbType.Bit).Value = salesPerson.IsActive;
        }

        private SalesPerson MapSalesPerson(IDataRecord record)
        {
            return new SalesPerson
            {
                Id = GetValue<int>(record, "Id"),
                Code = GetOptionalValue<string>(record, "Code", null),
                Name = GetValue<string>(record, "Name"),
                Email = GetOptionalValue<string>(record, "Email", null),
                NIC = GetValue<string>(record, "NIC"),
                ContactNo = GetValue<string>(record, "ContactNo"),
                Address = GetOptionalValue<string>(record, "Address", null),
                IsActive = GetValue<bool>(record, "IsActive"),
                CreateBy = GetValue<int>(record, "CreateBy"),
                CreateAt = GetValue<DateTime>(record, "CreateAt"),
                UpdateBy = GetOptionalValue<int?>(record, "UpdateBy", null),
                UpdateAt = GetOptionalValue<DateTime?>(record, "UpdateAt", null)
            };
        }

        private T GetOptionalValue<T>(IDataRecord record, string columnName, T defaultValue)
        {
            for (int i = 0; i < record.FieldCount; i++)
            {
                if (string.Equals(record.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
                    return record.IsDBNull(i) ? defaultValue : (T)record.GetValue(i);
            }
            return defaultValue;
        }

        #endregion
    }
}
