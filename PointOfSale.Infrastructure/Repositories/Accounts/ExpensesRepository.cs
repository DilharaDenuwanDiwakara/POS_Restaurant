using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Repositories.Accounts;
using PointOfSale.Core.Models.Accounts.Entities;

namespace PointOfSale.Infrastructure.Repositories.Accounts
{
    public class ExpensesRepository : BaseRepository, IExpensesRepository
    {
        public ExpensesRepository(DatabaseConnection dbConnection) : base(dbConnection)
        {
        }

        #region Public Method
        public async Task<int> CreateAsync(Expenses expenses)
        {
            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Accounts].[uspInsertExpenses]"))
                {
                    AddExpenseParameters(command, expenses);

                    var outputParam = command.Parameters.Add("@ExpensesId", SqlDbType.Int);
                    outputParam.Direction = ParameterDirection.Output;

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();

                    return (int)outputParam.Value;
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while creating the expense.", ex);
            }
        }
        public async Task<IEnumerable<Expenses>> GetAllAsync(int locationId)
        {
            return await GetAllAsync(locationId, DateTime.Today, DateTime.Today);
        }

        public async Task<IEnumerable<Expenses>> GetAllAsync(int locationId, DateTime fromDate, DateTime toDate)
        {
            var expensesList = new List<Expenses>();

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Accounts].[uspGetAllExpenses]"))
                {
                    command.Parameters.AddWithValue("@LocationId", locationId);
                    command.Parameters.Add("@FromDate", SqlDbType.Date).Value = fromDate.Date;
                    command.Parameters.Add("@ToDate", SqlDbType.Date).Value = toDate.Date;

                    await connection.OpenAsync();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            expensesList.Add(MapExpense(reader));
                        }
                    }
                }

                return expensesList;
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while retrieving expenses.", ex);
            }
        }

        public async Task<DataTable> GetExpenseVoucherAsync(int expensesId)
        {
            var dataTable = new DataTable();

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Accounts].[uspGetExpenseVoucher]"))
                {
                    command.Parameters.Add("@ExpensesId", SqlDbType.Int).Value = expensesId;

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        dataTable.Load(reader);
                    }
                }

                return dataTable;
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException(
                    $"A database error occurred while retrieving Expense Voucher {expensesId}.",
                    ex);
            }
        }
        #endregion

        #region Private Helper Method
        private void AddExpenseParameters(SqlCommand command, Expenses expense)
        {
            // If the stored procedure expects an ExpenseId for updates, it can be added here.
            command.Parameters.Add("@ExpensesDate", SqlDbType.DateTime).Value = expense.ExpensesDate;
            command.Parameters.Add("@ExpensesCategoryId", SqlDbType.Int).Value = expense.ExpensesCategoryId;
            command.Parameters.Add("@PaymentAccountId", SqlDbType.Int).Value = expense.PaymentAccountId;
            command.Parameters.Add("@Amount", SqlDbType.Decimal).Value = expense.Amount;
            command.Parameters.Add("@Description", SqlDbType.NVarChar, 500).Value = string.IsNullOrWhiteSpace(expense.Description)
                ? (object)DBNull.Value
                : expense.Description;
            command.Parameters.Add("@CreatedBy", SqlDbType.Int).Value = expense.CreatedBy;
            command.Parameters.Add("@LocationId", SqlDbType.Int).Value = expense.LocationId;
        }
        private Expenses MapExpense(IDataRecord record)
        {
            var expensesId = GetValue<int>(record, "Id");

            return new Expenses
            {
                ExpensesId = expensesId,
                VoucherNumber = HasRecordColumn(record, "VoucherNumber")
                    ? GetValue<string>(record, "VoucherNumber")
                    : expensesId.ToString(),
                ExpensesDate = GetValue<DateTime>(record, "ExpensesDate"),
                ExpensesCategoryId = GetValue<int>(record, "ExpensesCategoryId"),
                ExpensesCategoryName = GetValue<string>(record, "ExpensesCategory"),
                Amount = GetValue<decimal>(record, "Amount"),
                Description = GetValue<string>(record, "Description"),

            };
        }

        private static bool HasRecordColumn(IDataRecord record, string columnName)
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
