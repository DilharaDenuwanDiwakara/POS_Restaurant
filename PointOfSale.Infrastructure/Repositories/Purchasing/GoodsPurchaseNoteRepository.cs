using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Repositories.Purchasing;
using PointOfSale.Core.Models.Purchasing;

namespace PointOfSale.Infrastructure.Repositories.Purchasing
{
    public class GoodsPurchaseNoteRepository : BaseRepository, IGoodsPurchaseNoteRepository
    {
        public GoodsPurchaseNoteRepository(DatabaseConnection databaseConnection) : base(databaseConnection) { }

        #region Public Method
        public async Task<long> CreateAsync(GoodPurchaseNote goodsPurchaseNote)
        {
            using (var connection = GetConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "[Purchasing].[uspInsertGoodPurchaseNote]";

                AddMainParameters(command, goodsPurchaseNote);
                AddLineItemsParameter(command, goodsPurchaseNote.Lines);
                AddOutputParameter(command);

                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();

                return Convert.ToInt64(command.Parameters["@PurchaseNoteId"].Value);
            }
        }
        public async Task<IEnumerable<GoodPurchaseNote>> GetAllAsync(int branchId, int? supplierId, DateTime? dateFrom, DateTime? dateTo)
        {
            var purchaseNotes = new List<GoodPurchaseNote>();

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Purchasing].[uspGetAllGoodsPurchaseNotes]"))
                {
                    command.Parameters.AddWithValue("@SupplierID", (object)supplierId ?? DBNull.Value);
                    command.Parameters.AddWithValue("@DateFrom", (object)dateFrom ?? DBNull.Value);
                    command.Parameters.AddWithValue("@DateTo", (object)dateTo ?? DBNull.Value);

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            purchaseNotes.Add(MapPurchaseNote(reader));
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occured while retrieving the POs.", ex);
            }
            return purchaseNotes;
        }
        public async Task<IEnumerable<GoodPurchaseNote>> GetPendingReceiptPOsAsync()
        {
            var pendingNotes = new List<GoodPurchaseNote>();

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Purchasing].[uspGetPendingPurchaseNotes]"))
                {
                    // Optional: Pass BranchId if you have multiple branches
                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            pendingNotes.Add(MapPurchaseNote(reader));
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while retrieving pending POs.", ex);
            }
            return pendingNotes;
        }
        public async Task<IEnumerable<GoodsPurchaseNoteLine>> GetPOLinesAsync(long purchaseNoteId)
        {
            var lines = new List<GoodsPurchaseNoteLine>();

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Purchasing].[uspGetPurchaseNoteLines]"))
                {
                    command.Parameters.AddWithValue("@PurchaseNoteId", purchaseNoteId);

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            lines.Add(MapPurchaseNoteLine(reader));
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occurred while retrieving lines for PO {purchaseNoteId}.", ex);
            }
            return lines;
        }
        public async Task<DataTable> GetPurchaseOrderReportDataAsync(long purchaseNoteId)
        {
            var dataTable = new DataTable();

            using (var connection = GetConnection())
            using (var command = CreateCommand(connection, "[Purchasing].[rptGetPurchaseOrderNote]"))
            {
                command.Parameters.AddWithValue("@TransferId", purchaseNoteId);

                await connection.OpenAsync();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    // Loads the data directly from the async reader into the generic DataTable
                    dataTable.Load(reader);
                }
            }

            return dataTable;
        }

        public async Task<IEnumerable<GoodPurchaseNote>> GetPOsForApprovalAsync(int branchId, int? supplierId, DateTime? dateFrom, DateTime? dateTo)
        {
            var purchaseNotes = new List<GoodPurchaseNote>();

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Purchasing].[uspGetPurchaseNotesForApproval]"))
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
                            purchaseNotes.Add(MapPurchaseNote(reader));
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while retrieving POs for approval.", ex);
            }

            return purchaseNotes;
        }

        public async Task ApproveRejectPOAsync(long purchaseNoteId, bool isApproved, int actionBy, string remarks)
        {
            using (var connection = GetConnection())
            using (var command = CreateCommand(connection, "[Purchasing].[uspApproveRejectPurchaseNote]"))
            {
                command.Parameters.AddWithValue("@PurchaseNoteId", purchaseNoteId);
                command.Parameters.AddWithValue("@IsApproved", isApproved);
                command.Parameters.AddWithValue("@ActionBy", actionBy);
                command.Parameters.AddWithValue("@Remarks", string.IsNullOrWhiteSpace(remarks) ? (object)DBNull.Value : remarks);

                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();
            }
        }

        public async Task SoftDeletePOAsync(long purchaseNoteId, int deletedBy)
        {
            using (var connection = GetConnection())
            using (var command = CreateCommand(connection, "[Purchasing].[uspSoftDeletePurchaseNote]"))
            {
                command.Parameters.AddWithValue("@PurchaseNoteId", purchaseNoteId);
                command.Parameters.AddWithValue("@DeletedBy", deletedBy);

                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();
            }
        }
        #endregion

        #region Private Method
        private void AddMainParameters(SqlCommand command, GoodPurchaseNote note)
        {
            command.Parameters.Add("@BranchId", SqlDbType.Int).Value = note.BranchId;
            command.Parameters.Add("@SupplierId", SqlDbType.Int).Value = note.SupplierId;
            AddDecimalParameter(command, "@SubTotal", note.SubTotal);
            AddDecimalParameter(command, "@DiscountAmount", note.DiscountAmount);
            AddDecimalParameter(command, "@TaxAmount", note.TaxAmount);
            command.Parameters.Add("@Notes", SqlDbType.NVarChar, 500).Value =
                string.IsNullOrWhiteSpace(note.Note) ? (object)DBNull.Value : note.Note.Trim();
            command.Parameters.Add("@OrderBy", SqlDbType.NVarChar, 100).Value =
                string.IsNullOrWhiteSpace(note.OrderBy) ? string.Empty : note.OrderBy.Trim();
            command.Parameters.Add("@OrderDate", SqlDbType.DateTime).Value = note.OrderDate;
            command.Parameters.Add("@ExpectedDeliveryDate", SqlDbType.DateTime).Value =
                note.ExpectedDeliveryDate.HasValue ? (object)note.ExpectedDeliveryDate.Value : DBNull.Value;
            command.Parameters.Add("@CreatedBy", SqlDbType.Int).Value = note.CreatedBy;
        }
        private void AddLineItemsParameter(SqlCommand command, IEnumerable<GoodsPurchaseNoteLine> lines)
        {
            var table = new DataTable();
            table.Columns.Add("ProductId", typeof(int));
            table.Columns.Add("UnitPrice", typeof(decimal));
            table.Columns.Add("QuantityOrdered", typeof(decimal));
            table.Columns.Add("LineDiscount", typeof(decimal));
            table.Columns.Add("TaxAmount", typeof(decimal));

            foreach (var line in lines)
            {
                table.Rows.Add(
                    line.ProductId,
                    line.UnitPrice,
                    line.QuantityOrdered,
                    line.LineDiscount,
                    line.TaxAmount
                );
            }

            var param = command.Parameters.AddWithValue("@PurchaseLines", table);
            param.SqlDbType = SqlDbType.Structured;
            param.TypeName = "Purchasing.GoodsPurchaseNoteLineType";
        }
        private void AddOutputParameter(SqlCommand command)
        {
            var outputParam = new SqlParameter("@PurchaseNoteId", SqlDbType.Int)
            {
                Direction = ParameterDirection.Output
            };
            command.Parameters.Add(outputParam);
        }
        private GoodPurchaseNote MapPurchaseNote(IDataRecord record)
        {
            return new GoodPurchaseNote
            {
                GoodsPurchaseNoteId = GetOptionalValue<long>(record, "Id"),
                BranchId = GetOptionalValue<int>(record, "BranchId"),
                SupplierId = GetOptionalValue<int>(record, "SupplierId"),
                PONumber = GetOptionalValue<string>(record, "PONumber"),
                SupplierName = GetOptionalValue<string>(record, "SupplierName"),
                SubTotal = GetOptionalValue<decimal>(record, "SubTotal"),
                DiscountAmount = GetOptionalValue<decimal>(record, "DiscountAmount"),
                TaxAmount = GetOptionalValue<decimal>(record, "TaxAmount"),
                TotalAmount = GetOptionalValue<decimal>(record, "TotalAmount"),
                Note = GetOptionalValue<string>(record, "Note"),
                Status = GetOptionalValue<string>(record, "Status"),
                OrderBy = GetOptionalValue<string>(record, "OrderBy"),
                OrderDate = GetOptionalValue<DateTime>(record, "OrderDate"),
                ExpectedDeliveryDate = GetOptionalValue<DateTime?>(record, "ExpectedDeliveryDate"),
                CreatedBy = GetOptionalValue<int>(record, "CreatedBy"),
                CreatedDate = GetOptionalValue<DateTime>(record, "CreatedDate"),
                ApprovedBy = GetOptionalValue<int?>(record, "ApprovedBy"),
                ApprovedAt = GetOptionalValue<DateTime?>(record, "ApprovedAt"),
                CancelledBy = GetOptionalValue<int?>(record, "CancelledBy"),
                CancelledAt = GetOptionalValue<DateTime?>(record, "CancelledAt"),
                Username = GetOptionalValue<string>(record, "Username")
            };
        }

        private GoodsPurchaseNoteLine MapPurchaseNoteLine(IDataRecord record)
        {
            var quantityReceived = GetOptionalValue<decimal>(record, "QuantityReceived");
            var alreadyReceivedQuantity = HasColumn(record, "AlreadyReceivedQuantity")
                ? GetValue<decimal>(record, "AlreadyReceivedQuantity")
                : quantityReceived;
            var taxAmount = GetOptionalValue<decimal>(record, "TaxAmount");

            return new GoodsPurchaseNoteLine
            {
                GoodsPurchaseNoteLineId = GetOptionalValue<long>(record, "Id"),
                GoodsPurchaseNoteId = GetOptionalValue<long>(record, "GoodsPurchaseNoteId"),
                ProductId = GetOptionalValue<int>(record, "ProductId"),
                ProductName = GetOptionalValue<string>(record, "ProductName"),
                QuantityOrdered = GetOptionalValue<decimal>(record, "QuantityOrdered"),
                QuantityReceived = quantityReceived,
                AlreadyReceivedQuantity = alreadyReceivedQuantity,
                UnitPrice = GetOptionalValue<decimal>(record, "UnitPrice"),
                LineDiscount = GetOptionalValue<decimal>(record, "LineDiscount"),
                TaxAmount = taxAmount,
                UnitMeasure = GetOptionalValue<string>(record, "UnitMeasureCode"),
                TrackExpiry = GetOptionalValue<bool>(record, "TrackExpiry"),
                IsTaxApplicable = HasColumn(record, "IsTaxApplicable")
                    ? GetValue<bool>(record, "IsTaxApplicable")
                    : taxAmount > 0m
            };
        }

        private static void AddDecimalParameter(SqlCommand command, string name, decimal value)
        {
            var parameter = command.Parameters.Add(name, SqlDbType.Decimal);
            parameter.Precision = 18;
            parameter.Scale = 2;
            parameter.Value = value;
        }

        private T GetOptionalValue<T>(IDataRecord record, string columnName)
        {
            return HasColumn(record, columnName) ? GetValue<T>(record, columnName) : default(T);
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
