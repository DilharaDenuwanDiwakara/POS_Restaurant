using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Repositories.Inventory;
using PointOfSale.Core.Models.Inventory;

namespace PointOfSale.Infrastructure.Repositories.Inventory
{
    public class ProductBatchRepository : BaseRepository, IProductBatchRepository
    {
        public ProductBatchRepository(DatabaseConnection dbConnection) : base(dbConnection) { }

        public async Task<IEnumerable<ProductBatch>> GetAvailableBatchesAsync(int productId, int locationId)
        {
            var productsBatch = new List<ProductBatch>();

            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Inventory].[uspGetAvailableProductBatches]"))
                    {
                        // Add the parameter for the ProductId
                        command.Parameters.Add(new SqlParameter("@ProductId", SqlDbType.Int) { Value = productId });
                        command.Parameters.Add(new SqlParameter("@LocationId", SqlDbType.Int) { Value = locationId });

                        await connection.OpenAsync();

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                productsBatch.Add(MapProductBatch(reader));
                            }
                        }
                    }

                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occured while selecting the productBatch.", ex);
            }
            return productsBatch;
        }

        private ProductBatch MapProductBatch(IDataRecord record)
        {
            return new ProductBatch
            {
                BatchId = Convert.ToInt64(record["Id"]),
                SupplierId = GetValue<int>(record, "SupplierId"),
                SupplierName = GetValue<string>(record, "SupplierName"),
                ProductId = GetValue<int>(record, "ProductId"),
                ProductName = GetValue<string>(record, "ProductName"),
                AvailableQuantity = GetValue<decimal>(record, "AvailableQuantity"),
                UnitCost = GetValue<decimal>(record, "CostPrice"),
                ReceivedDate = GetValue<DateTime>(record, "ReceivedDate")
            };
        }
    }
}
