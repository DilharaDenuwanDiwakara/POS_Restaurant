using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Repositories.System;
using PointOfSale.Core.Models.System;

namespace PointOfSale.Infrastructure.Repositories.System
{
    public class BankBranchRepository : BaseRepository, IBankBranchRepository
    {
        public BankBranchRepository(DatabaseConnection databaseConnection) : base(databaseConnection) { }

        #region Public Methods

        public async Task<IEnumerable<BankBranch>> GetAllAsync()
        {
            var branches = new List<BankBranch>();

            try
            {
                using (var connection = GetConnection())
                using (var command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = @"
                        SELECT
                            Id,
                            BankId,
                            BranchCode,
                            BranchName,
                            IsActive
                        FROM [System].[BankBranch]
                        ORDER BY BranchName;";

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            branches.Add(MapBranch(reader));
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while retrieving bank branches.", ex);
            }

            return branches;
        }

        public async Task<IEnumerable<BankBranch>> GetByBankIdAsync(int bankId)
        {
            var branches = new List<BankBranch>();

            try
            {
                using (var connection = GetConnection())
                using (var command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = @"
                        SELECT
                            Id,
                            BankId,
                            BranchCode,
                            BranchName,
                            IsActive
                        FROM [System].[BankBranch]
                        WHERE BankId = @BankId
                        ORDER BY BranchName;";

                    command.Parameters.Add("@BankId", SqlDbType.Int).Value = bankId;

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            branches.Add(MapBranch(reader));
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occurred while retrieving branches for bank ID {bankId}.", ex);
            }

            return branches;
        }

        public async Task<int> CreateAsync(BankBranch branch)
        {
            try
            {
                using (var connection = GetConnection())
                using (var command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = @"
                        INSERT INTO [System].[BankBranch]
                        (
                            BankId,
                            BranchCode,
                            BranchName,
                            IsActive
                        )
                        VALUES
                        (
                            @BankId,
                            @BranchCode,
                            @BranchName,
                            @IsActive
                        );
                        SELECT CAST(SCOPE_IDENTITY() AS INT);";

                    AddBranchParameters(command, branch);

                    await connection.OpenAsync();
                    var result = await command.ExecuteScalarAsync();
                    return Convert.ToInt32(result);
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while creating the bank branch.", ex);
            }
        }

        public async Task UpdateAsync(BankBranch branch)
        {
            try
            {
                using (var connection = GetConnection())
                using (var command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = @"
                        UPDATE [System].[BankBranch]
                        SET
                            BankId = @BankId,
                            BranchCode = @BranchCode,
                            BranchName = @BranchName,
                            IsActive = @IsActive
                        WHERE Id = @Id;";

                    command.Parameters.Add("@Id", SqlDbType.Int).Value = branch.Id;
                    AddBranchParameters(command, branch);

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occurred while updating bank branch with ID {branch.Id}.", ex);
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
                    command.CommandText = "DELETE FROM [System].[BankBranch] WHERE Id = @Id;";

                    command.Parameters.Add("@Id", SqlDbType.Int).Value = id;

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occurred while deleting bank branch with ID {id}.", ex);
            }
        }

        #endregion

        #region Private Methods

        private void AddBranchParameters(SqlCommand command, BankBranch branch)
        {
            command.Parameters.Add("@BankId", SqlDbType.Int).Value = branch.BankId;
            command.Parameters.Add("@BranchCode", SqlDbType.NVarChar, 20).Value = (object)branch.BranchCode ?? DBNull.Value;
            command.Parameters.Add("@BranchName", SqlDbType.NVarChar, 100).Value = branch.BranchName ?? string.Empty;
            command.Parameters.Add("@IsActive", SqlDbType.Bit).Value = branch.IsActive;
        }

        private BankBranch MapBranch(IDataRecord record)
        {
            return new BankBranch
            {
                Id = GetValue<int>(record, "Id"),
                BankId = GetValue<int>(record, "BankId"),
                BranchCode = GetValue<string>(record, "BranchCode"),
                BranchName = GetValue<string>(record, "BranchName"),
                IsActive = GetValue<bool>(record, "IsActive")
            };
        }

        #endregion
    }
}
