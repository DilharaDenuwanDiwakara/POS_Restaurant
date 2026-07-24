using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Repositories.Inventory;
using PointOfSale.Core.Models.Inventory;

namespace PointOfSale.Infrastructure.Repositories.Inventory
{
    public class CategoryRepository : BaseRepository, ICategoryRepository
    {
        public CategoryRepository(DatabaseConnection dbConnection) : base(dbConnection) { }

        #region Public Methods
        public async Task<int> CreateAsync(Category category)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Inventory].[uspInsertCategory]"))
                    {
                        AddCategoryParameters(command, category);

                        var categoryId = command.Parameters.Add("@CategoryId", SqlDbType.Int);
                        categoryId.Direction = ParameterDirection.Output;

                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();

                        return (int)categoryId.Value;
                    }
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2627 || ex.Number == 2601 || ex.Number == 50000) // Unique constraint error number
                {
                    throw new InvalidOperationException(ex.Message, ex);
                }

                throw new InvalidOperationException("A database error occured while creating the category.", ex);
            }
        }
        public async Task<IEnumerable<Category>> GetAllAsync()
        {
            var categories = new List<Category>();
            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Inventory].[uspGetAllCategories]"))
                    {
                        await connection.OpenAsync();

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                categories.Add(MapCategory(reader));
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occured while selecting the categories.", ex);
            }
            return categories;
        }
        public Task UpdateAsync(Category category)
        {
            return UpdateProductCategoryAsync(category);
        }

        public async Task UpdateProductCategoryAsync(Category productCategory)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Inventory].[uspUpdateCategory]"))
                    {
                        command.Parameters.Add("@CategoryId", SqlDbType.Int).Value = productCategory.CategoryId;
                        AddCategoryParameters(command, productCategory);

                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occurred while updating category with ID {productCategory.CategoryId}.", ex);
            }
        }
        public async Task DeleteAsync(int id)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Inventory].[uspDeleteCategory]"))
                    {
                        command.Parameters.Add("@CategoryId", SqlDbType.Int).Value = id;

                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occurred while deleting category with ID {id}.", ex);
            }
        }
        #endregion

        #region Private Methods
        private void AddCategoryParameters(SqlCommand command, Category category)
        {
            var isParentCategory = !category.ParentCategoryId.HasValue || category.ParentCategoryId.Value == 0;

            command.Parameters.Add("@Code", SqlDbType.NChar, 3).Value = category.Code;
            command.Parameters.Add("@Name", SqlDbType.NVarChar, 100).Value = category.Name;
            command.Parameters.Add("@ParentCategoryId", SqlDbType.Int).Value =
                (object)category.ParentCategoryId ?? DBNull.Value;
            command.Parameters.Add("@StockAccountCode", SqlDbType.NVarChar, 50).Value =
                isParentCategory && !string.IsNullOrWhiteSpace(category.StockAccountCode)
                    ? (object)category.StockAccountCode
                    : DBNull.Value;
            command.Parameters.Add("@CostOfSalesAccountCode", SqlDbType.NVarChar, 50).Value =
                isParentCategory && !string.IsNullOrWhiteSpace(category.CostOfSalesAccountCode)
                    ? (object)category.CostOfSalesAccountCode
                    : DBNull.Value;
        }
        private Category MapCategory(IDataRecord record)
        {
            return new Category
            {
                CategoryId = GetValue<int>(record, "Id"),
                Code = GetValue<string>(record, "Code"),
                Name = GetValue<string>(record, "CategoryName"),
                ParentCategoryId = GetValue<int?>(record, "ParentCategoryId"),
                ParentCategoryName = GetValue<string>(record, "ParentCategoryName"),
                StockAccountCode = GetOptionalString(record, "StockAccountCode"),
                CostOfSalesAccountCode = GetOptionalString(record, "CostOfSalesAccountCode")
            };
        }

        private static string GetOptionalString(IDataRecord record, string columnName)
        {
            for (var i = 0; i < record.FieldCount; i++)
            {
                if (string.Equals(record.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
                    return record.IsDBNull(i) ? null : Convert.ToString(record.GetValue(i));
            }

            return null;
        }
        #endregion
    }
}
