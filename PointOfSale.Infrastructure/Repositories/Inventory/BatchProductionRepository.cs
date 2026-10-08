using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Exception;
using PointOfSale.Core.Interfaces.Repositories.Inventory;

namespace PointOfSale.Infrastructure.Repositories.Inventory
{
    public class BatchProductionRepository : BaseRepository, IBatchProductionRepository
    {
        public BatchProductionRepository(DatabaseConnection databaseConnection)
            : base(databaseConnection)
        {
        }

        /// <summary>
        /// Calls Inventory.uspProcessBatchProduction to increase the output item stock and deduct ingredient stock.
        /// </summary>
        public async Task<bool> ProcessBatchAsync(BatchProductionRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Inventory].[uspProcessBatchProduction]"))
                {
                    command.Parameters.Add("@OutputProductId", SqlDbType.Int).Value = request.OutputProductId;

                    var producedQtyParameter = command.Parameters.Add("@ProducedQty", SqlDbType.Decimal);
                    producedQtyParameter.Precision = 18;
                    producedQtyParameter.Scale = 3;
                    producedQtyParameter.Value = request.ProducedQty;

                    command.Parameters.Add("@BranchId", SqlDbType.Int).Value = request.BranchId;
                    command.Parameters.Add("@LocationId", SqlDbType.Int).Value = request.LocationId;
                    command.Parameters.Add("@UserId", SqlDbType.Int).Value = request.UserId;

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                    return true;
                }
            }
            catch (SqlException ex)
            {
                throw new BatchProductionException("Failed to process batch production in the database.", ex);
            }
        }

        public async Task<IList<BatchProductionHistoryDto>> GetHistoryAsync(DateTime fromDate, DateTime toDate)
        {
            var history = new List<BatchProductionHistoryDto>();

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Inventory].[uspGetBatchProductionHistory]"))
                {
                    command.Parameters.Add("@FromDate", SqlDbType.DateTime).Value = fromDate;
                    command.Parameters.Add("@ToDate", SqlDbType.DateTime).Value = toDate;

                    await connection.OpenAsync();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            history.Add(new BatchProductionHistoryDto
                            {
                                StockTransactionId = GetValue<long>(reader, "StockTransactionId"),
                                ProductId = GetValueOrDefault<int>(reader, "ProductId"),
                                ItemName = GetValue<string>(reader, "ItemName"),
                                YieldQuantity = GetValueOrDefault<decimal>(reader, "YieldQuantity", "YieldQty"),
                                UnitMeasureName = GetValueOrDefault<string>(reader, "UnitMeasureName", "UomName"),
                                ProcessedAt = GetValueOrDefault<DateTime>(reader, "ProcessedAt", "TransactionTime")
                            });
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new BatchProductionException(
                    "Failed to load batch production history from the database. SQL: " + ex.Message,
                    ex);
            }

            return history;
        }

        private T GetValueOrDefault<T>(IDataRecord record, params string[] columnNames)
        {
            foreach (var columnName in columnNames)
            {
                if (HasColumn(record, columnName))
                {
                    return GetValue<T>(record, columnName);
                }
            }

            return default(T);
        }

        private static bool HasColumn(IDataRecord record, string columnName)
        {
            for (var i = 0; i < record.FieldCount; i++)
            {
                if (string.Equals(record.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
