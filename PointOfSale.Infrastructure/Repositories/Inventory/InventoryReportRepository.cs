using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Inventory;

namespace PointOfSale.Infrastructure.Repositories.Inventory
{
    public class InventoryReportRepository : BaseRepository, IInventoryReportRepository
    {
        public InventoryReportRepository(DatabaseConnection databaseConnection) : base(databaseConnection)
        {
        }

        public async Task<DataTable> GetExpiryListAsync(int locationId)
        {
            var dataTable = new DataTable();

            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Inventory].[uspGetExpiryReachReport]"))
                    {
                        command.Parameters.Add("@LocationId", SqlDbType.Int).Value = locationId;

                        await connection.OpenAsync();

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            dataTable.Load(reader);
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("Failed to generate Reorder Level report.", ex);
            }

            return dataTable;
        }
        public async Task<DataTable> GetReorderListAsync(int locationId)
        {
            var dataTable = new DataTable();

            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Inventory].[uspGetReorderLevelReport]"))
                    {
                        command.Parameters.Add("@LocationId", SqlDbType.Int).Value = locationId;

                        await connection.OpenAsync();

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            dataTable.Load(reader);
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("Failed to generate Reorder Level report.", ex);
            }

            return dataTable;
        }
        public async Task<DataTable> GetStockReportAsync(DateTime toDate, int? categoryId, int locationId)
        {
            var dataTable = new DataTable();

            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Inventory].[GetInventoryFromTransactions]"))
                    {
                        command.Parameters.Add("@LocationId", SqlDbType.Int).Value = locationId;
                        command.Parameters.Add("@ToDate", SqlDbType.DateTime2).Value = toDate;
                        command.Parameters.Add("@CategoryId", SqlDbType.Int).Value =
                            categoryId.HasValue ? (object)categoryId.Value : DBNull.Value;

                        await connection.OpenAsync();

                        // Load directly into DataTable (Best for Reporting)
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            dataTable.Load(reader);
                        }

                        EnsureUnitMeasureColumn(dataTable);
                    }
                }
            }
            catch (SqlException ex)
            {
                // Log error or throw specific exception
                throw new InvalidOperationException("A database error occurred while generating the stock report.", ex);
            }

            return dataTable;
        }

        private static void EnsureUnitMeasureColumn(DataTable dataTable)
        {
            if (!dataTable.Columns.Contains("UnitMeasure"))
            {
                dataTable.Columns.Add("UnitMeasure", typeof(string));
            }

            foreach (DataRow row in dataTable.Rows)
            {
                row["UnitMeasure"] = row["UnitMeasure"] != DBNull.Value
                    ? row["UnitMeasure"].ToString()
                    : string.Empty;
            }
        }

        public async Task<DataTable> GetStockMovementLedgerAsync(DateTime startDate, DateTime endDate, int productId, int locationId)
        {
            var dataTable = new DataTable();

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Inventory].[uspGetStockMovementLedger]"))
                {
                    command.Parameters.Add("@ProductId", SqlDbType.Int).Value = productId;
                    command.Parameters.Add("@LocationId", SqlDbType.Int).Value = locationId;
                    command.Parameters.Add("@StartDate", SqlDbType.DateTime2).Value = startDate;
                    command.Parameters.Add("@EndDate", SqlDbType.DateTime2).Value = endDate;

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                        dataTable.Load(reader);
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("Failed to generate Stock Movement Ledger.", ex);
            }

            return dataTable;
        }

        public async Task<List<CategoryValueDto>> GetInventoryValueByCategoryAsync(int locationId)
        {
            var list = new List<CategoryValueDto>();

            using (var conn = GetConnection())
            using (var cmd = CreateCommand(conn, "[Inventory].[uspGetInventoryValueByCategory]"))
            {
                cmd.Parameters.Add("@LocationId", SqlDbType.Int).Value = locationId;

                await conn.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        list.Add(new CategoryValueDto
                        {
                            CategoryName = GetValue<string>(reader, "CategoryName"),
                            TotalValue = GetValue<decimal>(reader, "TotalValue")
                        });
                    }
                }
            }
            return list;
        }

    }
}
