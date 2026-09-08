using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Inventory;
using PointOfSale.Core.Models.Inventory;

namespace PointOfSale.Infrastructure.Repositories.Inventory
{
    public class StockAdjustmentRepository : BaseRepository, IStockAdjustmentRepository
    {
        public StockAdjustmentRepository(DatabaseConnection dbConnection) : base(dbConnection) { }

        #region Public Methods
        public async Task<long> CreateAsync(StockAdjustment stockAdjustment)
        {
            if (stockAdjustment == null)
                throw new ArgumentNullException(nameof(stockAdjustment));

            if (stockAdjustment.Lines == null || !stockAdjustment.Lines.Any())
                throw new ArgumentException("At least one stock adjustment line is required.", nameof(stockAdjustment));

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Inventory].[uspInsertStockAdjustment]"))
                {
                    command.CommandTimeout = 120;

                    AddStockAdjustmentParameters(command, stockAdjustment);

                    var idParameter = new SqlParameter("@StockAdjustmentId", SqlDbType.BigInt)
                    {
                        Direction = ParameterDirection.Output
                    };
                    command.Parameters.Add(idParameter);

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();

                    if (idParameter.Value == null || idParameter.Value == DBNull.Value)
                    {
                        throw new InvalidOperationException("Stock adjustment was saved, but the database did not return a StockAdjustmentId.");
                    }

                    return Convert.ToInt64(idParameter.Value);
                }
            }
            catch (SqlException ex) when (ex.Number == -2)
            {
                throw new InvalidOperationException("Save failed due to network timeout. Please try again.", ex);
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2627 || ex.Number == 2601 || ex.Number == 50000)
                {
                    throw new InvalidOperationException(ex.Message, ex);
                }

                throw new InvalidOperationException("A database error occurred while creating the stock adjustment.", ex);
            }
        }

        public async Task<IEnumerable<StockAdjustment>> GetAllAsync()
        {
            var adjustments = new List<StockAdjustment>();

            using (var connection = GetConnection())
            using (var command = CreateCommand(connection, "[Inventory].[uspGetAllStockAdjustments]"))
            {
                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        adjustments.Add(MapStockAdjustment(reader));
                    }
                }
            }

            return adjustments;
        }

        public async Task<IEnumerable<AvailableQuantityDto>> GetAvailableQty(int locationId, int productId)
        {
            var availableQuantities = new List<AvailableQuantityDto>();

            using (var connection = GetConnection())
            using (var command = CreateCommand(connection, "[Inventory].[uspGetAvailableQuantityByProduct]"))
            {
                command.Parameters.Add("@LocationId", SqlDbType.Int).Value = locationId;
                command.Parameters.Add("@ProductId", SqlDbType.Int).Value = productId;

                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        availableQuantities.Add(MapAvailableQuantity(reader));
                    }
                }
            }

            return availableQuantities;
        }

        public async Task<List<StockAdjustmentHeaderDto>> GetHistoryAsync(DateTime? fromDate, DateTime? toDate, int? locationId)
        {
            var headers = new List<StockAdjustmentHeaderDto>();
            var lines = new List<StockAdjustmentLineDto>();

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Inventory].[uspGetStockAdjustmentHistory]"))
                {
                    command.Parameters.Add("@FromDate", SqlDbType.Date).Value = fromDate.HasValue ? (object)fromDate.Value.Date : DBNull.Value;
                    command.Parameters.Add("@ToDate", SqlDbType.Date).Value = toDate.HasValue ? (object)toDate.Value.Date : DBNull.Value;
                    command.Parameters.Add("@LocationId", SqlDbType.Int).Value = locationId.HasValue ? (object)locationId.Value : DBNull.Value;

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            headers.Add(MapStockAdjustmentHeader(reader));
                        }

                        if (await reader.NextResultAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                lines.Add(MapStockAdjustmentLine(reader));
                            }
                        }
                    }
                }

                var headerLookup = headers.ToDictionary(x => x.Id);
                foreach (var line in lines)
                {
                    if (headerLookup.TryGetValue(line.StockAdjustmentId, out var header))
                    {
                        header.Lines.Add(line);
                    }
                }

                return headers;
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while loading stock adjustment history.", ex);
            }
        }
        #endregion

        #region Private Methods
        private void AddStockAdjustmentParameters(SqlCommand command, StockAdjustment stockAdjustment)
        {
            command.Parameters.Add("@BranchId", SqlDbType.Int).Value = stockAdjustment.BranchId;
            command.Parameters.Add("@UserId", SqlDbType.Int).Value = stockAdjustment.UserId;
            command.Parameters.Add("@LocationId", SqlDbType.Int).Value = stockAdjustment.LocationId;
            command.Parameters.Add("@Note", SqlDbType.NVarChar, -1).Value =
                string.IsNullOrWhiteSpace(stockAdjustment.Note) ? (object)DBNull.Value : stockAdjustment.Note.Trim();

            var linesParameter = command.Parameters.Add("@AdjustmentLines", SqlDbType.Structured);
            linesParameter.TypeName = "[Inventory].[StockAdjustmentLineType]";
            linesParameter.Value = CreateLinesDataTable(stockAdjustment.Lines);
        }

        private DataTable CreateLinesDataTable(IEnumerable<StockAdjustmentLine> lines)
        {
            var table = new DataTable();
            table.Columns.Add("ProductId", typeof(int));
            table.Columns.Add("Quantity", typeof(decimal));
            table.Columns.Add("Reason", typeof(string));

            foreach (var line in lines)
            {
                if (line.ProductId <= 0)
                    throw new InvalidOperationException("A valid product is required for every stock adjustment line.");

                if (line.Quantity == 0)
                    throw new InvalidOperationException($"Adjustment quantity is required for '{line.ProductName ?? line.ProductId.ToString()}'.");

                table.Rows.Add(
                    line.ProductId,
                    line.Quantity,
                    string.IsNullOrWhiteSpace(line.Reason) ? (object)DBNull.Value : line.Reason.Trim());
            }

            return table;
        }

        private StockAdjustment MapStockAdjustment(IDataRecord record)
        {
            return new StockAdjustment
            {
                StockAdjustmentId = GetValue<long>(record, "Id"),
                AdjustDate = GetValue<DateTime>(record, "AdjustDate"),
                Quantity = GetValue<decimal>(record, "Quantity"),
                Reason = GetValue<string>(record, "Reason"),
                ProductId = GetValue<int>(record, "ProductId"),
                ProductName = GetValue<string>(record, "ProductName"),
                UserId = GetValue<int>(record, "UserId"),
                LocationId = GetValue<int>(record, "LocationId"),
                UserName = GetValue<string>(record, "UserName"),
                LocationName = GetValue<string>(record, "LocationName")
            };
        }

        private StockAdjustmentHeaderDto MapStockAdjustmentHeader(IDataRecord record)
        {
            return new StockAdjustmentHeaderDto
            {
                Id = GetValue<long>(record, "Id"),
                AdjustmentNumber = GetValue<string>(record, "AdjustmentNumber"),
                AdjustDate = GetValue<DateTime>(record, "AdjustDate"),
                LocationId = GetValue<int>(record, "LocationId"),
                LocationName = GetValue<string>(record, "LocationName"),
                Note = GetValue<string>(record, "Note"),
                Status = GetValue<string>(record, "Status"),
                UserId = GetValue<int>(record, "UserId"),
                CreatedByName = GetValue<string>(record, "CreatedByName"),
                TotalAmount = GetValue<decimal>(record, "TotalAmount")
            };
        }

        private StockAdjustmentLineDto MapStockAdjustmentLine(IDataRecord record)
        {
            return new StockAdjustmentLineDto
            {
                Id = GetValue<long>(record, "Id"),
                StockAdjustmentId = GetValue<long>(record, "StockAdjustmentId"),
                ProductId = GetValue<int>(record, "ProductId"),
                ProductName = GetValue<string>(record, "ProductName"),
                Quantity = GetValue<decimal>(record, "Quantity"),
                UnitCost = GetValue<decimal>(record, "UnitCost"),
                Total = GetValue<decimal>(record, "Total"),
                Reason = GetValue<string>(record, "Reason")
            };
        }

        private AvailableQuantityDto MapAvailableQuantity(DbDataReader reader)
        {
            return new AvailableQuantityDto
            {
                ProductId = Convert.ToInt32(reader["Id"]),
                ProductName = reader["ProductName"].ToString(),
                Quantity = Convert.ToDecimal(reader["TotalAvailableQuantity"])
            };
        }
        #endregion
    }
}
