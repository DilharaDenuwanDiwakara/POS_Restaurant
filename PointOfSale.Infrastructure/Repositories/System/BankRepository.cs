using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Repositories.System;
using PointOfSale.Core.Models.System;

namespace PointOfSale.Infrastructure.Repositories.System
{
    public class BankRepository : BaseRepository, IBankRepository
    {
        public BankRepository(DatabaseConnection databaseConnection) : base(databaseConnection) { }

        #region Public Methods

        public async Task<IEnumerable<Bank>> GetAllAsync()
        {
            var banks = new List<Bank>();

            try
            {
                using (var connection = GetConnection())
                using (var command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = @"
                        SELECT
                            Id,
                            BankCode,
                            BankName,
                            IsActive
                        FROM [System].[Bank]
                        ORDER BY BankName;";

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            banks.Add(MapBank(reader));
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while retrieving the banks.", ex);
            }

            return banks;
        }

        public async Task<int> CreateAsync(Bank bank)
        {
            try
            {
                using (var connection = GetConnection())
                using (var command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = @"
                        INSERT INTO [System].[Bank]
                        (
                            BankCode,
                            BankName,
                            IsActive
                        )
                        VALUES
                        (
                            @BankCode,
                            @BankName,
                            @IsActive
                        );
                        SELECT CAST(SCOPE_IDENTITY() AS INT);";

                    AddBankParameters(command, bank);

                    await connection.OpenAsync();
                    var result = await command.ExecuteScalarAsync();
                    return Convert.ToInt32(result);
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2627 || ex.Number == 2601 || ex.Number == 50000)
                    throw new InvalidOperationException(ex.Message, ex);

                throw new InvalidOperationException("A database error occurred while creating the bank.", ex);
            }
        }

        public async Task UpdateAsync(Bank bank)
        {
            try
            {
                using (var connection = GetConnection())
                using (var command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = @"
                        UPDATE [System].[Bank]
                        SET
                            BankCode = @BankCode,
                            BankName = @BankName,
                            IsActive = @IsActive
                        WHERE Id = @Id;";

                    command.Parameters.Add("@Id", SqlDbType.Int).Value = bank.Id;
                    AddBankParameters(command, bank);

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occurred while updating bank with ID {bank.Id}.", ex);
            }
        }

        public async Task DeleteAsync(int id)
        {
            try
            {
                using (var connection = GetConnection())
                using (var command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = "DELETE FROM [System].[Bank] WHERE Id = @Id;";

                    command.Parameters.Add("@Id", SqlDbType.Int).Value = id;

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occurred while deleting bank with ID {id}.", ex);
            }
        }

        #endregion

        #region Private Methods

        private void AddBankParameters(SqlCommand command, Bank bank)
        {
            command.Parameters.Add("@BankCode", SqlDbType.NVarChar, 20).Value = (object)bank.BankCode ?? DBNull.Value;
            command.Parameters.Add("@BankName", SqlDbType.NVarChar, 100).Value = bank.BankName ?? string.Empty;
            command.Parameters.Add("@IsActive", SqlDbType.Bit).Value = bank.IsActive;
        }

        private Bank MapBank(IDataRecord record)
        {
            return new Bank
            {
                Id = GetValue<int>(record, "Id"),
                BankCode = GetValue<string>(record, "BankCode"),
                BankName = GetValue<string>(record, "BankName"),
                IsActive = GetValue<bool>(record, "IsActive")
            };
        }

        #endregion
    }
}
