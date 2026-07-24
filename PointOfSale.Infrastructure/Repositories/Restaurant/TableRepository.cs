using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Repositories.Restaurant;
using PointOfSale.Core.Models.Restaurant;

namespace PointOfSale.Infrastructure.Repositories.Restaurant
{
    public class TableRepository : BaseRepository, ITableRepository
    {
        public TableRepository(DatabaseConnection dbConnection) : base(dbConnection) { }

        #region public Method
        public async Task<int> CreateAsync(Table table)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Restaurant].[uspInsertTable]"))
                    {
                        AddTableParameters(command, table);

                        var id = command.Parameters.Add("@Id", SqlDbType.Int);
                        id.Direction = ParameterDirection.Output;

                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();

                        return (int)id.Value;
                    }
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2627 || ex.Number == 2601 || ex.Number == 50000) // Unique constraint error number
                {
                    throw new InvalidOperationException(ex.Message, ex);
                }

                throw new InvalidOperationException("A database error occured while creating the table.", ex);
            }
        }
        public async Task<IEnumerable<Table>> GetAllAsync(int branchId)
        {
            var tables = new List<Table>();
            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Restaurant].[uspGetAllTables]"))
                    {
                        command.Parameters.Add("@BranchId", SqlDbType.Int).Value = branchId;

                        await connection.OpenAsync();

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                tables.Add(MapTable(reader));
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occured while selecting the tables.", ex);
            }
            return tables;
        }
        public async Task UpdateAsync(Table table)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Restaurant].[uspUpdateTable]"))
                    {
                        command.Parameters.Add("@Id", SqlDbType.Int).Value = table.Id;
                        AddTableParameters(command, table);

                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occurred while updating category with ID {table.Id}.", ex);
            }
        }
        #endregion

        #region Private Method
        private void AddTableParameters(SqlCommand command, Table table)
        {
            command.Parameters.Add("@BranchId", SqlDbType.Int).Value = table.BranchId;
            command.Parameters.Add("@Name", SqlDbType.NVarChar, 50).Value = table.Name;
            command.Parameters.Add("@Capacity", SqlDbType.Int).Value = table.Capacity;
        }
        private Table MapTable(IDataRecord record)
        {
            return new Table
            {
                Id = GetValue<int>(record, "Id"),
                BranchId = GetValue<int>(record, "BranchId"),
                Name = GetValue<string>(record, "TableName"),
                Capacity = GetValue<int>(record, "Capacity"),
                CurrentStatus = GetValue<string>(record, "CurrentStatus")
            };
        }
        #endregion
    }
}
