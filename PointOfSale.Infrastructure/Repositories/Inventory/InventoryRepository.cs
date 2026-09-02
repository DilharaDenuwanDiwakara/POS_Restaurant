using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Inventory;
using PointOfSale.Core.Models.Inventory;

namespace PointOfSale.Infrastructure.Repositories.Inventory
{
    public class InventoryRepository : BaseRepository, IInventoryRepository
    {
        public InventoryRepository(DatabaseConnection databaseConnection) : base(databaseConnection) { }

        public async Task<long> CreateStockTransferAsync(StockTransfer transfer)
        {
            using (var connection = GetConnection()) // Assumes you have a BaseRepository
            {
                using (var command = CreateCommand(connection, "[Inventory].[uspInsertStockTransfer]"))
                {
                    // Add Header Parameters
                    command.Parameters.AddWithValue("@BranchId", transfer.BranchId);
                    command.Parameters.AddWithValue("@FromLocationId", transfer.FromLocationId);
                    command.Parameters.AddWithValue("@ToLocationId", transfer.ToLocationId);
                    command.Parameters.AddWithValue("@TransferDate", transfer.TransferDate);
                    command.Parameters.AddWithValue("@Note", (object)transfer.Note ?? DBNull.Value);
                    command.Parameters.AddWithValue("@CreatedBy", transfer.CreatedBy); // Usually from UserSession

                    // Add Lines Parameter (Table Valued Parameter)
                    var linesTable = CreateLinesDataTable(transfer.Lines);
                    var linesParam = command.Parameters.AddWithValue("@Lines", linesTable);
                    linesParam.SqlDbType = SqlDbType.Structured;
                    linesParam.TypeName = "[Inventory].[StockTransferLineType]";

                    // Output Parameter for the new Transfer ID
                    var outParam = new SqlParameter("@TransferId", SqlDbType.BigInt)
                    {
                        Direction = ParameterDirection.Output
                    };
                    command.Parameters.Add(outParam);

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();

                    var transferIdValue = command.Parameters["@TransferId"].Value;
                    if (transferIdValue == null || transferIdValue == DBNull.Value)
                    {
                        throw new InvalidOperationException("Stock transfer was saved, but the database did not return a TransferId.");
                    }

                    return Convert.ToInt64(transferIdValue);
                }
            }
        }
        public async Task ImportOpeningStockAsync(List<OpenStockItemDto> items, int userId, int locationId = 1)
        {
            if (items == null || !items.Any()) return;

            // 1. Create a DataTable that matches [Inventory].[dt_OpeningStockImport] EXACTLY
            var dt = new DataTable();
            dt.Columns.Add("Code", typeof(string));
            dt.Columns.Add("Quantity", typeof(decimal));
            dt.Columns.Add("SellingPrice", typeof(decimal));
            dt.Columns.Add("UnitCost", typeof(decimal));

            // 2. Populate DataTable
            foreach (var item in items)
            {
                dt.Rows.Add(
                    item.ProductCode,
                    item.Quantity,
                    item.SellingPrice,
                    item.UnitCost.HasValue ? (object)item.UnitCost.Value : DBNull.Value
                );
            }

            try
            {
                using (var conn = GetConnection())
                {
                    await conn.OpenAsync();

                    // 3. Call the Stored Procedure
                    using (var cmd = CreateCommand(conn, "[Inventory].[uspImportOpeningStock]"))
                    {
                        cmd.Parameters.AddWithValue("@LocationId", locationId);
                        cmd.Parameters.AddWithValue("@CreatedBy", userId);

                        // 4. Pass the Table Valued Parameter
                        var tvpParam = cmd.Parameters.AddWithValue("@ImportLines", dt);
                        tvpParam.SqlDbType = SqlDbType.Structured;
                        tvpParam.TypeName = "[Inventory].[OpeningStockImportType]";

                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("Failed to import opening stock. Database error: " + ex.Message, ex);
            }
        }

        public string SaveOpeningStock(int locationId, int userId, DateTime openingDate, List<OpeningStockItemModel> stockItems)
        {
            if (locationId <= 0)
                throw new ArgumentOutOfRangeException(nameof(locationId), "A valid location is required.");

            if (userId <= 0)
                throw new ArgumentOutOfRangeException(nameof(userId), "A valid user is required.");

            if (stockItems == null)
                throw new ArgumentNullException(nameof(stockItems));

            var openingStockTable = CreateOpeningStockDataTable(stockItems);
            if (openingStockTable.Rows.Count == 0)
                throw new InvalidOperationException("At least one product must have an opening quantity greater than zero.");

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Inventory].[uspSaveOpeningStock]"))
                {
                    command.Parameters.Add("@LocationId", SqlDbType.Int).Value = locationId;
                    command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                    command.Parameters.Add("@OpeningDate", SqlDbType.DateTime).Value = openingDate;

                    var stockItemsParameter = command.Parameters.Add("@StockItems", SqlDbType.Structured);
                    stockItemsParameter.TypeName = "[Inventory].[udtOpeningStock]";
                    stockItemsParameter.Value = openingStockTable;

                    connection.Open();
                    var result = command.ExecuteScalar();

                    if (result == null || result == DBNull.Value)
                        throw new InvalidOperationException("Opening stock was saved, but the database did not return a document number.");

                    return Convert.ToString(result);
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2627 || ex.Number == 2601 || ex.Number == 50000)
                {
                    throw new InvalidOperationException(ex.Message, ex);
                }

                throw new InvalidOperationException("A database error occurred while saving opening stock.", ex);
            }
        }

        public async Task<List<OpeningStockItemModel>> GetOpeningStockItemsAsync(int locationId)
        {
            var items = new List<OpeningStockItemModel>();

            try
            {
                using (var connection = GetConnection())
                using (var command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = @"
SELECT
    p.Id AS ProductId,
    p.Name AS ProductName,
    uom.Code AS UOM,
    ISNULL(SUM(ls.AvailableQuantity), 0) AS CurrentStock,
    ISNULL(p.StandardCost, 0) AS UnitCost
FROM [Inventory].[Product] p
INNER JOIN [Inventory].[UnitMeasure] uom ON uom.Id = p.UnitMeasureId
LEFT JOIN [Inventory].[LocationStock] ls
    ON ls.ProductId = p.Id
   AND ls.LocationId = @LocationId
WHERE p.IsActive = 1
GROUP BY p.Id, p.Name, uom.Code, p.StandardCost
ORDER BY p.Name;";

                    command.Parameters.Add("@LocationId", SqlDbType.Int).Value = locationId;

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            items.Add(MapOpeningStockItem(reader));
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while loading opening stock products.", ex);
            }

            return items;
        }

        private DataTable CreateLinesDataTable(List<StockTransferLine> lines)
        {
            var table = new DataTable();
            // These columns must match [Inventory].[StockTransferLineType] exactly order and type
            table.Columns.Add("ProductId", typeof(int));
            table.Columns.Add("BatchId", typeof(long));
            table.Columns.Add("Quantity", typeof(decimal));
            table.Columns.Add("UnitMeasureId", typeof(int));

            foreach (var line in lines)
            {
                table.Rows.Add(line.ProductId, line.BatchId, line.Quantity, line.UnitMeasureId);
            }

            return table;
        }

        private OpeningStockItemModel MapOpeningStockItem(IDataRecord record)
        {
            return new OpeningStockItemModel
            {
                ProductId = GetValue<int>(record, "ProductId"),
                ProductName = GetValue<string>(record, "ProductName"),
                DefaultUOM = GetValue<string>(record, "UOM"),
                CurrentStock = GetValue<decimal>(record, "CurrentStock"),
                OpeningQuantity = 0m,
                UnitCost = GetValue<decimal>(record, "UnitCost")
            };
        }

        private DataTable CreateOpeningStockDataTable(IEnumerable<OpeningStockItemModel> stockItems)
        {
            var table = new DataTable();
            table.Columns.Add("ProductId", typeof(int));
            table.Columns.Add("Quantity", typeof(decimal));
            table.Columns.Add("UnitCost", typeof(decimal));

            foreach (var item in stockItems.Where(x => x.OpeningQuantity > 0))
            {
                table.Rows.Add(item.ProductId, item.OpeningQuantity, item.UnitCost);
            }

            return table;
        }

        public async Task<IEnumerable<Location>> GetLocationsByBranchAsync(int branchId)
        {
            var locations = new List<Location>();

            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Inventory].[uspGetLocationsByBranch]"))
                    {
                        await connection.OpenAsync();

                        command.Parameters.Add("@BranchId", SqlDbType.Int).Value = branchId;
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                locations.Add(MapLocations(reader));
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occured while selecting the location.", ex);
            }
            return locations;
        }

        public async Task<IEnumerable<StockTransfer>> GetAllStockTransfersAsync(int branchId, DateTime? dateFrom, DateTime? dateTo)
        {
            var transfers = new List<StockTransfer>();

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Inventory].[uspGetAllStockTransfers]"))
                {
                    command.Parameters.AddWithValue("@BranchId", branchId);
                    command.Parameters.AddWithValue("@DateFrom", (object)dateFrom ?? DBNull.Value);
                    command.Parameters.AddWithValue("@DateTo", (object)dateTo ?? DBNull.Value);

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            transfers.Add(MapStockTransfer(reader));
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while retrieving stock transfer history.", ex);
            }

            return transfers;
        }

        public async Task<DataTable> GetStockTransferNoteReportAsync(long transferId)
        {
            if (transferId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(transferId), "A valid stock transfer ID is required.");
            }

            var reportTable = new DataTable("rptGetStockTransferNote");

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Inventory].[rptGetStockTransferNote]"))
                using (var adapter = new SqlDataAdapter(command))
                {
                    command.Parameters.Add("@TransferId", SqlDbType.BigInt).Value = transferId;

                    await connection.OpenAsync();
                    adapter.Fill(reportTable);
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"A database error occurred while loading Stock Transfer Note data for TransferId {transferId}.", ex);
            }

            return reportTable;
        }

        public async Task ProcessItemProvisioningAsync(
            int branchId,
            int locationId,
            int inputProductId,
            int inputUnitId,
            long? inputBatchId,
            decimal inputQty,
            decimal inputUnitCost,
            int createdBy,
            List<ProvisioningOutputModel> outputLines)
        {
            if (outputLines == null || !outputLines.Any())
            {
                throw new ArgumentException("At least one provisioning output line is required.", nameof(outputLines));
            }

            var outputLinesTable = CreateProvisioningLinesDataTable(outputLines);

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Inventory].[uspProcessItemProvisioning]"))
                {
                    command.Parameters.Add("@BranchId", SqlDbType.Int).Value = branchId;
                    command.Parameters.Add("@LocationId", SqlDbType.Int).Value = locationId;
                    command.Parameters.Add("@InputProductId", SqlDbType.Int).Value = inputProductId;
                    command.Parameters.Add("@InputUnitId", SqlDbType.Int).Value = inputUnitId;
                    command.Parameters.Add("@InputBatchId", SqlDbType.BigInt).Value = inputBatchId.HasValue ? (object)inputBatchId.Value : DBNull.Value;
                    command.Parameters.Add("@InputQty", SqlDbType.Decimal).Value = inputQty;
                    command.Parameters["@InputQty"].Precision = 18;
                    command.Parameters["@InputQty"].Scale = 3;
                    command.Parameters.Add("@InputUnitCost", SqlDbType.Decimal).Value = inputUnitCost;
                    command.Parameters["@InputUnitCost"].Precision = 18;
                    command.Parameters["@InputUnitCost"].Scale = 2;
                    command.Parameters.Add("@CreatedBy", SqlDbType.Int).Value = createdBy;

                    var linesParameter = command.Parameters.Add("@OutputLines", SqlDbType.Structured);
                    linesParameter.TypeName = "[Inventory].[tvpItemProvisioningLine]";
                    linesParameter.Value = outputLinesTable;

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (SqlException ex)
            {
                var batchValue = inputBatchId.HasValue ? inputBatchId.Value.ToString() : "NULL";
                throw new InvalidOperationException(
                    $"DB Error while processing item provisioning: {ex.Message} " +
                    $"Parameters: BranchId={branchId}, LocationId={locationId}, InputProductId={inputProductId}, " +
                    $"InputUnitId={inputUnitId}, InputBatchId={batchValue}, InputQty={inputQty}, " +
                    $"InputUnitCost={inputUnitCost}, CreatedBy={createdBy}, OutputLineCount={outputLinesTable.Rows.Count}.",
                    ex);
            }
        }

        public async Task<List<ProvisioningYieldModel>> GetProvisioningYieldReportAsync(int locationId, DateTime fromDate, DateTime toDate)
        {
            var report = new List<ProvisioningYieldModel>();

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Inventory].[uspGetProvisioningYieldReport]"))
                {
                    command.Parameters.Add("@LocationId", SqlDbType.Int).Value = locationId;
                    command.Parameters.Add("@FromDate", SqlDbType.DateTime2).Value = fromDate;
                    command.Parameters.Add("@ToDate", SqlDbType.DateTime2).Value = toDate;

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        var rowNumber = 0;
                        while (await reader.ReadAsync())
                        {
                            rowNumber++;

                            try
                            {
                                report.Add(new ProvisioningYieldModel
                                {
                                    ProvisionNumber = GetValue<string>(reader, "ProvisionNumber"),
                                    ProvisionDate = GetValue<DateTime>(reader, "ProvisionDate"),
                                    LocationName = GetValue<string>(reader, "LocationName"),
                                    InputProductName = GetValue<string>(reader, "InputProductName"),
                                    InputQty = GetValue<decimal>(reader, "InputQty"),
                                    InputUnit = reader["InputUnit"] == DBNull.Value ? string.Empty : reader["InputUnit"].ToString(),
                                    TotalInputCost = GetValue<decimal>(reader, "TotalInputCost"),
                                    TotalUsableQty = GetValue<decimal>(reader, "TotalUsableQty"),
                                    TotalWastageQty = GetValue<decimal>(reader, "TotalWastageQty"),
                                    YieldPercentage = GetValue<decimal>(reader, "YieldPercentage")
                                });
                            }
                            catch (Exception ex)
                            {
                                throw new InvalidOperationException(
                                    $"Failed to map provisioning yield report row {rowNumber}. " +
                                    "Expected columns: ProvisionNumber, ProvisionDate, LocationName, InputProductName, InputQty, InputUnit, TotalInputCost, TotalUsableQty, TotalWastageQty, YieldPercentage. " +
                                    $"Details: {ex.Message}",
                                    ex);
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"Failed to load provisioning yield report: {ex.Message}", ex);
            }

            return report;
        }

        public async Task<decimal> GetRetailItemStockAsync(int variantId, int locationId)
        {
            if (variantId <= 0 || locationId <= 0)
                return 0m;

            try
            {
                using (var connection = GetConnection())
                using (var command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = @"
                        SELECT ISNULL(SUM(ls.AvailableQuantity), 0)
                        FROM [Inventory].[Recipe] r
                        INNER JOIN [Inventory].[LocationStock] ls ON ls.ProductId = r.ProductId
                        WHERE r.VariantId = @VariantId AND ls.LocationId = @LocationId;";

                    command.Parameters.Add("@VariantId", SqlDbType.Int).Value = variantId;
                    command.Parameters.Add("@LocationId", SqlDbType.Int).Value = locationId;

                    await connection.OpenAsync();
                    var result = await command.ExecuteScalarAsync();
                    return result == null || result == DBNull.Value ? 0m : Convert.ToDecimal(result);
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("A database error occurred while checking retail item stock.", ex);
            }
        }

        public async Task<List<ProvisioningYieldDetailModel>> GetProvisioningYieldDetailsAsync(string provisionNumber)
        {
            var details = new List<ProvisioningYieldDetailModel>();

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Inventory].[uspGetProvisioningYieldDetails]"))
                {
                    command.Parameters.Add("@ProvisionNumber", SqlDbType.NVarChar, 50).Value = provisionNumber;

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        var rowNumber = 0;
                        while (await reader.ReadAsync())
                        {
                            rowNumber++;

                            try
                            {
                                details.Add(new ProvisioningYieldDetailModel
                                {
                                    OutputProductName = GetValue<string>(reader, "OutputProductName"),
                                    Unit = reader["Unit"] == DBNull.Value ? string.Empty : reader["Unit"].ToString(),
                                    OutputQty = GetValue<decimal>(reader, "OutputQty"),
                                    CostAllocationPercentage = GetValue<decimal>(reader, "CostAllocationPercentage"),
                                    CalculatedUnitCost = GetValue<decimal>(reader, "CalculatedUnitCost"),
                                    IsWastage = GetValue<bool>(reader, "IsWastage")
                                });
                            }
                            catch (Exception ex)
                            {
                                throw new InvalidOperationException(
                                    $"Failed to map provisioning yield detail row {rowNumber} for {provisionNumber}. " +
                                    "Expected columns: OutputProductName, Unit, OutputQty, CostAllocationPercentage, CalculatedUnitCost, IsWastage. " +
                                    $"Details: {ex.Message}",
                                    ex);
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"Failed to load provisioning yield details for {provisionNumber}: {ex.Message}", ex);
            }

            return details;
        }

        private StockTransfer MapStockTransfer(IDataRecord record)
        {
            return new StockTransfer
            {
                TransferId = GetValue<long>(record, "Id"),
                TransferNumber = GetValue<string>(record, "TransferNumber"),
                BranchId = GetValue<int>(record, "BranchId"),
                FromLocationId = GetValue<int>(record, "FromLocationId"),
                FromLocationName = GetValue<string>(record, "FromLocationName"),
                ToLocationId = GetValue<int>(record, "ToLocationId"),
                ToLocationName = GetValue<string>(record, "ToLocationName"),
                TransferDate = GetValue<DateTime>(record, "TransferDate"),
                Note = GetValue<string>(record, "Note"),
                Status = GetValue<string>(record, "Status"),
                CreatedBy = GetValue<int>(record, "CreatedBy"),
                Username = GetValue<string>(record, "Username"),
            };
        }

        private Location MapLocations(IDataRecord record)
        {
            return new Location
            {
                Id = GetValue<int>(record, "Id"),
                BranchId = GetValue<int>(record, "BranchId"),
                Name = GetValue<string>(record, "LocationName"),
            };
        }

        private DataTable CreateProvisioningLinesDataTable(IEnumerable<ProvisioningOutputModel> outputLines)
        {
            var table = new DataTable();
            table.Columns.Add("OutputProductId", typeof(int));
            table.Columns.Add("OutputUnitId", typeof(int));
            table.Columns.Add("OutputQty", typeof(decimal));
            table.Columns.Add("CostAllocationPercentage", typeof(decimal));
            table.Columns.Add("IsWastage", typeof(bool));

            foreach (var line in outputLines)
            {
                table.Rows.Add(
                    line.OutputProductId,
                    line.OutputUnitId,
                    line.OutputQty,
                    line.CostAllocationPercentage,
                    line.IsWastage);
            }

            return table;
        }
    }
}
