using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Repositories.Sales;
using PointOfSale.Core.Models.Sales;

namespace PointOfSale.Infrastructure.Repositories.Sales
{
    public class PaymentTerminalRepository : BaseRepository, IPaymentTerminalRepository
    {
        public PaymentTerminalRepository(DatabaseConnection databaseConnection) : base(databaseConnection) { }

        #region Public Methods

        public async Task<IEnumerable<PaymentTerminal>> GetAllAsync()
        {
            var terminals = new List<PaymentTerminal>();

            try
            {
                using (var connection = GetConnection())
                using (var command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = @"
                        SELECT
                            pt.Id,
                            pt.BranchId,
                            pt.LinkedAccountId,
                            pt.ProviderBankId,
                            pt.TerminalName,
                            pt.TerminalId,
                            pt.IsActive,
                            b.Name                                                  AS BranchName,
                            a.Code + ' - ' + a.Name                                 AS LinkedAccountDisplay,
                            bk.BankName                                             AS ProviderBankName
                        FROM  [Sales].[PaymentTerminal]  pt
                        INNER JOIN [System].[Branch]      b   ON b.Id            = pt.BranchId
                        LEFT  JOIN [Accounts].[Account]   a   ON a.Id            = pt.LinkedAccountId
                        LEFT  JOIN [System].[Bank]        bk  ON bk.Id           = pt.ProviderBankId
                        ORDER BY pt.TerminalName;";

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            terminals.Add(MapTerminal(reader));
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while retrieving payment terminals.", ex);
            }

            return terminals;
        }

        public async Task<int> CreateAsync(PaymentTerminal terminal)
        {
            try
            {
                using (var connection = GetConnection())
                using (var command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = @"
                        INSERT INTO [Sales].[PaymentTerminal]
                        (
                            BranchId,
                            LinkedAccountId,
                            ProviderBankId,
                            TerminalName,
                            TerminalId,
                            IsActive,
                            CreatedBy
                        )
                        VALUES
                        (
                            @BranchId,
                            @LinkedAccountId,
                            @ProviderBankId,
                            @TerminalName,
                            @TerminalId,
                            @IsActive,
                            @CreatedBy
                        );
                        SELECT CAST(SCOPE_IDENTITY() AS INT);";

                    AddTerminalParameters(command, terminal);

                    await connection.OpenAsync();
                    var result = await command.ExecuteScalarAsync();
                    return Convert.ToInt32(result);
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2627 || ex.Number == 2601 || ex.Number == 50000)
                    throw new InvalidOperationException(ex.Message, ex);

                throw new InvalidOperationException($"A database error occurred while creating the payment terminal: {ex.Message}", ex);
            }
        }

        public async Task UpdateAsync(PaymentTerminal terminal)
        {
            try
            {
                using (var connection = GetConnection())
                using (var command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = @"
                        UPDATE [Sales].[PaymentTerminal]
                        SET
                            BranchId        = @BranchId,
                            LinkedAccountId = @LinkedAccountId,
                            ProviderBankId  = @ProviderBankId,
                            TerminalName    = @TerminalName,
                            TerminalId      = @TerminalId,
                            IsActive        = @IsActive,
                            UpdatedBy       = @UpdatedBy,
                            UpdatedAt       = GETDATE()
                        WHERE Id = @Id;";

                    command.Parameters.Add("@Id", SqlDbType.Int).Value = terminal.Id;
                    AddTerminalParameters(command, terminal);

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occurred while updating payment terminal with ID {terminal.Id}: {ex.Message}", ex);
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
                    command.CommandText = "DELETE FROM [Sales].[PaymentTerminal] WHERE Id = @Id;";

                    command.Parameters.Add("@Id", SqlDbType.Int).Value = id;

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occurred while deleting payment terminal with ID {id}.", ex);
            }
        }

        #endregion

        #region Private Methods

        private void AddTerminalParameters(SqlCommand command, PaymentTerminal terminal)
        {
            command.Parameters.Add("@BranchId", SqlDbType.Int).Value = terminal.BranchId;
            command.Parameters.Add("@LinkedAccountId", SqlDbType.Int).Value = (object)terminal.LinkedAccountId ?? DBNull.Value;
            command.Parameters.Add("@ProviderBankId", SqlDbType.Int).Value = (object)terminal.ProviderBankId ?? DBNull.Value;
            command.Parameters.Add("@TerminalName", SqlDbType.NVarChar, 100).Value = terminal.TerminalName ?? string.Empty;
            command.Parameters.Add("@TerminalId", SqlDbType.VarChar, 50).Value = terminal.TerminalId ?? string.Empty;
            command.Parameters.Add("@IsActive", SqlDbType.Bit).Value = terminal.IsActive;
            command.Parameters.Add("@CreatedBy", SqlDbType.Int).Value = terminal.CreatedBy;
            command.Parameters.Add("@UpdatedBy", SqlDbType.Int).Value = (object)terminal.UpdatedBy ?? DBNull.Value;
        }

        private PaymentTerminal MapTerminal(IDataRecord record)
        {
            return new PaymentTerminal
            {
                Id = GetValue<int>(record, "Id"),
                BranchId = GetValue<int>(record, "BranchId"),
                LinkedAccountId = GetValue<int?>(record, "LinkedAccountId"),
                ProviderBankId = GetValue<int?>(record, "ProviderBankId"),
                TerminalName = GetValue<string>(record, "TerminalName"),
                TerminalId = GetValue<string>(record, "TerminalId"),
                IsActive = GetValue<bool>(record, "IsActive"),
                BranchName = GetValue<string>(record, "BranchName"),
                LinkedAccountDisplay = GetValue<string>(record, "LinkedAccountDisplay"),
                ProviderBankName = GetValue<string>(record, "ProviderBankName")
            };
        }

        #endregion
    }
}
