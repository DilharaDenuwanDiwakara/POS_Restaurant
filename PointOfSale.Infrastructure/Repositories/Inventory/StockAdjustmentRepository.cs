using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Inventory;
using PointOfSale.Core.Models.Inventory;

namespace PointOfSale.Infrastructure.Repositories.Inventory
{
    public class StockAdjustmentRepository : BaseRepository, IStockAdjustmentRepository
    {
        public StockAdjustmentRepository(DatabaseConnection dbConnection) : base(dbConnection) { }

        public async Task<long> CreateAsync(StockAdjustment stockAdjustment)
        {
            ValidateForCreate(stockAdjustment);

            try
            {
                using (var connection = GetConnection())
                {
                    await connection.OpenAsync();

                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            long stockAdjustmentId;
                            string adjustmentNumber;

                            await ValidateAvailableStockAsync(connection, transaction, stockAdjustment);

                            using (var command = connection.CreateCommand())
                            {
                                command.Transaction = transaction;
                                command.CommandType = CommandType.Text;
                                command.CommandText = @"
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @GeneratedAdjustmentNumber NVARCHAR(50);

EXEC [System].[uspGetNextCode]
    @BranchId = @BranchId,
    @Prefix = N'ADJ',
    @FormattedCode = @GeneratedAdjustmentNumber OUTPUT;

INSERT INTO [Inventory].[StockAdjustment]
    ([AdjustmentNumber], [LocationId], [AdjustDate], [Note], [Status], [CreatedBy])
VALUES
    (@GeneratedAdjustmentNumber, @LocationId, @AdjustDate, @Note, 'POSTED', @CreatedBy);

SET @StockAdjustmentId = CONVERT(BIGINT, SCOPE_IDENTITY());
SET @AdjustmentNumber = @GeneratedAdjustmentNumber;";

                                command.Parameters.Add("@BranchId", SqlDbType.Int).Value = stockAdjustment.BranchId;
                                command.Parameters.Add("@LocationId", SqlDbType.Int).Value = stockAdjustment.LocationId;
                                command.Parameters.Add("@AdjustDate", SqlDbType.DateTime).Value = stockAdjustment.AdjustDate;
                                command.Parameters.Add("@Note", SqlDbType.NVarChar, 500).Value =
                                    string.IsNullOrWhiteSpace(stockAdjustment.Note)
                                        ? (object)DBNull.Value
                                        : stockAdjustment.Note.Trim();
                                command.Parameters.Add("@CreatedBy", SqlDbType.Int).Value = stockAdjustment.UserId;

                                var idParameter = command.Parameters.Add("@StockAdjustmentId", SqlDbType.BigInt);
                                idParameter.Direction = ParameterDirection.Output;

                                var numberParameter = command.Parameters.Add("@AdjustmentNumber", SqlDbType.NVarChar, 50);
                                numberParameter.Direction = ParameterDirection.Output;

                                await command.ExecuteNonQueryAsync();

                                if (idParameter.Value == DBNull.Value || numberParameter.Value == DBNull.Value)
                                {
                                    throw new InvalidOperationException("The stock adjustment header was not created correctly.");
                                }

                                stockAdjustmentId = Convert.ToInt64(idParameter.Value);
                                adjustmentNumber = Convert.ToString(numberParameter.Value);

                                if (stockAdjustmentId <= 0 || string.IsNullOrWhiteSpace(adjustmentNumber))
                                {
                                    throw new InvalidOperationException("The stock adjustment header was not created correctly.");
                                }
                            }

                            var defaultLineReason = string.IsNullOrWhiteSpace(stockAdjustment.Note)
                                ? "Stock adjustment"
                                : stockAdjustment.Note.Trim();

                            foreach (var line in stockAdjustment.Lines)
                            {
                                var lineReason = string.IsNullOrWhiteSpace(line.Reason)
                                    ? defaultLineReason
                                    : line.Reason.Trim();

                                using (var command = CreateCommand(
                                    connection,
                                    "[Inventory].[uspInsertStockAdjustmentLine]",
                                    transaction))
                                {
                                    command.Parameters.Add("@StockAdjustmentId", SqlDbType.BigInt).Value = stockAdjustmentId;
                                    command.Parameters.Add("@AdjustmentNumber", SqlDbType.NVarChar, 50).Value = adjustmentNumber;
                                    command.Parameters.Add("@BranchId", SqlDbType.Int).Value = stockAdjustment.BranchId;
                                    command.Parameters.Add("@LocationId", SqlDbType.Int).Value = stockAdjustment.LocationId;
                                    command.Parameters.Add("@ProductId", SqlDbType.Int).Value = line.ProductId;

                                    var quantityParameter = command.Parameters.Add("@Quantity", SqlDbType.Decimal);
                                    quantityParameter.Precision = 18;
                                    quantityParameter.Scale = 3;
                                    quantityParameter.Value = line.Quantity;

                                    command.Parameters.Add("@Reason", SqlDbType.NVarChar, 200).Value = lineReason;
                                    command.Parameters.Add("@CreatedBy", SqlDbType.Int).Value = stockAdjustment.UserId;

                                    await command.ExecuteNonQueryAsync();
                                }
                            }

                            transaction.Commit();

                            stockAdjustment.StockAdjustmentId = stockAdjustmentId;
                            stockAdjustment.AdjustmentNumber = adjustmentNumber;

                            return stockAdjustmentId;
                        }
                        catch
                        {
                            try
                            {
                                transaction.Rollback();
                            }
                            catch (InvalidOperationException)
                            {
                                // The SQL Server transaction may already be rolled back by XACT_ABORT.
                            }
                            catch (SqlException)
                            {
                                // Preserve the original database exception when rollback is no longer possible.
                            }

                            throw;
                        }
                    }
                }
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
            {
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
            }

            return adjustments;
        }

        public async Task<IEnumerable<AvailableQuantityDto>> GetAvailableQty(int locationId, int productId)
        {
            var availableQuantities = new List<AvailableQuantityDto>();

            using (var connection = GetConnection())
            {
                using (var command = CreateCommand(connection, "[Inventory].[uspGetAvailableQuantityByProduct]"))
                {
                    // 2. Add the parameters that the stored procedure expects
                    command.Parameters.AddWithValue("@LocationId", locationId);
                    command.Parameters.AddWithValue("@ProductId", productId);

                    await connection.OpenAsync();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            availableQuantities.Add(MapAvailableQuantity(reader));
                        }
                    }
                }
            }

            return availableQuantities;
        }


        #region Private Methods
        private static async Task ValidateAvailableStockAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            StockAdjustment stockAdjustment)
        {
            var quantityChangesByProduct = new Dictionary<int, decimal>();

            foreach (var line in stockAdjustment.Lines)
            {
                if (quantityChangesByProduct.ContainsKey(line.ProductId))
                    quantityChangesByProduct[line.ProductId] += line.Quantity;
                else
                    quantityChangesByProduct.Add(line.ProductId, line.Quantity);
            }

            foreach (var quantityChange in quantityChangesByProduct)
            {
                if (quantityChange.Value >= 0)
                    continue;

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandType = CommandType.Text;
                    command.CommandText = @"
SELECT ISNULL(SUM([AvailableQuantity]), 0)
FROM [Inventory].[LocationStock] WITH (UPDLOCK, HOLDLOCK)
WHERE [LocationId] = @LocationId
  AND [ProductId] = @ProductId;";

                    command.Parameters.Add("@LocationId", SqlDbType.Int).Value = stockAdjustment.LocationId;
                    command.Parameters.Add("@ProductId", SqlDbType.Int).Value = quantityChange.Key;

                    var result = await command.ExecuteScalarAsync();
                    var availableQuantity = result == null || result == DBNull.Value
                        ? 0
                        : Convert.ToDecimal(result);
                    var requiredQuantity = Math.Abs(quantityChange.Value);

                    if (requiredQuantity > availableQuantity)
                    {
                        throw new InvalidOperationException(
                            $"Cannot reduce product {quantityChange.Key} by {requiredQuantity:N3}. " +
                            $"Only {availableQuantity:N3} is available at this location.");
                    }
                }
            }
        }

        private static void ValidateForCreate(StockAdjustment stockAdjustment)
        {
            if (stockAdjustment == null)
                throw new ArgumentNullException(nameof(stockAdjustment));

            if (stockAdjustment.BranchId <= 0)
                throw new InvalidOperationException("A valid branch is required for a stock adjustment.");

            if (stockAdjustment.LocationId <= 0)
                throw new InvalidOperationException("A valid location is required for a stock adjustment.");

            if (stockAdjustment.UserId <= 0)
                throw new InvalidOperationException("A valid user is required for a stock adjustment.");

            if (stockAdjustment.AdjustDate == default(DateTime))
                throw new InvalidOperationException("An adjustment date is required.");

            if (stockAdjustment.Note != null && stockAdjustment.Note.Trim().Length > 500)
                throw new InvalidOperationException("The stock adjustment note cannot exceed 500 characters.");

            if (stockAdjustment.Lines == null || stockAdjustment.Lines.Count == 0)
                throw new InvalidOperationException("At least one stock adjustment line is required.");

            var defaultLineReason = string.IsNullOrWhiteSpace(stockAdjustment.Note)
                ? "Stock adjustment"
                : stockAdjustment.Note.Trim();

            foreach (var line in stockAdjustment.Lines)
            {
                if (line == null)
                    throw new InvalidOperationException("A stock adjustment line cannot be empty.");

                if (line.ProductId <= 0)
                    throw new InvalidOperationException("Every stock adjustment line must have a valid product.");

                if (line.Quantity == 0)
                    throw new InvalidOperationException("A stock adjustment quantity cannot be zero.");

                var lineReason = string.IsNullOrWhiteSpace(line.Reason)
                    ? defaultLineReason
                    : line.Reason.Trim();

                if (lineReason.Length > 200)
                    throw new InvalidOperationException("A stock adjustment line reason cannot exceed 200 characters.");
            }
        }

        private StockAdjustment MapStockAdjustment(IDataRecord record)
        {
            return new StockAdjustment
            {
                StockAdjustmentId = Convert.ToInt64(record["Id"]),
                AdjustDate = Convert.ToDateTime(record["AdjustDate"]),
                Quantity = Convert.ToDecimal(record["Quantity"]),
                Reason = record["Reason"] == DBNull.Value ? null : record["Reason"].ToString(),
                ProductId = Convert.ToInt32(record["ProductId"]),
                ProductName = record["ProductName"].ToString(),

                UserId = Convert.ToInt32(record["UserId"]),
                LocationId = Convert.ToInt32(record["LocationId"]),

                UserName = record["UserName"] == DBNull.Value ? null : record["UserName"].ToString(),
                LocationName = record["LocationName"] == DBNull.Value ? null : record["LocationName"].ToString()
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
