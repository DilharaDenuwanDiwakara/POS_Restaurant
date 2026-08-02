using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using PointOfSale.Core.Enums;
using PointOfSale.Core.Interfaces.Repositories.Purchasing;
using PointOfSale.Core.Models.Purchasing;

namespace PointOfSale.Infrastructure.Repositories.Purchasing
{
    public class GoodsReceiveNoteRepository : BaseRepository, IGoodsReceiveNoteRepository
    {
        public GoodsReceiveNoteRepository(DatabaseConnection databaseConnection) : base(databaseConnection) { }


        #region Public Method
        public async Task<long> CreateAsync(GoodsReceiveNote goodsReceiveNote)
        {
            using (var connection = _databaseConnection.GetConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "[Purchasing].[uspInsertGoodsReceiveNote]";

                AddMainParameters(command, goodsReceiveNote);
                AddLineItemsParameter(command, goodsReceiveNote.Lines);
                AddOutputParameter(command);

                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();

                return (long)command.Parameters["@GoodsReceiveNoteId"].Value;
            }
        }
        public async Task<IEnumerable<GoodsReceiveNote>> GetAllAsync(int? supplierId, DateTime? dateFrom, DateTime? dateTo)
        {
            var receiveNotes = new List<GoodsReceiveNote>();

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Purchasing].[uspGetAllGoodsReceiveNotes]"))
                {
                    command.Parameters.AddWithValue("@SupplierID", (object)supplierId ?? DBNull.Value);
                    command.Parameters.AddWithValue("@DateFrom", (object)dateFrom ?? DBNull.Value);
                    command.Parameters.AddWithValue("@DateTo", (object)dateTo ?? DBNull.Value);

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            receiveNotes.Add(MapReceiveNote(reader));
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occured while retrieving the GRNs.", ex);
            }
            return receiveNotes;
        }
        public async Task<IEnumerable<GoodsReceiveNote>> GetPendingApprovalsAsync(int branchId, int? supplierId, DateTime? dateFrom, DateTime? dateTo)
        {
            var receiveNotes = new List<GoodsReceiveNote>();

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Purchasing].[uspGetGoodsReceiveNotesForApproval]"))
                {
                    command.Parameters.AddWithValue("@BranchId", branchId);
                    command.Parameters.AddWithValue("@SupplierId", (object)supplierId ?? DBNull.Value);
                    command.Parameters.AddWithValue("@DateFrom", (object)dateFrom ?? DBNull.Value);
                    command.Parameters.AddWithValue("@DateTo", (object)dateTo ?? DBNull.Value);

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            receiveNotes.Add(MapReceiveNote(reader));
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                // Fallback path for environments where the approval SP is not deployed yet.
                if (ex.Number == 2812) // Could not find stored procedure
                {
                    var existingItems = await GetAllAsync(supplierId, dateFrom, dateTo);
                    return existingItems.Where(x =>
                        string.Equals(x.Status, GoodsReceiveNoteStatus.PENDING_APPROVAL.ToString(), StringComparison.OrdinalIgnoreCase));
                }

                throw new InvalidOperationException($"A database error occured while retrieving GRNs for approval. {ex.Message}", ex);
            }

            return receiveNotes;
        }
        public async Task<IEnumerable<GoodsReceiveNoteLine>> GetLinesByGRNIdAsync(long goodsReceiveNoteId)
        {
            var lines = new List<GoodsReceiveNoteLine>();

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Purchasing].[uspGetGoodsReceiveNoteLinesById]"))
                {
                    command.Parameters.AddWithValue("@GoodsReceiveNoteId", goodsReceiveNoteId);

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            lines.Add(MapReceiveNoteLine(reader));
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occured while retrieving lines for GRN {goodsReceiveNoteId}. {ex.Message}", ex);
            }

            return lines;
        }
        public async Task<decimal> GetLastGrnCostPriceByProductIdAsync(int productId)
        {
            try
            {
                using (var connection = GetConnection())
                using (var command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = @"
                        SELECT TOP 1 L.UnitPrice
                        FROM [Purchasing].[GoodsReceiveNoteLine] L
                        INNER JOIN [Purchasing].[GoodsReceiveNote] G ON G.Id = L.GoodsReceiveNoteId
                        WHERE L.ProductId = @ProductId
                        ORDER BY G.ReceivedDate DESC, G.Id DESC";

                    command.Parameters.AddWithValue("@ProductId", productId);

                    await connection.OpenAsync();
                    var result = await command.ExecuteScalarAsync();
                    return (result != null && result != DBNull.Value) ? Convert.ToDecimal(result) : 0m;
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occurred while retrieving last GRN cost price for product {productId}.", ex);
            }
        }

        public async Task<DataTable> GetGoodsReceiveNoteReportDataAsync(long goodsReceiveNoteId)
        {
            var dataTable = new DataTable();

            using (var connection = GetConnection())
            using (var command = CreateCommand(connection, "[Purchasing].[rptGetGoodsReceiveNote]"))
            {
                command.Parameters.AddWithValue("@TransferId", goodsReceiveNoteId);

                await connection.OpenAsync();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    dataTable.Load(reader);
                }
            }

            return dataTable;
        }

        public async Task ApproveRejectAsync(long goodsReceiveNoteId, bool isApproved, int actionBy, string remarks)
        {
            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Purchasing].[uspApproveRejectGoodsReceiveNote]"))
                {
                    command.Parameters.AddWithValue("@GoodsReceiveNoteId", goodsReceiveNoteId);
                    command.Parameters.AddWithValue("@IsApproved", isApproved);
                    command.Parameters.AddWithValue("@ActionBy", actionBy);
                    command.Parameters.AddWithValue("@Remarks", string.IsNullOrWhiteSpace(remarks) ? (object)DBNull.Value : remarks.Trim());

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occured while processing the GRN approval. {ex.Message}", ex);
            }
        }

        public async Task ResubmitRejectedAsync(GoodsReceiveNote goodsReceiveNote)
        {
            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Purchasing].[uspResubmitRejectedGoodsReceiveNote]"))
                {
                    command.Parameters.AddWithValue("@GoodsReceiveNoteId", goodsReceiveNote.GoodsReceiveNoteId);
                    command.Parameters.AddWithValue("@BranchId", goodsReceiveNote.BranchId);
                    command.Parameters.AddWithValue("@SupplierId", goodsReceiveNote.SupplierId);
                    command.Parameters.AddWithValue("@PurchaseOrderId",
                        goodsReceiveNote.PurchaseOrderId > 0 ? (object)goodsReceiveNote.PurchaseOrderId : DBNull.Value);
                    command.Parameters.AddWithValue("@InvoiceNumber", goodsReceiveNote.InvoiceNumber);
                    command.Parameters.AddWithValue("@DiscountAmount", goodsReceiveNote.DiscountAmount);
                    command.Parameters.AddWithValue("@TaxAmount", goodsReceiveNote.TaxAmount);
                    command.Parameters.AddWithValue("@SubTotal", goodsReceiveNote.SubTotal);
                    command.Parameters.AddWithValue("@TotalAmount", goodsReceiveNote.TotalAmount);
                    command.Parameters.AddWithValue("@ReceivedBy", goodsReceiveNote.ReceivedBy);
                    command.Parameters.AddWithValue("@Notes", string.IsNullOrWhiteSpace(goodsReceiveNote.Notes) ? (object)DBNull.Value : goodsReceiveNote.Notes);
                    command.Parameters.AddWithValue("@ReceivedDate", goodsReceiveNote.GoodsReceiveNoteDate);
                    command.Parameters.AddWithValue("@CreditDays", goodsReceiveNote.CreditDays);
                    command.Parameters.AddWithValue("@DueDate", goodsReceiveNote.DueDate);
                    command.Parameters.AddWithValue("@UpdatedBy", goodsReceiveNote.CreatedBy);
                    AddLineItemsParameter(command, goodsReceiveNote.Lines);

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occured while resubmitting the rejected GRN. {ex.Message}", ex);
            }
        }
        #endregion

        #region Private Method
        private void AddMainParameters(SqlCommand command, GoodsReceiveNote note)
        {
            command.Parameters.AddWithValue("@BranchId", note.BranchId);
            command.Parameters.AddWithValue("@SupplierId", note.SupplierId);
            command.Parameters.AddWithValue("@PurchaseOrderId", note.PurchaseOrderId);
            command.Parameters.AddWithValue("@InvoiceNumber", note.InvoiceNumber);
            command.Parameters.AddWithValue("@DiscountAmount", note.DiscountAmount);
            command.Parameters.AddWithValue("@TaxAmount", note.TaxAmount);
            command.Parameters.AddWithValue("@SubTotal", note.SubTotal);
            command.Parameters.AddWithValue("@ReceivedBy", note.ReceivedBy);
            command.Parameters.AddWithValue("@Notes", string.IsNullOrWhiteSpace(note.Notes) ? (object)DBNull.Value : note.Notes);
            command.Parameters.AddWithValue("@ReceivedDate", note.GoodsReceiveNoteDate);
            command.Parameters.AddWithValue("@CreditDays", note.CreditDays);
            command.Parameters.AddWithValue("@DueDate", note.DueDate);
            command.Parameters.AddWithValue("@CreatedBy", note.CreatedBy);
        }
        private void AddLineItemsParameter(SqlCommand command, IEnumerable<GoodsReceiveNoteLine> lines)
        {
            var table = new DataTable();

            table.Columns.Add("GoodsPurchaseNoteLineId", typeof(int));
            table.Columns.Add("ProductId", typeof(int));
            table.Columns.Add("UnitPrice", typeof(decimal));
            table.Columns.Add("ExpiryDate", typeof(DateTime));
            table.Columns.Add("QuantityReceived", typeof(decimal));
            table.Columns.Add("LineDiscount", typeof(decimal));
            table.Columns.Add("TaxAmount", typeof(decimal));

            foreach (var line in lines)
            {
                table.Rows.Add(
                    line.GoodsPurchaseNoteLineId,
                    line.ProductId,
                    line.UnitPrice,
                    line.ExpiryDate.HasValue ? (object)line.ExpiryDate.Value : DBNull.Value,
                    line.QuantityReceived,
                    line.LineDiscount,
                    line.TaxAmount
                );
            }

            var param = command.Parameters.AddWithValue("@GoodsReceiveNoteLines", table);
            param.SqlDbType = SqlDbType.Structured;
            param.TypeName = "Purchasing.GoodsReceiveNoteLineType";
        }
        private void AddOutputParameter(SqlCommand command)
        {
            var outputParam = new SqlParameter("@GoodsReceiveNoteId", SqlDbType.BigInt)
            {
                Direction = ParameterDirection.Output
            };
            command.Parameters.Add(outputParam);
        }
        private GoodsReceiveNote MapReceiveNote(IDataRecord record)
        {
            return new GoodsReceiveNote
            {
                GoodsReceiveNoteId = GetValue<long>(record, "Id"),
                BranchId = GetOptionalValue<int>(record, "BranchId"),
                PurchaseOrderId = GetFirstOptionalInt64(record, "PurchaseOrderId", "GoodsPurchaseNoteId"),
                SupplierId = GetValue<int>(record, "SupplierId"),
                SupplierName = GetValue<string>(record, "SupplierName"),
                GoodsReceiveNoteNumber = GetValue<string>(record, "GoodsReceiveNoteNumber"),
                InvoiceNumber = GetValue<string>(record, "InvoiceNumber"),
                SubTotal = GetValue<decimal>(record, "SubTotal"),
                DiscountAmount = GetValue<decimal>(record, "DiscountAmount"),
                TaxAmount = GetValue<decimal>(record, "TaxAmount"),
                TotalAmount = GetValue<decimal>(record, "TotalAmount"),
                Notes = GetValue<string>(record, "Note"),
                ReceivedBy = GetValue<string>(record, "ReceivedBy"),
                GoodsReceiveNoteDate = GetValue<DateTime>(record, "ReceivedDate"),
                CreditDays = GetOptionalValue<int>(record, "CreditDays"),
                DueDate = GetOptionalValue<DateTime>(record, "DueDate"),
                Status = GetValue<string>(record, "Status"),
                CreatedBy = GetValue<int>(record, "CreatedBy"),
                CreatedDate = GetValue<DateTime>(record, "CreatedAt"),
                Username = GetOptionalValue<string>(record, "Username"),
                PONumber = GetFirstOptionalString(record, "PONumber", "PoNumber", "PurchaseOrderNumber", "OriginalPONumber"),
                CreatedByName = GetFirstOptionalString(record, "CreatedByName", "CreatorName", "ReceiverName")
            };
        }
        private GoodsReceiveNoteLine MapReceiveNoteLine(IDataRecord record)
        {
            var taxAmount = HasColumn(record, "TaxAmount")
                ? GetValue<decimal>(record, "TaxAmount")
                : GetOptionalValue<decimal>(record, "LineTaxAmount");
            var unitPrice = GetValue<decimal>(record, "UnitPrice");

            var line = new GoodsReceiveNoteLine
            {
                GoodsPurchaseNoteLineId = GetValue<long>(record, "GoodsPurchaseNoteLineId"),
                ProductId = GetValue<int>(record, "ProductId"),
                ProductName = GetValue<string>(record, "ProductName"),
                QuantityOrdered = HasColumn(record, "QuantityOrdered")
                    ? GetValue<decimal>(record, "QuantityOrdered")
                    : 0m,
                UnitMeasure = GetFirstOptionalString(record, "UnitMeasure", "UnitMeasureCode", "UOM"),
                OrderedPrice = HasColumn(record, "OrderedPrice")
                    ? GetValue<decimal>(record, "OrderedPrice")
                    : unitPrice,
                UnitPrice = unitPrice,
                LineDiscount = HasColumn(record, "LineDiscount")
                    ? GetValue<decimal>(record, "LineDiscount")
                    : 0m,
                TaxAmount = taxAmount,
                IsTaxApplicable = HasColumn(record, "IsTaxApplicable") && GetValue<bool>(record, "IsTaxApplicable"),
                TrackExpiry = HasColumn(record, "TrackExpiry") && GetValue<bool>(record, "TrackExpiry"),
                ExpiryDate = HasColumn(record, "ExpiryDate")
                    ? GetValue<DateTime?>(record, "ExpiryDate")
                    : null
            };

            line.SetStoredQuantityReceived(GetValue<decimal>(record, "QuantityReceived"));
            return line;
        }

        private T GetOptionalValue<T>(IDataRecord record, string columnName)
        {
            return HasColumn(record, columnName) ? GetValue<T>(record, columnName) : default(T);
        }

        private string GetFirstOptionalString(IDataRecord record, params string[] columnNames)
        {
            foreach (var columnName in columnNames)
            {
                if (HasColumn(record, columnName))
                {
                    var value = GetValue<string>(record, columnName);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }

            return null;
        }

        private long GetFirstOptionalInt64(IDataRecord record, params string[] columnNames)
        {
            foreach (var columnName in columnNames)
            {
                if (HasColumn(record, columnName))
                {
                    return GetOptionalValue<long>(record, columnName);
                }
            }

            return 0L;
        }

        private bool HasColumn(IDataRecord record, string columnName)
        {
            for (var i = 0; i < record.FieldCount; i++)
            {
                if (string.Equals(record.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
        #endregion
    }
}
