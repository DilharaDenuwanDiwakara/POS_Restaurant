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

        #region Public Methods
        public async Task<int> CreateAsync(StockAdjustment stockAdjustment)
        {
            try
            {
                using (var connection = GetConnection())
                {

                    using (var command = CreateCommand(connection, "[Inventory].[uspInsertStockAdjustment]"))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        AddStockAdjustmentParameters(command, stockAdjustment);

                        var idParameter = new SqlParameter("@StockAdjustmentId", SqlDbType.Int)
                        {
                            Direction = ParameterDirection.Output
                        };
                        command.Parameters.Add(idParameter);

                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();

                        return (int)idParameter.Value;
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
        #endregion

        #region Private Methods
        private void AddStockAdjustmentParameters(SqlCommand command, StockAdjustment stockAdjustment)
        {
            command.Parameters.AddWithValue("@BranchId", stockAdjustment.BranchId);
            command.Parameters.AddWithValue("@UserId", stockAdjustment.UserId);
            command.Parameters.AddWithValue("@LocationId", stockAdjustment.LocationId);
            command.Parameters.AddWithValue("@Quantity", stockAdjustment.Quantity);
            command.Parameters.AddWithValue("@Reason", stockAdjustment.Reason);
            command.Parameters.AddWithValue("@ProductId", stockAdjustment.ProductId);
        }

        private StockAdjustment MapStockAdjustment(IDataRecord record)
        {
            return new StockAdjustment
            {
                StockAdjustmentId = GetValue<int>(record, "Id"),
                AdjustDate = GetValue<DateTime>(record, "AdjustDate"),
                Quantity = GetValue<decimal>(record, "Quantity"),
                Reason = GetValue<string>(record, "Reason"),
                ProductId = GetValue<int>(record, "ProductId"),
                ProductName = GetValue<string>(record, "ProductName"),


                // Foreign Key IDs
                UserId = GetValue<int>(record, "UserId"),
                LocationId = GetValue<int>(record, "LocationId"),

                // Display Names (from the JOINs in the SP)
                UserName = GetValue<string>(record, "UserName"),
                LocationName = GetValue<string>(record, "LocationName")
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
