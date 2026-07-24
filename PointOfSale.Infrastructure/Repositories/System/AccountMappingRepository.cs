using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Repositories.System;

namespace PointOfSale.Infrastructure.Repositories.System
{
    public class AccountMappingRepository : BaseRepository, IAccountMappingRepository
    {
        public AccountMappingRepository(DatabaseConnection databaseConnection) : base(databaseConnection) { }

        public async Task<Dictionary<string, int?>> GetSystemAccountMappingsAsync()
        {
            var mappings = new Dictionary<string, int?>();

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[System].[uspGetSystemAccountMappings]"))
                {
                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var mappingKey = GetValue<string>(reader, "MappingKey");
                            mappings[mappingKey] = GetValue<int?>(reader, "AccountId");
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while loading system account mappings.", ex);
            }

            return mappings;
        }

        public async Task UpdateSystemAccountMappingsAsync(
            int? cashAccountId,
            int? cardAccountId,
            int? arAccountId,
            int? cashVarianceAccountId,
            int? customerAdvanceAccountId,
            int? apAccountId,
            int? loyaltyPayableAccountId,
            int? loyaltyExpenseAccountId,
            int? freeIssueExpenseAccountId,
            int updatedBy)
        {
            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[System].[uspUpdateSystemAccountMappings]"))
                {
                    command.Parameters.Add("@CashAccountId", SqlDbType.Int).Value = (object)cashAccountId ?? DBNull.Value;
                    command.Parameters.Add("@CardAccountId", SqlDbType.Int).Value = (object)cardAccountId ?? DBNull.Value;
                    command.Parameters.Add("@ArAccountId", SqlDbType.Int).Value = (object)arAccountId ?? DBNull.Value;
                    command.Parameters.Add("@CashVarianceAccountId", SqlDbType.Int).Value = (object)cashVarianceAccountId ?? DBNull.Value;
                    command.Parameters.Add("@CustomerAdvanceAccountId", SqlDbType.Int).Value = (object)customerAdvanceAccountId ?? DBNull.Value;
                    command.Parameters.Add("@ApAccountId", SqlDbType.Int).Value = (object)apAccountId ?? DBNull.Value;
                    command.Parameters.Add("@LoyaltyPayableAccountId", SqlDbType.Int).Value = (object)loyaltyPayableAccountId ?? DBNull.Value;
                    command.Parameters.Add("@LoyaltyExpenseAccountId", SqlDbType.Int).Value = (object)loyaltyExpenseAccountId ?? DBNull.Value;
                    command.Parameters.Add("@FreeIssueAccountId", SqlDbType.Int).Value = (object)freeIssueExpenseAccountId ?? DBNull.Value;
                    command.Parameters.Add("@UpdatedBy", SqlDbType.Int).Value = updatedBy;

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while updating system account mappings.", ex);
            }
        }
    }
}
