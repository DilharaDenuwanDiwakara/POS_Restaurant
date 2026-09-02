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
        public async Task<int> CreateAsync(ExpenseHeader expense)
        {
            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Accounts].[uspInsertExpensesMasterDetail]"))
                {
                    AddExpenseParameters(command, expense);

                    var outputParam = command.Parameters.Add("@NewExpenseHeaderId", SqlDbType.Int);
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
        public async Task<IEnumerable<Expenses>> GetAllAsync(int branchId)
        {
            return await GetAllAsync(branchId, DateTime.Today, DateTime.Today);
        }

        public async Task<IEnumerable<Expenses>> GetAllAsync(int branchId, DateTime fromDate, DateTime toDate)
        {
            var expensesList = new List<Expenses>();

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Accounts].[uspGetAllExpenses]"))
                {
                    command.Parameters.Add("@BranchId", SqlDbType.Int).Value = branchId;
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
            var dataTable = new DataTable("uspGetExpenseVoucher");

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
        private void AddExpenseParameters(SqlCommand command, ExpenseHeader expense)
        {
            command.Parameters.Add("@PaymentAccountId", SqlDbType.Int).Value = expense.PaymentAccountId;
            command.Parameters.Add("@ExpenseDate", SqlDbType.DateTime).Value = expense.ExpensesDate;
            command.Parameters.Add("@HeaderDescription", SqlDbType.NVarChar, 500).Value = string.IsNullOrWhiteSpace(expense.Description)
                ? (object)DBNull.Value
                : expense.Description;
            command.Parameters.Add("@CreatedBy", SqlDbType.Int).Value = expense.CreatedBy;
            command.Parameters.Add("@BranchId", SqlDbType.Int).Value = expense.BranchId;

            var linesParameter = command.Parameters.Add("@ExpenseLines", SqlDbType.Structured);
            linesParameter.TypeName = "[Accounts].[ExpenseLineType]";
            linesParameter.Value = CreateExpenseLineDataTable(expense.Lines);
        }

        private static DataTable CreateExpenseLineDataTable(IEnumerable<ExpenseLine> lines)
        {
            var table = new DataTable();
            table.Columns.Add("AccountId", typeof(int));
            table.Columns.Add("Amount", typeof(decimal));
            table.Columns.Add("Description", typeof(string));

            if (lines == null)
            {
                return table;
            }

            foreach (var line in lines)
            {
                var row = table.NewRow();
                row["AccountId"] = line.AccountId;
                row["Amount"] = line.Amount;
                row["Description"] = string.IsNullOrWhiteSpace(line.Description)
                    ? (object)DBNull.Value
                    : line.Description;
                table.Rows.Add(row);
            }

            return table;
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
                BranchId = HasRecordColumn(record, "BranchId") ? GetValue<int>(record, "BranchId") : 0,
                ExpensesCategoryId = HasRecordColumn(record, "ExpensesCategoryId") ? GetValue<int>(record, "ExpensesCategoryId") : 0,
                ExpensesCategoryName = HasRecordColumn(record, "ExpensesCategory")
                    ? GetValue<string>(record, "ExpensesCategory")
                    : HasRecordColumn(record, "ExpenseAccount")
                        ? GetValue<string>(record, "ExpenseAccount")
                        : string.Empty,
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
