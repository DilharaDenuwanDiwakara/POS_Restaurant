using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Repositories.Accounts;
using PointOfSale.Core.Models.Accounts.Entities;

namespace PointOfSale.Infrastructure.Repositories.Accounts
{
    public class FundTransferRepository : BaseRepository, IFundTransferRepository
    {
        public FundTransferRepository(DatabaseConnection databaseConnection) : base(databaseConnection) { }

        #region Public Method
        public async Task<long> CreateAsync(FundTransfer fundTransfer)
        {
            if (fundTransfer == null)
                throw new ArgumentNullException(nameof(fundTransfer));

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Accounts].[uspPostFundTransfer]"))
                {
                    AddFundTransferParameters(command, fundTransfer);

                    var idParam = command.Parameters.Add("@FundTransferId", SqlDbType.BigInt);
                    idParam.Direction = ParameterDirection.Output;

                    var numberParam = command.Parameters.Add("@TransferNumber", SqlDbType.NVarChar, 30);
                    numberParam.Direction = ParameterDirection.Output;

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();

                    fundTransfer.TransferNumber = numberParam.Value as string;
                    return (long)idParam.Value;
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while posting the fund transfer.", ex);
            }
        }

        public async Task<IEnumerable<FundTransfer>> GetAllAsync(int branchId, DateTime fromDate, DateTime toDate)
        {
            var transfers = new List<FundTransfer>();

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Accounts].[uspGetAllFundTransfers]"))
                {
                    command.Parameters.Add("@BranchId", SqlDbType.Int).Value = branchId;
                    command.Parameters.Add("@FromDate", SqlDbType.Date).Value = fromDate.Date;
                    command.Parameters.Add("@ToDate", SqlDbType.Date).Value = toDate.Date;

                    await connection.OpenAsync();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            transfers.Add(MapFundTransfer(reader));
                        }
                    }
                }

                return transfers;
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while retrieving fund transfers.", ex);
            }
        }

        public async Task<DataTable> GetFundTransferVoucherAsync(long fundTransferId)
        {
            var dataTable = new DataTable();

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Accounts].[uspGetFundTransferVoucher]"))
                {
                    command.Parameters.Add("@FundTransferId", SqlDbType.BigInt).Value = fundTransferId;

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
                    $"A database error occurred while retrieving Fund Transfer Voucher {fundTransferId}.",
                    ex);
            }
        }
        #endregion

        #region Private Helper Method
        private void AddFundTransferParameters(SqlCommand command, FundTransfer fundTransfer)
        {
            command.Parameters.Add("@TransferDate", SqlDbType.Date).Value = fundTransfer.TransferDate.Date;
            command.Parameters.Add("@BranchId", SqlDbType.Int).Value = fundTransfer.BranchId;
            command.Parameters.Add("@SourceAccountId", SqlDbType.Int).Value = fundTransfer.SourceAccountId;
            command.Parameters.Add("@DestinationAccountId", SqlDbType.Int).Value = fundTransfer.DestinationAccountId;
            command.Parameters.Add("@Amount", SqlDbType.Decimal).Value = fundTransfer.Amount;
            command.Parameters.Add("@ReferenceNo", SqlDbType.VarChar, 100).Value = string.IsNullOrWhiteSpace(fundTransfer.ReferenceNo)
                ? (object)DBNull.Value
                : fundTransfer.ReferenceNo.Trim();
            command.Parameters.Add("@Description", SqlDbType.NVarChar, 500).Value = string.IsNullOrWhiteSpace(fundTransfer.Description)
                ? (object)DBNull.Value
                : fundTransfer.Description.Trim();
            command.Parameters.Add("@CreatedBy", SqlDbType.Int).Value = fundTransfer.CreatedBy;
        }

        private FundTransfer MapFundTransfer(IDataRecord record)
        {
            return new FundTransfer
            {
                FundTransferId = GetValue<long>(record, "Id"),
                TransferNumber = GetValue<string>(record, "TransferNumber"),
                TransferDate = GetValue<DateTime>(record, "TransferDate"),
                SourceAccountId = GetValue<int>(record, "SourceAccountId"),
                SourceAccountName = GetValue<string>(record, "SourceAccountName"),
                DestinationAccountId = GetValue<int>(record, "DestinationAccountId"),
                DestinationAccountName = GetValue<string>(record, "DestinationAccountName"),
                Amount = GetValue<decimal>(record, "Amount"),
                ReferenceNo = GetValue<string>(record, "ReferenceNo"),
                Description = GetValue<string>(record, "Description"),
                CreatedBy = GetValue<int>(record, "CreatedBy"),
                CreatedAt = GetValue<DateTime>(record, "CreatedAt")
            };
        }
        #endregion
    }
}
