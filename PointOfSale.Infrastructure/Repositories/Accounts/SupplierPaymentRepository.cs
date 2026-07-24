using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Repositories.Accounts;
using PointOfSale.Core.Models.Accounts;
using PointOfSale.Core.Models.Accounts.Entities;

namespace PointOfSale.Infrastructure.Repositories.Accounts
{
    public class SupplierPaymentRepository : BaseRepository, ISupplierPaymentRepository
    {
        public SupplierPaymentRepository(DatabaseConnection dbConnection) : base(dbConnection) { }

        #region Public Methods
        public async Task<IEnumerable<SupplierPayable>> GetSupplierPayableAsync(int supplierId)
        {
            var payables = new List<SupplierPayable>();

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Accounts].[uspGetSupplierPayables]"))
                {
                    command.Parameters.Add("@SupplierId", SqlDbType.Int).Value = supplierId;

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            payables.Add(MapSupplierPayable(reader));
                        }
                    }
                }
                return payables;
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occured while retrieving supplier payables.", ex);
            }
        }
        public async Task<long> CreateAsync(SupplierPayment supplierPayment)
        {
            using (var connection = GetConnection())
            using (var command = CreateCommand(connection, "[Accounts].[uspInsertSupplierPayment]"))
            {

                AddPaymentParameters(command, supplierPayment);

                // Tvp for Payment Lines
                var dt = new DataTable();
                dt.Columns.Add("SupplierPayableId", typeof(long));
                dt.Columns.Add("AmountApplied", typeof(decimal));

                foreach (var line in supplierPayment.PaymentLines)
                    dt.Rows.Add(line.SupplierPayableId, line.AmountApplied);

                var tvpParam = command.Parameters.Add("@Lines", SqlDbType.Structured);
                tvpParam.TypeName = "[Accounts].[SupplierPaymentLineType]";
                tvpParam.Value = dt;

                var outputParam = command.Parameters.Add("@SupplierPaymentId", SqlDbType.BigInt);
                outputParam.Direction = ParameterDirection.Output;

                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();

                return (long)outputParam.Value;
            }
        }

        public async Task<DataSet> GetSupplierPaymentVoucherDataSetAsync(long supplierPaymentId)
        {
            var dataSet = new DataSet("SupplierPaymentVoucherDS");

            using (var connection = GetConnection())
            using (var command = CreateCommand(connection, "[Accounts].[uspGetSupplierPaymentVoucher]"))
            {
                command.Parameters.Add("@PaymentId", SqlDbType.BigInt).Value = supplierPaymentId;

                using (var adapter = new SqlDataAdapter(command))
                {
                    await connection.OpenAsync();
                    adapter.Fill(dataSet);
                }
            }

            RenameSupplierPaymentVoucherTables(dataSet);
            return dataSet;
        }

        public async Task ProcessBulkPaymentAsync(int supplierId, string paymentMethod, DateTime paymentDate, decimal totalCash, int userId,
                                          List<SupplierSettlement> settlements,
                                          List<SupplierPaymentLine> paymentLines)
        {
            using (var conn = GetConnection())
            using (var cmd = new SqlCommand("[Accounts].[uspInsertSupplierSettlement]", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("@SupplierId", supplierId);
                cmd.Parameters.AddWithValue("@PaymentMethod", (object)paymentMethod ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@PaymentDate", paymentDate);
                cmd.Parameters.AddWithValue("@TotalCashPaid", totalCash);
                cmd.Parameters.AddWithValue("@UserId", userId);

                // 1. Convert Settlements to DataTable for TVP
                var dtSettlements = new DataTable();
                dtSettlements.Columns.Add("PayableId", typeof(long));
                dtSettlements.Columns.Add("CreditId", typeof(int));
                dtSettlements.Columns.Add("Amount", typeof(decimal));

                foreach (var item in settlements)
                {
                    // Map 'SettlementAmount' to the 'Amount' column
                    dtSettlements.Rows.Add(item.PayableId, item.CreditId, item.SettlementAmount);
                }

                var pSettlements = cmd.Parameters.AddWithValue("@Settlements", dtSettlements);
                pSettlements.SqlDbType = SqlDbType.Structured;
                pSettlements.TypeName = "[Accounts].[SupplierSettlementType]";

                // 2. Convert PaymentLines to DataTable for TVP
                var dtLines = new DataTable();
                dtLines.Columns.Add("SupplierPayableId", typeof(long));
                dtLines.Columns.Add("AmountApplied", typeof(decimal));

                foreach (var item in paymentLines)
                    dtLines.Rows.Add(item.SupplierPayableId, item.AmountApplied);

                var pLines = cmd.Parameters.AddWithValue("@PaymentLines", dtLines);
                pLines.SqlDbType = SqlDbType.Structured;
                pLines.TypeName = "[Accounts].[SupplierPaymentLineType]";

                await conn.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
        }
        #endregion

        #region Private Helper Methods
        private SupplierPayable MapSupplierPayable(IDataRecord record)
        {
            return new SupplierPayable
            {
                GoodsReceiveNoteId = record["SupplierPayableId"] as long? ?? 0,
                SupplierId = record["SupplierId"] as int? ?? 0,
                InvoiceNumber = record["GRNNumber"] as string,
                InvoiceDate = record["TransactionDate"] as DateTime? ?? DateTime.MinValue,
                DueDate = record["DueDate"] as DateTime? ?? DateTime.MinValue,
                TotalAmount = record["TotalGRNAmount"] as decimal? ?? 0m,
                PaidAmount = record["AmountSettled"] as decimal? ?? 0m,
                AmountDue = record["BalanceAmount"] as decimal? ?? 0m,
                Status = record["Status"] as string ?? "Unknown"
            };
        }
        private void AddPaymentParameters(SqlCommand command, SupplierPayment header)
        {
            command.Parameters.AddWithValue("@BranchId", header.BranchId);
            command.Parameters.AddWithValue("@SupplierId", header.SupplierId);
            command.Parameters.AddWithValue("@PaymentDate", header.PaymentDate);
            command.Parameters.AddWithValue("@PaymentMethod", header.PaymentMethod);
            command.Parameters.AddWithValue("@ReferenceNumber",
                string.IsNullOrWhiteSpace(header.ReferenceNumber) ? (object)DBNull.Value : header.ReferenceNumber);
            command.Parameters.AddWithValue("@TotalPaidAmount", header.PaidAmount);
            command.Parameters.AddWithValue("@CreatedBy", header.CreatedBy);



            command.Parameters.AddWithValue("@BankName",
              string.IsNullOrWhiteSpace(header.BankName) ? (object)DBNull.Value : header.BankName);

            // 2. Account Number
            command.Parameters.AddWithValue("@AccountNumber",
                string.IsNullOrWhiteSpace(header.AccountNumber) ? (object)DBNull.Value : header.AccountNumber);

            // 3. Account Name (Payee Name)
            command.Parameters.AddWithValue("@AccountName",
                string.IsNullOrWhiteSpace(header.AccountName) ? (object)DBNull.Value : header.AccountName);
        }

        private static void RenameSupplierPaymentVoucherTables(DataSet dataSet)
        {
            if (dataSet.Tables.Count > 0)
            {
                dataSet.Tables[0].TableName = "rptGetSupplierPaymentVoucherHeader";
            }

            if (dataSet.Tables.Count > 1)
            {
                dataSet.Tables[1].TableName = "rptGetSupplierPaymentVoucherLines";
            }
        }
        #endregion
    }
}
