using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces;

namespace PointOfSale.Infrastructure.Repositories
{
    public class AccountingRepository : BaseRepository, IAccountingRepository
    {
        public AccountingRepository(DatabaseConnection databaseConnection) : base(databaseConnection) { }

        #region Public Methods
        public async Task<IEnumerable<AccountDto>> GetAccountsAsync()
        {
            var accounts = new List<AccountDto>();
            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Accounts].[uspGetAllAccounts]"))
                {
                    await connection.OpenAsync();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            accounts.Add(MapAccountDto(reader));
                        }
                    }
                }
                return accounts;
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while retrieving chart of accounts.", ex);
            }
        }

        public async Task<int> CreateAccountAsync(AccountDto account)
        {
            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Accounts].[uspInsertAccount]"))
                {
                    AddInsertAccountParameters(command, account);
                    var outputParam = command.Parameters.Add("@Id", SqlDbType.Int);
                    outputParam.Direction = ParameterDirection.Output;

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                    return (int)outputParam.Value;
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while creating the account.", ex);
            }
        }

        public async Task UpdateAccountAsync(AccountDto account)
        {
            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Accounts].[uspUpdateAccount]"))
                {
                    command.Parameters.Add("@Id", SqlDbType.Int).Value = account.Id;
                    AddInsertAccountParameters(command, account);
                    command.Parameters.Add("@UpdatedBy", SqlDbType.Int).Value = account.UpdatedBy.HasValue
                        ? (object)account.UpdatedBy.Value
                        : DBNull.Value;

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while updating the account.", ex);
            }
        }

        public async Task DeactivateAccountAsync(int accountId)
        {
            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Accounts].[uspDeactivateAccount]"))
                {
                    command.Parameters.AddWithValue("@Id", accountId);
                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while deactivating the account.", ex);
            }
        }

        public async Task<int> CreateAccountTypeAsync(AccountType accountType)
        {
            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Accounts].[uspInsertAccountType]"))
                {
                    AddAccountTypeParameters(command, accountType);
                    var outputParam = command.Parameters.Add("@AccountTypeId", SqlDbType.Int);
                    outputParam.Direction = ParameterDirection.Output;

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                    return (int)outputParam.Value;
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2627 || ex.Number == 2601 || ex.Number == 50000) // Unique constraint error number
                {
                    throw new InvalidOperationException(ex.Message, ex);
                }

                throw new InvalidOperationException("A database error occurred while creating the account type.", ex);
            }
        }

        public async Task UpdateAccountTypeAsync(AccountType accountType)
        {
            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Accounts].[uspUpdateAccountType]"))
                {
                    command.Parameters.Add("@AccountTypeId", SqlDbType.Int).Value = accountType.AccountTypeId;
                    AddAccountTypeParameters(command, accountType);

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while updating the account type.", ex);
            }
        }

        public async Task DeactivateAccountTypeAsync(int accountTypeId)
        {
            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Accounts].[uspDeactivateAccountType]"))
                {
                    command.Parameters.AddWithValue("@AccountTypeId", accountTypeId);
                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while deactivating the account type.", ex);
            }
        }

        public async Task<bool> AccountCodeExistsAsync(string code, int? excludeAccountId = null)
        {
            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Accounts].[uspAccountCodeExists]"))
                {
                    command.Parameters.Add("@Code", SqlDbType.VarChar, 50).Value = code;
                    command.Parameters.Add("@ExcludeAccountId", SqlDbType.Int).Value =
                        excludeAccountId.HasValue ? (object)excludeAccountId.Value : DBNull.Value;

                    await connection.OpenAsync();
                    var result = await command.ExecuteScalarAsync();
                    return Convert.ToInt32(result) > 0;
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while checking if the account code exists.", ex);
            }
        }

        public async Task<IEnumerable<AccountType>> GetAccountTypesAsync()
        {
            var types = new List<AccountType>();
            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Accounts].[uspGetAllAccountTypes]"))
                {
                    await connection.OpenAsync();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            types.Add(MapAccountType(reader));
                        }
                    }
                }
                return types;
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while retrieving account types.", ex);
            }
        }

        #endregion

        #region Private Helper Methods

        private void AddInsertAccountParameters(SqlCommand command, AccountDto account)
        {
            command.Parameters.Add("@AccountTypeId", SqlDbType.Int).Value = account.AccountTypeId;
            command.Parameters.Add("@ParentAccountId", SqlDbType.Int).Value = (object)account.ParentAccountId ?? DBNull.Value;
            command.Parameters.Add("@Code", SqlDbType.VarChar, 50).Value = account.Code;
            command.Parameters.Add("@Name", SqlDbType.VarChar, 200).Value = account.Name;
            command.Parameters.Add("@Description", SqlDbType.NVarChar, 500).Value =
                string.IsNullOrWhiteSpace(account.Description) ? (object)DBNull.Value : account.Description.Trim();
            command.Parameters.Add("@IsActive", SqlDbType.Bit).Value = account.IsActive;
            command.Parameters.Add("@IsHeader", SqlDbType.Bit).Value = account.IsHeader;
            command.Parameters.Add("@CreatedBy", SqlDbType.Int).Value = account.CreatedBy;
        }

        private void AddAccountTypeParameters(SqlCommand command, AccountType type)
        {
            command.Parameters.Add("@Code", SqlDbType.VarChar, 10).Value = type.Code;
            command.Parameters.Add("@Name", SqlDbType.VarChar, 100).Value = type.Name;
            command.Parameters.Add("@NormalBalance", SqlDbType.Char, 1).Value = type.NormalBalance;
            command.Parameters.Add("@IsActive", SqlDbType.Bit).Value = type.IsActive;
        }

        private AccountDto MapAccountDto(IDataRecord record)
        {
            return new AccountDto
            {
                Id = GetValue<int>(record, "Id"),
                ParentAccountId = GetValue<int?>(record, "ParentAccountId"),
                Code = GetValue<string>(record, "Code"),
                Name = GetValue<string>(record, "Name"),
                Description = GetValue<string>(record, "Description"),
                AccountTypeId = GetValue<int>(record, "AccountTypeId"),
                IsActive = GetValue<bool>(record, "IsActive"),
                IsHeader = GetValue<bool>(record, "IsHeader"),
                CreatedBy = GetValue<int>(record, "CreatedBy"),
                CreatedAt = GetValue<DateTime>(record, "CreatedAt"),
                UpdatedBy = GetValue<int?>(record, "UpdatedBy"),
                UpdatedAt = GetValue<DateTime?>(record, "UpdatedAt")
            };
        }

        private AccountType MapAccountType(IDataRecord record)
        {
            return new AccountType
            {
                AccountTypeId = GetValue<int>(record, "Id"),
                Code = GetValue<string>(record, "Code"),
                Name = GetValue<string>(record, "Name"),
                NormalBalance = GetValue<string>(record, "NormalBalance"),
                IsActive = GetValue<bool>(record, "IsActive")
            };
        }

        #endregion
    }
}
