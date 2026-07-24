using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Repositories.Sales;
using PointOfSale.Core.Models.Sales;

namespace PointOfSale.Infrastructure.Repositories.Sales
{
    public class CashInOutRepository : BaseRepository, ICashInOutRepository
    {
        public CashInOutRepository(DatabaseConnection dbConnection) : base(dbConnection) { }

        #region Public Methods
        public async Task<long> CreateAsync(CashInOut cashInOut)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Sales].[uspInsertCashInOut]"))
                    {
                        AddCashInOutParameters(command, cashInOut);

                        var transactionId = command.Parameters.Add("@TransactionId", SqlDbType.BigInt);
                        transactionId.Direction = ParameterDirection.Output;

                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();

                        return (long)transactionId.Value;
                    }
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2627 || ex.Number == 2601)
                {
                    throw new InvalidOperationException("Duplicate transaction detected.", ex);
                }
                throw new InvalidOperationException("A database error occurred while recording the cash transaction.", ex);
            }
        }
        public async Task<IEnumerable<CashInOut>> GetAllAsync(int locationId)
        {
            var transactions = new List<CashInOut>();
            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Sales].[uspGetAllCashInOut]"))
                    {
                        command.Parameters.AddWithValue("@LocationId", locationId);

                        await connection.OpenAsync();

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                transactions.Add(MapCashInOut(reader));
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while retrieving transactions.", ex);
            }
            return transactions;
        }
        #endregion

        #region Private Methods
        private void AddCashInOutParameters(SqlCommand command, CashInOut cashInOut)
        {
            command.Parameters.Add("@TransactionType", SqlDbType.NVarChar, 50).Value = cashInOut.TransactionType;
            command.Parameters.Add("@Amount", SqlDbType.Decimal).Value = cashInOut.Amount;
            command.Parameters.Add("@Reason", SqlDbType.NVarChar, 255).Value = (object)cashInOut.Reason ?? DBNull.Value;
            command.Parameters.Add("@CreatedBy", SqlDbType.Int).Value = cashInOut.CreatedBy;
            command.Parameters.Add("@LocationId", SqlDbType.Int).Value = cashInOut.LocationId;
            command.Parameters.Add("@ShiftId", SqlDbType.BigInt).Value = cashInOut.ShiftId;
        }
        private CashInOut MapCashInOut(IDataRecord record)
        {
            return new CashInOut
            {
                // Note: Using long for BigInteger mapping in ADO.NET
                TransactionId = GetValue<long>(record, "Id"),
                TransactionType = GetValue<string>(record, "TransactionType"),
                Amount = GetValue<decimal>(record, "Amount"),
                Reason = GetValue<string>(record, "Reason"),
                CreatedBy = GetValue<int>(record, "CreatedBy"),
                TransactionDate = GetValue<DateTime>(record, "TransactionDate"),
                LocationId = GetValue<int>(record, "BranchId")
            };
        }
        #endregion
    }
}
