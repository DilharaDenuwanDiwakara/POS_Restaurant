using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Repositories.Restaurant;
using PointOfSale.Core.Models.Restaurant;

namespace PointOfSale.Infrastructure.Repositories.Restaurant
{
    public class MenuCategoryRepository : BaseRepository, IMenuCategoryRepository
    {
        public MenuCategoryRepository(DatabaseConnection databaseConnection) : base(databaseConnection)
        {
        }

        #region Public Methods
        public async Task<int> CreateAsync(MenuCategory menuCategory)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Restaurant].[uspInsertMenuCategory]"))
                    {
                        AddMenuCategoryParameters(command, menuCategory);

                        var categoryId = command.Parameters.Add("@Id", SqlDbType.Int);
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

                throw new InvalidOperationException("A database error occured while creating the menu category.", ex);
            }
        }
        public async Task<IEnumerable<MenuCategory>> GetAllAsync()
        {
            var categories = new List<MenuCategory>();
            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Restaurant].[uspGetAllMenuCategories]"))
                    {
                        await connection.OpenAsync();

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                categories.Add(MapMenuCategory(reader));
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
        public Task UpdateAsync(MenuCategory menuCategory)
        {
            return UpdateMenuCategoryAsync(menuCategory);
        }

        public async Task UpdateMenuCategoryAsync(MenuCategory menuCategory)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Restaurant].[uspUpdateMenuCategory]"))
                    {
                        command.Parameters.Add("@Id", SqlDbType.Int).Value = menuCategory.Id;
                        AddMenuCategoryParameters(command, menuCategory);

                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occurred while updating category with ID {menuCategory.Id}.", ex);
            }
        }
        public async Task DeleteAsync(int id)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Restaurant].[uspDeleteMenuCategory]"))
                    {
                        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;

                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number == 547)
                    throw new InvalidOperationException("Cannot delete this category because it is assigned to one or more menu items.", ex);

                if (ex.Number == 50000)
                    throw new InvalidOperationException(ex.Message, ex);

                throw new InvalidOperationException($"A database error occurred while deleting menu category with ID {id}.", ex);
            }
        }
        #endregion

        #region Private Methods
        private void AddMenuCategoryParameters(SqlCommand command, MenuCategory menuCategory)
        {
            var isParentCategory = !menuCategory.ParentId.HasValue || menuCategory.ParentId.Value == 0;

            command.Parameters.Add("@DisplayOrder", SqlDbType.Int).Value = menuCategory.DisplayOrder;
            command.Parameters.Add("@Name", SqlDbType.NVarChar, 100).Value = menuCategory.Name;
            command.Parameters.Add("@ParentId", SqlDbType.Int).Value = (object)menuCategory.ParentId ?? DBNull.Value;
            command.Parameters.Add("@IsActive", SqlDbType.Bit).Value = menuCategory.IsActive;
            command.Parameters.Add("@IncomeAccountCode", SqlDbType.NVarChar, 50).Value =
                isParentCategory && !string.IsNullOrWhiteSpace(menuCategory.IncomeAccountCode)
                    ? (object)menuCategory.IncomeAccountCode
                    : DBNull.Value;
        }
        private MenuCategory MapMenuCategory(IDataRecord record)
        {
            return new MenuCategory
            {
                Id = GetFirstValue<int>(record, "Id", "CategoryId", "MenuCategoryId"),
                DisplayOrder = HasColumn(record, "DisplayOrder") ? GetValue<int>(record, "DisplayOrder") : 0,
                Name = GetFirstValue<string>(record, "Name", "CategoryName"),
                ParentId = HasColumn(record, "ParentId") ? GetValue<int?>(record, "ParentId") : null,
                ParentName = HasColumn(record, "ParentName") ? GetValue<string>(record, "ParentName") : null,
                IsActive = !HasColumn(record, "IsActive") || GetValue<bool>(record, "IsActive"),
                IncomeAccountCode = HasColumn(record, "IncomeAccountCode") ? GetValue<string>(record, "IncomeAccountCode") : null
            };
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

        private T GetFirstValue<T>(IDataRecord record, params string[] columnNames)
        {
            foreach (var columnName in columnNames)
            {
                if (HasColumn(record, columnName))
                {
                    return GetValue<T>(record, columnName);
                }
            }

            throw new IndexOutOfRangeException(
                $"None of these columns were returned by [Restaurant].[uspGetAllMenuCategories]: {string.Join(", ", columnNames)}");
        }
        #endregion
    }
}
