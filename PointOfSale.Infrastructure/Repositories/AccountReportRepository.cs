using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Exceptions;
using PointOfSale.Core.Interfaces;

namespace PointOfSale.Infrastructure.Repositories
{
    public sealed class AccountReportRepository : BaseRepository, IAccountReportRepository
    {
        public AccountReportRepository(DatabaseConnection databaseConnection)
            : base(databaseConnection ?? throw new ArgumentNullException(nameof(databaseConnection)))
        {
        }

        public async Task<AccountInquiryResponseDto> GetAccountInquiryAsync(
            int accountId, DateTime fromDate, DateTime toDate)
        {
            if (accountId <= 0)
                throw new ArgumentOutOfRangeException(nameof(accountId), "Account ID must be greater than zero.");
            if (fromDate.Date > toDate.Date)
                throw new ArgumentException("From date must be on or before to date.", nameof(fromDate));

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Accounts].[uspGetAccountInquiry]"))
                {
                    command.CommandTimeout = 120;
                    command.Parameters.Add("@AccountId", SqlDbType.Int).Value = accountId;
                    command.Parameters.Add("@FromDate", SqlDbType.Date).Value = fromDate.Date;
                    command.Parameters.Add("@ToDate", SqlDbType.Date).Value = toDate.Date;

                    await connection.OpenAsync().ConfigureAwait(false);
                    using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                    {
                        if (!await reader.ReadAsync().ConfigureAwait(false))
                            throw new DataException("Account inquiry did not return the required summary row.");

                        var response = new AccountInquiryResponseDto
                        {
                            Summary = new AccountInquirySummaryDto
                            {
                                AccountCode = ReadText(reader, "AccountCode"),
                                AccountName = ReadText(reader, "AccountName"),
                                NormalBalanceType = ReadText(reader, "NormalBalanceType"),
                                OpeningBalance = reader.GetDecimal(reader.GetOrdinal("OpeningBalance")),
                                TotalDebit = reader.GetDecimal(reader.GetOrdinal("TotalDebit")),
                                TotalCredit = reader.GetDecimal(reader.GetOrdinal("TotalCredit")),
                                ClosingBalance = reader.GetDecimal(reader.GetOrdinal("ClosingBalance"))
                            }
                        };

                        if (await reader.ReadAsync().ConfigureAwait(false))
                            throw new DataException("Account inquiry returned more than one summary row.");
                        if (!await reader.NextResultAsync().ConfigureAwait(false))
                            throw new DataException("Account inquiry did not return the transaction result set.");

                        // Resolve ordinals even for an empty result to validate the procedure contract.
                        var date = reader.GetOrdinal("TransactionDate");
                        var document = reader.GetOrdinal("DocumentNo");
                        var source = reader.GetOrdinal("SourceType");
                        var reference = reader.GetOrdinal("Reference");
                        var description = reader.GetOrdinal("Description");
                        var debit = reader.GetOrdinal("Debit");
                        var credit = reader.GetOrdinal("Credit");
                        var balance = reader.GetOrdinal("RunningBalance");

                        // The procedure supplies chronological order, including its same-day tie-breaker.
                        while (await reader.ReadAsync().ConfigureAwait(false))
                        {
                            response.TransactionLines.Add(new AccountInquiryLineDto
                            {
                                TransactionDate = reader.GetDateTime(date),
                                DocumentNo = ReadText(reader, document),
                                SourceType = ReadText(reader, source),
                                Reference = ReadText(reader, reference),
                                Description = ReadText(reader, description),
                                Debit = reader.GetDecimal(debit),
                                Credit = reader.GetDecimal(credit),
                                RunningBalance = reader.GetDecimal(balance)
                            });
                        }

                        if (await reader.NextResultAsync().ConfigureAwait(false))
                            throw new DataException("Account inquiry returned an unexpected result set.");

                        return response;
                    }
                }
            }
            catch (SqlException ex) when (IsAccountNotFound(ex))
            {
                // Keep SQL-specific exception handling out of the presentation layer.
                throw new AccountNotFoundException(accountId, ex);
            }
        }

        private static bool IsAccountNotFound(SqlException exception)
        {
            foreach (SqlError error in exception.Errors)
            {
                if (error.Number == 51000)
                    return true;
            }
            return false;
        }

        private static string ReadText(SqlDataReader reader, string columnName)
            => ReadText(reader, reader.GetOrdinal(columnName));

        private static string ReadText(SqlDataReader reader, int ordinal)
            => reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
    }
}
