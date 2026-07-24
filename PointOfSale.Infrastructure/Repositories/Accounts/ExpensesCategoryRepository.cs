using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Repositories.Accounts;
using PointOfSale.Core.Models.Accounts.Entities;

namespace PointOfSale.Infrastructure.Repositories.Accounts
{
    public class ExpensesCategoryRepository : BaseRepository, IExpensesCategoryRepository
    {
        public ExpensesCategoryRepository(DatabaseConnection dbConnection) : base(dbConnection) { }

        #region Public Methods
        public async Task<int> CreateAsync(ExpensesCategory expensesCategory)
        {
            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Accounts].[uspInsertExpensesCategory]"))
                {
                    AddBrandParameters(command, expensesCategory);

                    var brandId = command.Parameters.Add("@ExpensesCategoryId", SqlDbType.Int);
                    brandId.Direction = ParameterDirection.Output;

                    await connection.OpenAsync();

                    await command.ExecuteNonQueryAsync();

                    return (int)brandId.Value;
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2627 || ex.Number == 2601 || ex.Number == 50000) // Unique constraint error number
                {
                    throw new InvalidOperationException(ex.Message, ex);
                }

                throw new InvalidOperationException("A database error occured while creating the brand.", ex);
            }
        }
        public async Task<IEnumerable<ExpensesCategory>> GetAllAsync()
        {
            var expensesCategory = new List<ExpensesCategory>();

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Accounts].[uspGetAllExpensesCategory]"))
                {
                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            expensesCategory.Add(MapBrand(reader));
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occured while retrieving the brands.", ex);
            }
            return expensesCategory;
        }
        public async Task UpdateAsync(ExpensesCategory expensesCategory)
        {
            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Accounts].[uspUpdateExpensesCategory]"))
                {
                    AddBrandIdParameter(command, expensesCategory.ExpensesCategoryId);
                    AddBrandParameters(command, expensesCategory);

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occurred while updating brand with ID {expensesCategory.ExpensesCategoryId}.", ex);
            }
        }
        public async Task DeleteAsync(int id)
        {
            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Accounts].[uspDeleteExpensesCategory]"))
                {
                    AddBrandIdParameter(command, id);

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occurred while deleting brand with ID {id}.", ex);
            }
        }
        #endregion

        #region Private Methods
        private void AddBrandParameters(SqlCommand command, ExpensesCategory expensesCategory)
        {
            command.Parameters.Add("@ExpensesCategoryName", SqlDbType.NVarChar, 50).Value = expensesCategory.ExpensesCategoryName;
            command.Parameters.Add("@AccountId", SqlDbType.Int).Value = expensesCategory.AccountId;
        }
        private void AddBrandIdParameter(SqlCommand command, int expensesCategoryId)
        {
            command.Parameters.Add("@ExpensesCategoryId", SqlDbType.Int).Value = expensesCategoryId;
        }
        private ExpensesCategory MapBrand(IDataRecord record)
        {
            return new ExpensesCategory
            {
                ExpensesCategoryId = GetValue<int>(record, "Id"),
                ExpensesCategoryName = GetValue<string>(record, "ExpensesCategoryName"),
                AccountId = GetValue<int>(record, "AccountId")
            };
        }

        #endregion
    }
}
