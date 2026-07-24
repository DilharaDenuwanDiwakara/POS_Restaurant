using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Repositories.Inventory;
using PointOfSale.Core.Models.Inventory;

namespace PointOfSale.Infrastructure.Repositories.Inventory
{
    public class ProductRepository : BaseRepository, IProductRepository
    {
        public ProductRepository(DatabaseConnection dbConnection) : base(dbConnection) { }

        #region Public Methods
        public async Task<int> CreateAsync(Product product)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            using (var command = CreateCommand(connection, "[Inventory].[uspInsertProduct]", transaction))
                            {
                                AddProductParameters(command, product, true);

                                var productId = command.Parameters.Add("@ProductId", SqlDbType.Int);
                                productId.Direction = ParameterDirection.Output;

                                await command.ExecuteNonQueryAsync();

                                var newProductId = (int)productId.Value;
                                await SaveProductUnitConversionsAsync(connection, transaction, newProductId, product.UnitConversions, product.CreatedBy);
                                transaction.Commit();

                                return newProductId;
                            }
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2627 || ex.Number == 2601 || ex.Number == 50000) // Unique constraint error number
                {
                    throw new InvalidOperationException(ex.Message, ex);
                }
                throw new InvalidOperationException("A database error occured while creating the product.", ex);
            }
        }
        public async Task<IEnumerable<Product>> GetAllAsync()
        {
            var products = new List<Product>();

            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Inventory].[uspGetAllProducts]"))
                    {
                        await connection.OpenAsync();

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                products.Add(MapProduct(reader));
                            }
                        }
                    }

                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occured while selecting the products.", ex);
            }
            return products;
        }
        public async Task<IEnumerable<ItemTypeModel>> GetItemTypesAsync()
        {
            var itemTypes = new List<ItemTypeModel>();

            using (var connection = GetConnection())
            {
                using (var command = CreateCommand(connection, "SELECT Id, TypeName FROM [Inventory].[ItemType] ORDER BY TypeName"))
                {
                    command.CommandType = CommandType.Text;

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            itemTypes.Add(new ItemTypeModel
                            {
                                Id = GetValue<int>(reader, "Id"),
                                TypeName = GetValue<string>(reader, "TypeName")
                            });
                        }
                    }
                }
            }

            return itemTypes;
        }
        public async Task<IEnumerable<Product>> SearchProductAsync(string searchTerm)
        {
            var products = new List<Product>();

            using (var connection = GetConnection())
            {
                using (var command = CreateCommand(connection, "[Inventory].[uspSearchProduct]"))
                {
                    command.Parameters.AddWithValue("@SearchTerm",
                        string.IsNullOrWhiteSpace(searchTerm) ? (object)DBNull.Value : searchTerm);

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            products.Add(MapProduct(reader));
                        }
                    }
                }
                return products;
            }
        }
        public async Task<IEnumerable<Product>> SearchProductWithFilterAsync(int locationId, string searchTerm, int? categoryId = null, int? itemTypeId = null)
        {
            var products = new List<Product>();

            using (var connection = GetConnection())
            {
                using (var command = CreateCommand(connection, "[Inventory].[uspSearchProductWithFilter]"))
                {
                    command.Parameters.AddWithValue("@LocationId", locationId);

                    command.Parameters.AddWithValue("@SearchTerm",
                        string.IsNullOrWhiteSpace(searchTerm) ? (object)DBNull.Value : searchTerm);

                    command.Parameters.Add(new SqlParameter("@CategoryId", SqlDbType.Int)
                    {
                        Value = categoryId.HasValue ? (object)categoryId.Value : DBNull.Value
                    });

                    command.Parameters.Add(new SqlParameter("@ItemTypeId", SqlDbType.Int)
                    {
                        Value = itemTypeId.HasValue ? (object)itemTypeId.Value : DBNull.Value
                    });

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            products.Add(MapProduct(reader));
                        }
                    }
                }
                return products;
            }
        }

        public async Task<IEnumerable<Product>> SearchProductStockAdjestment(string searchTerm)
        {
            var products = new List<Product>();

            using (var connection = GetConnection())
            {
                using (var command = CreateCommand(connection, "[Inventory].[uspSerachProductStockAdjestment]"))
                {

                    command.Parameters.AddWithValue("@SearchTerm",
                        string.IsNullOrWhiteSpace(searchTerm) ? (object)DBNull.Value : searchTerm);


                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            products.Add(MapProductStockAdjestment(reader));
                        }
                    }
                }
                return products;
            }
        }

        public async Task UpdateAsync(Product product)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            using (var command = CreateCommand(connection, "[Inventory].[uspUpdateProduct]", transaction))
                            {
                                command.Parameters.Add("@ProductId", SqlDbType.Int).Value = product.ProductId;
                                AddProductParameters(command, product, false);

                                await command.ExecuteNonQueryAsync();
                            }

                            await SaveProductUnitConversionsAsync(connection, transaction, product.ProductId, product.UnitConversions, product.UpdatedBy);
                            transaction.Commit();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occurred while updating product with ID {product.ProductId}.", ex);
            }
        }
        public async Task DeleteAsync(int id)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Inventory].[uspDeleteProduct]"))
                    {
                        command.Parameters.Add("@ProductId", SqlDbType.Int).Value = id;

                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occurred while deleting product with ID {id}.", ex);
            }
        }

        public async Task UpdateProductCategoryAsync(int productId, int? newCategoryId, int userId)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    await conn.OpenAsync();
                    using (var cmd = CreateCommand(conn, "[Inventory].[uspUpdateProductCategory]"))
                    {

                        cmd.Parameters.AddWithValue("@ProductId", productId);
                        cmd.Parameters.AddWithValue("@CategoryId", (object)newCategoryId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@UpdatedBy", userId);

                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (SqlException ex)
            {
                // Custom error handling for RAISERROR (50000)
                if (ex.Number == 50000)
                {
                    throw new InvalidOperationException(ex.Message, ex);
                }
                throw new InvalidOperationException("A database error occurred while updating the category.", ex);
            }
        }

        public async Task<IEnumerable<ProductUnitConversion>> GetUnitConversionsAsync(int productId)
        {
            var conversions = new List<ProductUnitConversion>();

            using (var connection = GetConnection())
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = @"
SELECT
    puc.Id,
    puc.ProductId,
    puc.TargetUnitMeasureId,
    um.Code AS TargetUnitMeasureCode,
    um.Name AS TargetUnitMeasureName,
    puc.ConversionRate,
    puc.IsActive,
    puc.CreatedBy,
    puc.CreatedAt,
    puc.UpdatedBy,
    puc.UpdatedAt
FROM [Inventory].[ProductUnitConversion] puc
INNER JOIN [Inventory].[UnitMeasure] um ON um.Id = puc.TargetUnitMeasureId
WHERE puc.ProductId = @ProductId
  AND puc.IsActive = 1
ORDER BY um.Name;";
                    command.Parameters.Add("@ProductId", SqlDbType.Int).Value = productId;

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            conversions.Add(MapProductUnitConversion(reader));
                        }
                    }
                }
            }

            return conversions;
        }
        #endregion

        #region Private Methods
        public async Task<long> ReserveSequenceRangeAsync(int count)
        {
            using (var conn = GetConnection())
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "sp_sequence_get_range";
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@sequence_name", "Inventory.Seq_ProductCode");
                    cmd.Parameters.AddWithValue("@range_size", count);

                    var firstValueParam = cmd.Parameters.Add("@range_first_value", SqlDbType.Variant);
                    firstValueParam.Direction = ParameterDirection.Output;

                    await cmd.ExecuteNonQueryAsync();

                    // Handle DBNull if sequence hasn't started, though unlikely for system sequence
                    if (firstValueParam.Value == DBNull.Value) return 1;

                    return Convert.ToInt64(firstValueParam.Value);
                }
            }
        }
        private void AddProductParameters(SqlCommand command, Product product, bool isInsert)
        {
            command.Parameters.Add("@CategoryId", SqlDbType.Int).Value = product.CategoryId;
            command.Parameters.Add("@Barcode", SqlDbType.NVarChar, 50).Value = product.Barcode ?? (object)DBNull.Value;
            command.Parameters.Add("@ProductName", SqlDbType.NVarChar, 100).Value = product.ProductName;
            command.Parameters.Add("@UnitMeasureId", SqlDbType.Int).Value = product.UnitMeasureId;
            command.Parameters.Add("@ItemTypeId", SqlDbType.Int).Value =
                product.ItemTypeId.HasValue ? (object)product.ItemTypeId.Value : DBNull.Value;
            AddDecimalParameter(command, "@ReorderPoint", product.ReorderPoint);
            AddDecimalParameter(command, "@MaxStockQuantity", product.MaxStockQuantity);
            AddDecimalParameter(command, "@AdditionalStockQuantity", product.AdditionalStockQuantity);
            AddDecimalParameter(command, "@WastagePercentage", product.WastagePercentage);
            command.Parameters.Add("@IsPurchasable", SqlDbType.Bit).Value = product.IsPurchasable;
            command.Parameters.Add("@IsActive", SqlDbType.Bit).Value = product.IsActive;
            command.Parameters.Add("@IsTaxApplicable", SqlDbType.Bit).Value = product.IsTaxApplicable;
            command.Parameters.Add("@TrackExpiry", SqlDbType.Bit).Value = product.TrackExpiry;
            command.Parameters.Add(isInsert ? "@CreatedBy" : "@UpdatedBy", SqlDbType.Int).Value =
                isInsert
                    ? (product.CreatedBy.HasValue ? (object)product.CreatedBy.Value : DBNull.Value)
                    : (product.UpdatedBy.HasValue ? (object)product.UpdatedBy.Value : DBNull.Value);

        }

        private void AddDecimalParameter(SqlCommand command, string parameterName, decimal value)
        {
            var parameter = command.Parameters.Add(parameterName, SqlDbType.Decimal);
            parameter.Precision = 18;
            parameter.Scale = 4;
            parameter.Value = value;
        }

        private async Task SaveProductUnitConversionsAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            int productId,
            IEnumerable<ProductUnitConversion> conversions,
            int? userId)
        {
            using (var deleteCommand = connection.CreateCommand())
            {
                deleteCommand.Transaction = transaction;
                deleteCommand.CommandType = CommandType.Text;
                deleteCommand.CommandText = @"
UPDATE [Inventory].[ProductUnitConversion]
SET IsActive = 0,
    UpdatedBy = @UpdatedBy,
    UpdatedAt = GETDATE()
WHERE ProductId = @ProductId
  AND IsActive = 1;";
                deleteCommand.Parameters.Add("@ProductId", SqlDbType.Int).Value = productId;
                deleteCommand.Parameters.Add("@UpdatedBy", SqlDbType.Int).Value = userId.HasValue ? (object)userId.Value : DBNull.Value;
                await deleteCommand.ExecuteNonQueryAsync();
            }

            if (conversions == null)
            {
                return;
            }

            foreach (var conversion in conversions)
            {
                using (var insertCommand = connection.CreateCommand())
                {
                    insertCommand.Transaction = transaction;
                    insertCommand.CommandType = CommandType.Text;
                    insertCommand.CommandText = @"
INSERT INTO [Inventory].[ProductUnitConversion]
(
    ProductId,
    TargetUnitMeasureId,
    ConversionRate,
    IsActive,
    CreatedBy,
    CreatedAt
)
VALUES
(
    @ProductId,
    @TargetUnitMeasureId,
    @ConversionRate,
    1,
    @CreatedBy,
    GETDATE()
);";
                    insertCommand.Parameters.Add("@ProductId", SqlDbType.Int).Value = productId;
                    insertCommand.Parameters.Add("@TargetUnitMeasureId", SqlDbType.Int).Value = conversion.TargetUnitMeasureId;
                    AddDecimalParameter(insertCommand, "@ConversionRate", conversion.ConversionRate);
                    insertCommand.Parameters.Add("@CreatedBy", SqlDbType.Int).Value = userId.HasValue ? (object)userId.Value : DBNull.Value;
                    await insertCommand.ExecuteNonQueryAsync();
                }
            }
        }

        private Product MapProduct(IDataRecord record)
        {
            return new Product
            {
                ProductId = GetValue<int>(record, "Id"),
                ProductCode = GetValue<string>(record, "Code"),
                Barcode = GetValue<string>(record, "Barcode"),
                ProductName = GetValue<string>(record, "Name"),
                UnitMeasureId = GetValue<int>(record, "UnitMeasureId"),
                UnitMeasureName = GetValue<string>(record, "UnitMeasureName"),
                UnitMeasureCode = GetValue<string>(record, "UnitMeasureCode"),
                ItemTypeId = GetValue<int?>(record, "ItemTypeId"),
                ItemTypeName = GetValue<string>(record, "ItemTypeName"),
                CategoryName = GetValue<string>(record, "CategoryName"),

                StandardCost = GetValue<decimal>(record, "StandardCost"),
                AvailableQuantity = GetValue<decimal>(record, "AvailableQuantity"),

                ReorderPoint = GetValue<decimal>(record, "ReorderPoint"),
                MaxStockQuantity = GetValue<decimal>(record, "MaxStockQuantity"),
                AdditionalStockQuantity = GetValue<decimal>(record, "AdditionalStockQuantity"),
                WastagePercentage = GetValue<decimal>(record, "WastagePercentage"),
                CategoryId = GetValue<int>(record, "CategoryId"),
                IsPurchasable = GetValue<bool>(record, "IsPurchasable"),
                IsActive = GetValue<bool>(record, "IsActive"),
                TrackExpiry = GetValue<bool>(record, "TrackExpiry"),
                IsTaxApplicable = HasColumn(record, "IsTaxApplicable") && GetValue<bool>(record, "IsTaxApplicable")

            };
        }

        private ProductUnitConversion MapProductUnitConversion(IDataRecord record)
        {
            return new ProductUnitConversion
            {
                Id = GetValue<int>(record, "Id"),
                ProductId = GetValue<int>(record, "ProductId"),
                TargetUnitMeasureId = GetValue<int>(record, "TargetUnitMeasureId"),
                TargetUnitMeasureCode = GetValue<string>(record, "TargetUnitMeasureCode"),
                TargetUnitMeasureName = GetValue<string>(record, "TargetUnitMeasureName"),
                ConversionRate = GetValue<decimal>(record, "ConversionRate"),
                IsActive = GetValue<bool>(record, "IsActive"),
                CreatedBy = GetValue<int?>(record, "CreatedBy"),
                CreatedAt = GetValue<DateTime?>(record, "CreatedAt"),
                UpdatedBy = GetValue<int?>(record, "UpdatedBy"),
                UpdatedAt = GetValue<DateTime?>(record, "UpdatedAt")
            };
        }

        private Product MapProductStockAdjestment(IDataRecord record)
        {
            return new Product
            {
                ProductId = GetValue<int>(record, "Id"),
                ProductCode = GetValue<string>(record, "ProductCode"),
                ProductName = GetValue<string>(record, "ProductName"),
                CategoryName = GetValue<string>(record, "CategoryName"),
                ItemTypeId = GetValue<int?>(record, "ItemTypeId"),
                ItemTypeName = GetValue<string>(record, "ItemTypeName"),

                StandardCost = GetValue<decimal>(record, "StandardCost"),

                ReorderPoint = GetValue<decimal>(record, "ReorderPoint"),
                MaxStockQuantity = GetValue<decimal>(record, "MaxStockQuantity"),
                CategoryId = GetValue<int>(record, "CategoryId"),
                IsPurchasable = GetValue<bool>(record, "IsPurchasable"),
                IsActive = GetValue<bool>(record, "IsActive"),
                IsTaxApplicable = HasColumn(record, "IsTaxApplicable") && GetValue<bool>(record, "IsTaxApplicable")

            };
        }

        private bool HasColumn(IDataRecord record, string columnName)
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
        #endregion
    }
}
