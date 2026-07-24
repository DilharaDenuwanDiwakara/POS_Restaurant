using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Repositories.Purchasing;
using PointOfSale.Core.Models.Purchasing;

namespace PointOfSale.Infrastructure.Repositories.Purchasing
{
    public class SupplierReturnRepository : BaseRepository, ISupplierReturnRepository
    {
        public SupplierReturnRepository(DatabaseConnection databaseConnection) : base(databaseConnection) { }


        #region Public Method
        public async Task<int> CreateAsync(SupplierReturn supplierReturn)
        {
            if (supplierReturn == null) throw new ArgumentNullException(nameof(supplierReturn));

            using (var connection = GetConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "[Purchasing].[uspInsertSupplierReturn]";

                AddHeaderParameters(command, supplierReturn);
                AddLineTableParameter(command, supplierReturn);

                var outputIdParam = command.Parameters.Add("@SupplierReturnId", SqlDbType.Int);
                outputIdParam.Direction = ParameterDirection.Output;

                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();

                return (int)outputIdParam.Value;
            }
        }
        public async Task<IEnumerable<SupplierReturn>> GetAllAsync(int? supplierId, DateTime? dateFrom, DateTime? dateTo)
        {
            var returnNotes = new List<SupplierReturn>();

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Purchasing].[uspGetAllSupplierReturn]"))
                {
                    command.Parameters.AddWithValue("@SupplierID", (object)supplierId ?? DBNull.Value);
                    command.Parameters.AddWithValue("@DateFrom", (object)dateFrom ?? DBNull.Value);
                    command.Parameters.AddWithValue("@DateTo", (object)dateTo ?? DBNull.Value);

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            returnNotes.Add(MapReturnNote(reader));
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occured while retrieving the Return Notes.", ex);
            }
            return returnNotes;
        }

        public async Task<IEnumerable<SupplierReturn>> GetPendingApprovalsAsync(int branchId, int? supplierId, DateTime? dateFrom, DateTime? dateTo)
        {
            var pendingReturns = new List<SupplierReturn>();

            try
            {
                using (var connection = GetConnection())
                using (var command = connection.CreateCommand())
                {
                    await connection.OpenAsync();

                    var hasLocationId = await ColumnExistsAsync(connection, "Purchasing.SupplierReturnNote", "LocationId");
                    var hasNote = await ColumnExistsAsync(connection, "Purchasing.SupplierReturnNote", "Note");
                    var hasApproverRemark = await ColumnExistsAsync(connection, "Purchasing.SupplierReturnNote", "ApproverRemark");
                    var hasTotalAmount = await ColumnExistsAsync(connection, "Purchasing.SupplierReturnNote", "TotalAmount");

                    command.CommandType = CommandType.Text;
                    command.CommandText = $@"
                        SELECT
                            SRN.Id,
                            SRN.BranchId,
                            {(hasLocationId ? "SRN.LocationId" : "CAST(0 AS int) AS LocationId")},
                            SRN.ReturnNoteNumber,
                            SRN.SupplierId,
                            S.Name AS SupplierName,
                            SRN.ReturnedBy,
                            SRN.ReturnedDate,
                            SRN.Status,
                            ISNULL(SRN.SubTotal, 0) AS SubTotal,
                            ISNULL(SRN.DiscountAmount, 0) AS DiscountAmount,
                            ISNULL(SRN.TaxAmount, 0) AS TaxAmount,
                            {(hasTotalAmount ? "ISNULL(SRN.TotalAmount, 0) AS TotalAmount" : "CAST(ISNULL(SRN.SubTotal, 0) - ISNULL(SRN.DiscountAmount, 0) + ISNULL(SRN.TaxAmount, 0) AS decimal(18, 2)) AS TotalAmount")},
                            {(hasNote ? "SRN.Note" : "CAST(NULL AS nvarchar(500)) AS Note")},
                            {(hasApproverRemark ? "SRN.ApproverRemark" : "CAST(NULL AS nvarchar(255)) AS ApproverRemark")},
                            SRN.CreatedBy
                        FROM [Purchasing].[SupplierReturnNote] SRN
                        INNER JOIN [Purchasing].[Supplier] S ON S.Id = SRN.SupplierId
                        WHERE SRN.BranchId = @BranchId
                          AND UPPER(SRN.Status) IN ('PENDING_APPROVAL', 'PENDING')
                          AND (@SupplierId IS NULL OR SRN.SupplierId = @SupplierId)
                          AND (@DateFrom IS NULL OR CAST(SRN.ReturnedDate AS date) >= @DateFrom)
                          AND (@DateTo IS NULL OR CAST(SRN.ReturnedDate AS date) <= @DateTo)
                        ORDER BY SRN.ReturnedDate DESC, SRN.Id DESC;";

                    command.Parameters.Add("@BranchId", SqlDbType.Int).Value = branchId;
                    command.Parameters.Add("@SupplierId", SqlDbType.Int).Value = (object)supplierId ?? DBNull.Value;
                    command.Parameters.Add("@DateFrom", SqlDbType.Date).Value = (object)dateFrom?.Date ?? DBNull.Value;
                    command.Parameters.Add("@DateTo", SqlDbType.Date).Value = (object)dateTo?.Date ?? DBNull.Value;

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            pendingReturns.Add(MapReturnNote(reader));
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occured while retrieving pending supplier returns.", ex);
            }

            return pendingReturns;
        }

        private async Task<bool> ColumnExistsAsync(SqlConnection connection, string tableName, string columnName)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.Text;
                command.CommandText = "SELECT CASE WHEN COL_LENGTH(@TableName, @ColumnName) IS NULL THEN 0 ELSE 1 END;";
                command.Parameters.Add("@TableName", SqlDbType.NVarChar, 256).Value = tableName;
                command.Parameters.Add("@ColumnName", SqlDbType.NVarChar, 128).Value = columnName;

                var result = await command.ExecuteScalarAsync();
                return Convert.ToInt32(result) == 1;
            }
        }

        public async Task<IEnumerable<SupplierReturnLine>> GetLinesByReturnIdAsync(int supplierReturnId)
        {
            var lines = new List<SupplierReturnLine>();

            try
            {
                using (var connection = GetConnection())
                using (var command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = @"
                        SELECT
                            L.Id AS SupplierReturnLineId,
                            L.SupplierReturnNoteId AS SupplierReturnId,
                            L.GoodsReceiveNoteLineId,
                            L.ProductId,
                            P.Name AS ProductName,
                            UM.Code AS UnitMeasureCode,
                            L.BatchId,
                            L.ReturnReasonId,
                            RR.Description AS ReturnReasonDescription,
                            L.QuantityReturn AS Quantity,
                            L.UnitPrice,
                            ISNULL(L.LineDiscount, 0) AS LineDiscount,
                            ISNULL(L.TaxAmount, 0) AS TaxAmount,
                            CAST((L.QuantityReturn * L.UnitPrice) - ISNULL(L.LineDiscount, 0) + ISNULL(L.TaxAmount, 0) AS decimal(18, 2)) AS LineTotal
                        FROM [Purchasing].[SupplierReturnNoteLine] L
                        INNER JOIN [Inventory].[Product] P ON P.Id = L.ProductId
                        LEFT JOIN [Inventory].[UnitMeasure] UM ON UM.Id = P.UnitMeasureId
                        LEFT JOIN [Purchasing].[ReturnReason] RR ON RR.Id = L.ReturnReasonId
                        WHERE L.SupplierReturnNoteId = @SupplierReturnId
                        ORDER BY L.Id;";

                    command.Parameters.Add("@SupplierReturnId", SqlDbType.Int).Value = supplierReturnId;

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            lines.Add(MapReturnLine(reader));
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occured while retrieving lines for supplier return {supplierReturnId}.", ex);
            }

            return lines;
        }

        public async Task ApproveAsync(int supplierReturnId, int approvedBy)
        {
            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Purchasing].[uspApproveSupplierReturn]"))
                {
                    command.Parameters.Add("@SupplierReturnId", SqlDbType.Int).Value = supplierReturnId;
                    command.Parameters.Add("@ApprovedBy", SqlDbType.Int).Value = approvedBy;

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occured while approving supplier return {supplierReturnId}.", ex);
            }
        }

        public async Task RejectAsync(int supplierReturnId, string approverRemark, int rejectedBy)
        {
            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Purchasing].[uspRejectSupplierReturn]"))
                {
                    command.Parameters.Add("@SupplierReturnId", SqlDbType.Int).Value = supplierReturnId;
                    command.Parameters.Add("@ApproverRemark", SqlDbType.NVarChar, 255).Value =
                        string.IsNullOrWhiteSpace(approverRemark) ? (object)DBNull.Value : approverRemark.Trim();
                    command.Parameters.Add("@RejectedBy", SqlDbType.Int).Value = rejectedBy;

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occured while rejecting supplier return {supplierReturnId}.", ex);
            }
        }

        public async Task<DataTable> GetSupplierReturnReportDataAsync(int supplierReturnId)
        {
            var dataTable = new DataTable();

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Purchasing].[rptGetSupplierReturnNote]"))
                {
                    command.Parameters.Add("@SupplierReturnId", SqlDbType.Int).Value = supplierReturnId;

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        dataTable.Load(reader);
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occured while retrieving the report data for supplier return {supplierReturnId}.", ex);
            }

            return dataTable;
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
                    command.CommandText = @"
                        SELECT Id, Description
                        FROM [Purchasing].[ReturnReason]
                        WHERE IsActive = 1
                        ORDER BY Description;";

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            reasons.Add(new ReturnReasonModel
                            {
                                Id = GetValue<int>(reader, "Id"),
                                Description = GetValue<string>(reader, "Description")
                            });
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occured while retrieving return reasons.", ex);
            }

            return reasons;
        }

        public async Task<OriginalGrnReturnLine> GetOriginalGrnLineForBatchAsync(long batchId)
        {
            try
            {
                using (var connection = GetConnection())
                using (var command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = @"
                        DECLARE @Sql nvarchar(max);

                        IF COL_LENGTH('Inventory.ProductBatch', 'GoodsReceiveNoteLineId') IS NOT NULL
                        BEGIN
                            SET @Sql = N'
                                SELECT TOP 1
                                    L.Id AS GoodsReceiveNoteLineId,
                                    L.QuantityReceived AS OriginalQuantity,
                                    L.UnitPrice,
                                    ISNULL(L.LineDiscount, 0) AS LineDiscount,
                                    ISNULL(L.TaxAmount, 0) AS TaxAmount
                                FROM [Inventory].[ProductBatch] B
                                INNER JOIN [Purchasing].[GoodsReceiveNoteLine] L ON L.Id = B.GoodsReceiveNoteLineId
                                WHERE B.Id = @BatchId';
                        END
                        ELSE IF COL_LENGTH('Purchasing.GoodsReceiveNoteLine', 'BatchId') IS NOT NULL
                        BEGIN
                            SET @Sql = N'
                                SELECT TOP 1
                                    L.Id AS GoodsReceiveNoteLineId,
                                    L.QuantityReceived AS OriginalQuantity,
                                    L.UnitPrice,
                                    ISNULL(L.LineDiscount, 0) AS LineDiscount,
                                    ISNULL(L.TaxAmount, 0) AS TaxAmount
                                FROM [Purchasing].[GoodsReceiveNoteLine] L
                                WHERE L.BatchId = @BatchId';
                        END

                        IF @Sql IS NOT NULL
                        BEGIN
                            EXEC sp_executesql @Sql, N'@BatchId bigint', @BatchId;
                        END";

                    command.Parameters.Add("@BatchId", SqlDbType.BigInt).Value = batchId;

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new OriginalGrnReturnLine
                            {
                                GoodsReceiveNoteLineId = GetValue<long>(reader, "GoodsReceiveNoteLineId"),
                                OriginalQuantity = GetValue<decimal>(reader, "OriginalQuantity"),
                                UnitPrice = GetValue<decimal>(reader, "UnitPrice"),
                                LineDiscount = GetValue<decimal>(reader, "LineDiscount"),
                                TaxAmount = GetValue<decimal>(reader, "TaxAmount")
                            };
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occured while retrieving original GRN values for batch {batchId}.", ex);
            }

            return null;
        }
        #endregion

        #region Private Method
        private void AddHeaderParameters(SqlCommand command, SupplierReturn supplierReturn)
        {
            command.Parameters.AddWithValue("@BranchId", supplierReturn.BranchId);
            command.Parameters.AddWithValue("@LocationId", supplierReturn.LocationId);
            command.Parameters.AddWithValue("@SupplierId", supplierReturn.SupplierId);
            command.Parameters.AddWithValue("@ReturnedBy", supplierReturn.ReturnedBy ?? (object)DBNull.Value);
            command.Parameters.Add("@Note", SqlDbType.NVarChar, 500).Value =
                string.IsNullOrWhiteSpace(supplierReturn.Note) ? (object)DBNull.Value : supplierReturn.Note.Trim();
            command.Parameters.AddWithValue("@ReturnDate", supplierReturn.ReturnDate);
            command.Parameters.AddWithValue("@SubTotal", supplierReturn.SubTotal);
            command.Parameters.AddWithValue("@DiscountAmount", supplierReturn.DiscountAmount);
            command.Parameters.AddWithValue("@TaxAmount", supplierReturn.TaxAmount);
            command.Parameters.AddWithValue("@NetAmount", supplierReturn.NetAmount);
            command.Parameters.AddWithValue("@CreatedBy", supplierReturn.CreatedBy);
        }
        private void AddLineTableParameter(SqlCommand command, SupplierReturn supplierReturn)
        {
            var lineTable = new DataTable();
            // Must match Purchasing.SupplierReturnLineType exactly by column order and type.
            lineTable.Columns.Add("ProductId", typeof(int));
            lineTable.Columns.Add("BatchId", typeof(int));
            lineTable.Columns.Add("ReturnReasonId", typeof(int));
            lineTable.Columns.Add("Quantity", typeof(decimal));
            lineTable.Columns.Add("UnitCost", typeof(decimal));
            lineTable.Columns.Add("LineDiscount", typeof(decimal));
            lineTable.Columns.Add("TaxAmount", typeof(decimal));

            foreach (var line in supplierReturn.Lines)
            {
                lineTable.Rows.Add(
                    line.ProductId,
                    checked((int)line.BatchId),
                    line.ReturnReasonId,
                    line.Quantity,
                    line.UnitPrice,
                    line.LineDiscount,
                    line.TaxAmount);
            }
            var lineParam = command.Parameters.AddWithValue("@Lines", lineTable);

            lineParam.SqlDbType = SqlDbType.Structured;
            lineParam.TypeName = "Purchasing.SupplierReturnLineType";
        }
        private SupplierReturn MapReturnNote(IDataRecord record)
        {
            return new SupplierReturn
            {

                SupplierReturnId = GetValue<int>(record, "Id"),
                BranchId = HasColumn(record, "BranchId") ? GetValue<int>(record, "BranchId") : 0,
                LocationId = HasColumn(record, "LocationId") ? GetValue<int>(record, "LocationId") : 0,
                SupplierId = HasColumn(record, "SupplierId") ? GetValue<int>(record, "SupplierId") : 0,
                ReturnNumber = GetValue<string>(record, "ReturnNoteNumber"),
                SupplierName = GetValue<string>(record, "SupplierName"),
                SubTotal = HasColumn(record, "SubTotal") ? GetValue<decimal>(record, "SubTotal") : 0m,
                DiscountAmount = HasColumn(record, "DiscountAmount") ? GetValue<decimal>(record, "DiscountAmount") : 0m,
                TaxAmount = HasColumn(record, "TaxAmount") ? GetValue<decimal>(record, "TaxAmount") : 0m,
                NetAmount = HasColumn(record, "NetAmount") ? GetValue<decimal>(record, "NetAmount") : GetValue<decimal>(record, "TotalAmount"),
                Note = HasColumn(record, "Note") ? GetValue<string>(record, "Note") : null,
                ReturnedBy = GetValue<string>(record, "ReturnedBy"),
                ReturnDate = GetValue<DateTime>(record, "ReturnedDate"),
                Status = HasColumn(record, "Status") ? GetValue<string>(record, "Status") : null,
                ApproverRemark = HasColumn(record, "ApproverRemark") ? GetValue<string>(record, "ApproverRemark") : null,
                ApprovedBy = HasColumn(record, "ApprovedBy") ? GetValue<int?>(record, "ApprovedBy") : null,
                ApprovedAt = HasColumn(record, "ApprovedAt") ? GetValue<DateTime?>(record, "ApprovedAt") : null,
                RejectedBy = HasColumn(record, "RejectedBy") ? GetValue<int?>(record, "RejectedBy") : null,
                RejectedAt = HasColumn(record, "RejectedAt") ? GetValue<DateTime?>(record, "RejectedAt") : null,
                CreatedBy = GetValue<int>(record, "CreatedBy")
            };
        }

        private SupplierReturnLine MapReturnLine(IDataRecord record)
        {
            var quantity = GetValue<decimal>(record, "Quantity");
            var unitPrice = GetValue<decimal>(record, "UnitPrice");
            var lineDiscount = GetValue<decimal>(record, "LineDiscount");
            var taxAmount = GetValue<decimal>(record, "TaxAmount");

            return new SupplierReturnLine
            {
                SupplierReturnLineId = GetValue<int>(record, "SupplierReturnLineId"),
                SupplierReturnId = GetValue<int>(record, "SupplierReturnId"),
                GoodsReceiveNoteLineId = GetValue<long>(record, "GoodsReceiveNoteLineId"),
                ProductId = GetValue<int>(record, "ProductId"),
                ProductName = GetValue<string>(record, "ProductName"),
                UnitMeasure = HasColumn(record, "UnitMeasureCode") ? GetValue<string>(record, "UnitMeasureCode") : null,
                BatchId = GetValue<long>(record, "BatchId"),
                ReturnReasonId = GetValue<int>(record, "ReturnReasonId"),
                ReturnReasonDescription = GetValue<string>(record, "ReturnReasonDescription"),
                Quantity = quantity,
                UnitPrice = unitPrice,
                LineSubTotal = quantity * unitPrice,
                LineDiscount = lineDiscount,
                TaxAmount = taxAmount,
                LineTotal = HasColumn(record, "LineTotal")
                    ? GetValue<decimal>(record, "LineTotal")
                    : (quantity * unitPrice) - lineDiscount + taxAmount
            };
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
