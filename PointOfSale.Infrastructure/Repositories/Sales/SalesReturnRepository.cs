using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Sales;
using PointOfSale.Core.Models.Sales;

namespace PointOfSale.Infrastructure.Repositories.Sales
{
    public class SalesReturnRepository : BaseRepository, ISalesReturnRepository
    {
        public SalesReturnRepository(DatabaseConnection databaseConnection) : base(databaseConnection) { }

        #region Public Methods
        public async Task<SalesReturnLookupDto> GetOrderForReturnAsync(string invoiceNumber)
        {
            if (string.IsNullOrWhiteSpace(invoiceNumber))
            {
                throw new ArgumentException("Invoice number is required.", nameof(invoiceNumber));
            }

            try
            {
                using (var connection = GetConnection())
                {
                    await connection.OpenAsync();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.CommandText = "[Sales].[uspGetInvoiceForReturn]";
                        command.Parameters.Add("@InvoiceNumber", SqlDbType.NVarChar, 50).Value = invoiceNumber.Trim();

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            // 1. Read Result Set 1: The Header
                            if (!await reader.ReadAsync())
                            {
                                return null; // Invoice not found or not completed
                            }

                            var header = new SalesReturnLookupDto
                            {
                                SalesId = GetValue<long>(reader, "SalesId"),
                                InvoiceNumber = GetValue<string>(reader, "InvoiceNumber"),
                                BranchId = GetValue<int>(reader, "BranchId"),
                                ShiftId = GetValue<int?>(reader, "ShiftId"),
                                SalesDate = GetValue<DateTime>(reader, "SalesDate")
                            };

                            // 2. Read Result Set 2: The Lines
                            if (await reader.NextResultAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    header.Lines.Add(new SalesReturnItemModel
                                    {
                                        SalesLineId = GetValue<long>(reader, "SalesLineId"),
                                        ProductName = GetValue<string>(reader, "ProductName"),
                                        SoldQty = GetValue<decimal>(reader, "SoldQty"), // This is now the "Remaining Qty"
                                        UnitPrice = GetValue<decimal>(reader, "UnitPrice"),
                                        ReturnQty = 0,
                                        ReturnReasonId = 0,
                                        IsWastage = false
                                    });
                                }
                            }

                            return header;
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occurred while retrieving invoice '{invoiceNumber}' for return.", ex);
            }
        }

        public async Task<IEnumerable<ReturnReasonModel>> GetActiveReturnReasonsAsync()
        {
            var reasons = new List<ReturnReasonModel>();

            try
            {
                using (var connection = GetConnection())
                using (var command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    // NOTE: Adjust the schema/table name below if customer-facing sales return
                    // reasons are stored in a different table than [Sales].[ReturnReason].
                    command.CommandText = @"
                        SELECT Id, Reason
                        FROM [Sales].[ReturnReason]
                        WHERE IsActive = 1
                        ORDER BY Reason;";

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            reasons.Add(new ReturnReasonModel
                            {
                                Id = GetValue<int>(reader, "Id"),
                                Reason = GetValue<string>(reader, "Reason")
                            });
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while retrieving sales return reasons.", ex);
            }

            return reasons;
        }

        public async Task<long> ProcessSalesReturnAsync(
            long salesId,
            int branchId,
            int shiftId,
            int authorizedBy,
            int createdBy,
            List<SalesReturnItemModel> returnItems)
        {
            if (returnItems == null || returnItems.Count == 0)
            {
                throw new ArgumentException("At least one return line is required.", nameof(returnItems));
            }

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Sales].[uspProcessSalesReturn]"))
                {
                    command.Parameters.Add("@SalesId", SqlDbType.BigInt).Value = salesId;
                    command.Parameters.Add("@BranchId", SqlDbType.Int).Value = branchId;
                    command.Parameters.Add("@ShiftId", SqlDbType.Int).Value = shiftId;
                    command.Parameters.Add("@AuthorizedBy", SqlDbType.Int).Value = authorizedBy;
                    command.Parameters.Add("@CreatedBy", SqlDbType.Int).Value = createdBy;

                    // @LocationId and @PeriodId are no longer part of [Sales].[uspProcessSalesReturn]'s
                    // signature: the procedure derives the location from @SalesId and resolves the
                    // open accounting period internally (querying the period table itself).
                    AddReturnLinesParameter(command, returnItems);

                    var newReturnIdParam = command.Parameters.Add("@NewReturnId", SqlDbType.BigInt);
                    newReturnIdParam.Direction = ParameterDirection.Output;

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();

                    if (newReturnIdParam.Value == DBNull.Value || newReturnIdParam.Value == null)
                    {
                        throw new InvalidOperationException("The sales return was not saved. The database did not return a valid return id.");
                    }

                    return Convert.ToInt64(newReturnIdParam.Value);
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occurred while processing the sales return for SalesId {salesId}: {ex.Message}", ex);
            }
        }
        #endregion

        #region Private Methods
        private void AddReturnLinesParameter(SqlCommand command, List<SalesReturnItemModel> returnItems)
        {
            var table = new DataTable();
            // Must match [Sales].[tvpSalesReturnLine] exactly by column order and type:
            // SalesLineId bigint, QuantityReturned decimal(18,3), RefundAmount decimal(18,2),
            // ReturnReasonId int, IsWastage bit.
            table.Columns.Add("SalesLineId", typeof(long));
            table.Columns.Add("QuantityReturned", typeof(decimal));
            table.Columns.Add("RefundAmount", typeof(decimal));
            table.Columns.Add("ReturnReasonId", typeof(int));
            table.Columns.Add("IsWastage", typeof(bool));

            foreach (var item in returnItems)
            {
                table.Rows.Add(
                    item.SalesLineId,
                    item.ReturnQty,
                    item.RefundAmount,
                    item.ReturnReasonId,
                    item.IsWastage);
            }

            var param = command.Parameters.AddWithValue("@ReturnLines", table);
            param.SqlDbType = SqlDbType.Structured;
            param.TypeName = "[Sales].[tvpSalesReturnLine]";
        }
        #endregion
    }
}
