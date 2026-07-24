using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Repositories.Accounts;
using PointOfSale.Core.Models.Accounts.Entities;

namespace PointOfSale.Infrastructure.Repositories.Accounts
{
    public class OpeningBalanceRepository : BaseRepository, IOpeningBalanceRepository
    {
        public OpeningBalanceRepository(DatabaseConnection databaseConnection) : base(databaseConnection) { }

        public async Task<bool> OpeningBalanceExistsAsync(int locationId)
        {
            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Accounts].[uspOpeningBalanceExists]"))
                {
                    command.Parameters.Add("@LocationId", SqlDbType.Int).Value = locationId;

                    await connection.OpenAsync();
                    var result = await command.ExecuteScalarAsync();
                    return Convert.ToInt32(result) > 0;
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while checking for an existing opening balance.", ex);
            }
        }

        public async Task<long> PostOpeningBalanceAsync(
            int locationId,
            DateTime asOfDate,
            int equityAccountId,
            int createdBy,
            IEnumerable<OpeningBalanceLine> lines)
        {
            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Accounts].[uspPostOpeningBalance]"))
                {
                    command.Parameters.Add("@LocationId", SqlDbType.Int).Value = locationId;
                    command.Parameters.Add("@AsOfDate", SqlDbType.Date).Value = asOfDate.Date;
                    command.Parameters.Add("@EquityAccountId", SqlDbType.Int).Value = equityAccountId;
                    command.Parameters.Add("@CreatedBy", SqlDbType.Int).Value = createdBy;

                    var linesParam = command.Parameters.Add("@Lines", SqlDbType.Structured);
                    linesParam.TypeName = "[Accounts].[OpeningBalanceLineType]";
                    linesParam.Value = BuildLinesTable(lines);

                    var outputParam = command.Parameters.Add("@OpeningBalanceHeaderId", SqlDbType.BigInt);
                    outputParam.Direction = ParameterDirection.Output;

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();

                    return (long)outputParam.Value;
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while posting the opening balance.", ex);
            }
        }

        private static DataTable BuildLinesTable(IEnumerable<OpeningBalanceLine> lines)
        {
            var table = new DataTable();
            table.Columns.Add("AccountId", typeof(int));
            table.Columns.Add("DebitAmount", typeof(decimal));
            table.Columns.Add("CreditAmount", typeof(decimal));

            foreach (var line in lines)
            {
                table.Rows.Add(line.AccountId, line.DebitAmount, line.CreditAmount);
            }

            return table;
        }
    }
}
