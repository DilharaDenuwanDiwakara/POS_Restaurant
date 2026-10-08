using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Inventory;

namespace PointOfSale.Infrastructure.Repositories.Inventory
{
    public class SubRecipeRepository : BaseRepository, ISubRecipeRepository
    {
        public SubRecipeRepository(DatabaseConnection dbConnection) : base(dbConnection)
        {
        }

        /// <summary>
        /// Gets the bill of material ingredient lines for a semi-finished output product.
        /// </summary>
        public async Task<IList<SubRecipeLineDto>> GetSubRecipeAsync(int outputProductId)
        {
            var lines = new List<SubRecipeLineDto>();

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Inventory].[uspGetSubRecipe]"))
                {
                    command.Parameters.Add("@OutputProductId", SqlDbType.Int).Value = outputProductId;

                    await connection.OpenAsync();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            lines.Add(new SubRecipeLineDto
                            {
                                ProductId = GetValue<int>(reader, "ProductId"),
                                ProductName = GetValue<string>(reader, "ProductName"),
                                UnitMeasureId = GetValue<int>(reader, "UnitMeasureId"),
                                UnitMeasureName = GetValueOrDefault<string>(reader, "UnitMeasureName", "UnitName"),
                                Quantity = GetValueOrDefault<decimal>(reader, "Quantity", "QuantityRequired"),
                                StandardCost = GetValueOrDefault<decimal>(reader, "StandardCost")
                            });
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while loading the sub-recipe.", ex);
            }

            return lines;
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

        public Task<IList<SubRecipeLineDto>> GetByOutputProductAsync(int outputProductId)
        {
            return GetSubRecipeAsync(outputProductId);
        }

        public async Task<bool> IsUsedAsMenuIngredientAsync(int outputProductId)
        {
            const string sql = @"
SELECT TOP 1 1
FROM [Inventory].[Recipe]
WHERE VariantId IS NOT NULL
  AND ProductId = @OutputProductId;";

            try
            {
                using (var connection = GetConnection())
                using (var command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = sql;
                    command.Parameters.Add("@OutputProductId", SqlDbType.Int).Value = outputProductId;

                    await connection.OpenAsync();
                    var result = await command.ExecuteScalarAsync();
                    return result != null && result != DBNull.Value;
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while checking sub-recipe usage.", ex);
            }
        }

        /// <summary>
        /// Replaces the bill of material ingredient lines for a semi-finished output product.
        /// </summary>
        public async Task SaveAsync(int outputProductId, IEnumerable<SubRecipeLineDto> recipeLines)
        {
            const string deleteSql = @"
DELETE FROM [Inventory].[Recipe]
WHERE OutputProductId = @OutputProductId
  AND VariantId IS NULL;";

            const string insertSql = @"
INSERT INTO [Inventory].[Recipe]
    (VariantId, OutputProductId, ProductId, UnitMeasureId, QuantityRequired)
VALUES
    (NULL, @OutputProductId, @ProductId, @UnitMeasureId, @Quantity);";

            try
            {
                using (var connection = GetConnection())
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            using (var deleteCommand = connection.CreateCommand())
                            {
                                deleteCommand.Transaction = transaction;
                                deleteCommand.CommandType = CommandType.Text;
                                deleteCommand.CommandText = deleteSql;
                                deleteCommand.Parameters.Add("@OutputProductId", SqlDbType.Int).Value = outputProductId;
                                await deleteCommand.ExecuteNonQueryAsync();
                            }

                            foreach (var line in recipeLines)
                            {
                                using (var insertCommand = connection.CreateCommand())
                                {
                                    insertCommand.Transaction = transaction;
                                    insertCommand.CommandType = CommandType.Text;
                                    insertCommand.CommandText = insertSql;
                                    insertCommand.Parameters.Add("@OutputProductId", SqlDbType.Int).Value = outputProductId;
                                    insertCommand.Parameters.Add("@ProductId", SqlDbType.Int).Value = line.ProductId;
                                    insertCommand.Parameters.Add("@UnitMeasureId", SqlDbType.Int).Value = line.UnitMeasureId;

                                    var quantity = insertCommand.Parameters.Add("@Quantity", SqlDbType.Decimal);
                                    quantity.Precision = 18;
                                    quantity.Scale = 3;
                                    quantity.Value = line.Quantity;

                                    await insertCommand.ExecuteNonQueryAsync();
                                }
                            }

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
                throw new InvalidOperationException($"A database error occurred while saving the sub-recipe. {ex.Message}", ex);
            }
        }
    }
}
