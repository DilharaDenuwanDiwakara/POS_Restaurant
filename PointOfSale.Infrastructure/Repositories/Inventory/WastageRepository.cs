using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Repositories.Inventory;
using PointOfSale.Core.Models.Inventory;

namespace PointOfSale.Infrastructure.Repositories.Inventory
{
    public class WastageRepository : BaseRepository, IWastageRepository
    {
        public WastageRepository(DatabaseConnection databaseConnection) : base(databaseConnection)
        {
        }

        public async Task<IEnumerable<WastageReason>> GetReasonsAsync()
        {
            var list = new List<WastageReason>();

            using (var connection = GetConnection())
            {
                using (var command = CreateCommand(connection, "SELECT Id, Name, IsActive FROM [Inventory].[WastageReason] WHERE IsActive = 1"))
                {
                    command.CommandType = CommandType.Text;

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new WastageReason
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                Reason = reader["Name"].ToString(),
                                IsActive = Convert.ToBoolean(reader["IsActive"])
                            });
                        }
                    }
                }
            }

            return list;
        }

        public async Task<long> CreateWastageAsync(Wastage wastage)
        {
            using (var connection = GetConnection())
            {
                await connection.OpenAsync();

                using (var command = CreateCommand(connection, "[Inventory].[uspInsertWastage]"))
                {

                    // 1. Add Header Parameters
                    command.Parameters.AddWithValue("@BranchId", wastage.BranchId);
                    command.Parameters.AddWithValue("@LocationId", wastage.LocationId);
                    command.Parameters.AddWithValue("@WastageDate", wastage.WastageDate);
                    // Handle nullable Note
                    command.Parameters.AddWithValue("@Note", (object)wastage.Note ?? DBNull.Value);
                    command.Parameters.AddWithValue("@CreatedBy", wastage.CreatedBy);

                    // 2. Prepare the Table Valued Parameter (The Lines)
                    DataTable table = new DataTable();
                    table.Columns.Add("ProductId", typeof(int));
                    table.Columns.Add("BatchId", typeof(long));
                    table.Columns.Add("UnitMeasureId", typeof(int));
                    table.Columns.Add("WastageReasonId", typeof(int));
                    table.Columns.Add("Quantity", typeof(decimal));
                    table.Columns.Add("UnitCost", typeof(decimal));

                    foreach (var line in wastage.Lines)
                    {
                        if (line.UnitMeasureId <= 0)
                        {
                            throw new InvalidOperationException($"Unit of measure is required for wastage item '{line.ProductName ?? line.ProductId.ToString()}'.");
                        }

                        var row = table.NewRow();
                        row["ProductId"] = line.ProductId;
                        row["BatchId"] = line.BatchId;
                        row["UnitMeasureId"] = line.UnitMeasureId;
                        row["WastageReasonId"] = (object)line.WastageReasonId ?? DBNull.Value;
                        row["Quantity"] = line.Quantity;
                        row["UnitCost"] = line.UnitCost;
                        table.Rows.Add(row);
                    }

                    var linesParam = command.Parameters.AddWithValue("@WastageLines", table);
                    linesParam.SqlDbType = SqlDbType.Structured;
                    linesParam.TypeName = "[Inventory].[WastageLineType]";

                    // 3. Add Output Parameter for ID
                    var outputId = new SqlParameter("@NewWastageId", SqlDbType.BigInt)
                    {
                        Direction = ParameterDirection.Output
                    };
                    command.Parameters.Add(outputId);

                    // 4. Execute
                    await command.ExecuteNonQueryAsync();

                    return (long)outputId.Value;
                }
            }
        }

        public async Task<List<WastageModel>> GetWastageHistory(int branchId, DateTime fromDate, DateTime toDate, int locationId)
        {
            var history = new List<WastageModel>();

            using (var connection = GetConnection())
            using (var command = CreateCommand(connection, "[Inventory].[uspGetWastageList]"))
            {
                command.Parameters.Add("@BranchId", SqlDbType.Int).Value = branchId;
                command.Parameters.Add("@FromDate", SqlDbType.Date).Value = fromDate.Date;
                command.Parameters.Add("@ToDate", SqlDbType.Date).Value = toDate.Date;
                command.Parameters.Add("@LocationId", SqlDbType.Int).Value = locationId;

                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        history.Add(new WastageModel
                        {
                            Id = GetValue<long>(reader, "Id"),
                            WastageNumber = GetValue<string>(reader, "WastageNumber"),
                            WastageDate = GetValue<DateTime>(reader, "WastageDate"),
                            LocationName = GetValue<string>(reader, "LocationName"),
                            Note = GetValue<string>(reader, "Note"),
                            Status = GetValue<string>(reader, "Status"),
                            CreatedBy = GetValue<string>(reader, "CreatedBy"),
                            CreatedDate = GetValue<DateTime>(reader, "CreatedDate"),
                            TotalAmount = GetValue<decimal>(reader, "TotalAmount")
                        });
                    }
                }
            }

            return history;
        }

        public async Task<List<WastageLineModel>> GetWastageLines(int wastageId)
        {
            var lines = new List<WastageLineModel>();

            using (var connection = GetConnection())
            using (var command = CreateCommand(connection, "[Inventory].[uspGetWastageLines]"))
            {
                command.Parameters.Add("@WastageId", SqlDbType.Int).Value = wastageId;

                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        lines.Add(new WastageLineModel
                        {
                            Id = GetValue<long>(reader, "Id"),
                            ProductName = GetValue<string>(reader, "ProductName"),
                            Reason = GetValue<string>(reader, "Reason"),
                            UOM = GetValue<string>(reader, "UOM"),
                            Quantity = GetValue<decimal>(reader, "Quantity"),
                            UnitCost = GetValue<decimal>(reader, "UnitCost"),
                            TotalCost = GetValue<decimal>(reader, "TotalCost")
                        });
                    }
                }
            }

            return lines;
        }

        public async Task<List<PendingWastageModel>> GetPendingWastageAsync(int branchId)
        {
            var queue = new List<PendingWastageModel>();

            using (var connection = GetConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.Text;
                command.CommandText = @"
SELECT
    w.[Id] AS [WastageId],
    w.[WastageNumber],
    loc.[Name] AS [LocationName],
    w.[WastageDate],
    COUNT(wl.[Id]) AS [TotalItems],
    COALESCE(NULLIF(LTRIM(RTRIM(u.[FullName])), ''), u.[Username], '') AS [RequestedBy],
    COALESCE(w.[Note], '') AS [Note],
    w.[Status],
    COALESCE(SUM(wl.[Quantity] * wl.[UnitCost]), 0) AS [TotalCost]
FROM [Inventory].[Wastage] w
INNER JOIN [Inventory].[Location] loc ON loc.[Id] = w.[LocationId]
LEFT JOIN [Inventory].[WastageLine] wl ON wl.[WastageId] = w.[Id]
LEFT JOIN [Auth].[User] u ON u.[Id] = w.[CreatedBy]
WHERE w.[BranchId] = @BranchId
  AND UPPER(LTRIM(RTRIM(w.[Status]))) = 'PENDING'
GROUP BY
    w.[Id],
    w.[WastageNumber],
    loc.[Name],
    w.[WastageDate],
    u.[FullName],
    u.[Username],
    w.[Note],
    w.[Status]
ORDER BY w.[WastageDate] DESC, w.[Id] DESC;";

                command.Parameters.Add("@BranchId", SqlDbType.Int).Value = branchId;

                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        queue.Add(new PendingWastageModel
                        {
                            WastageId = GetValue<long>(reader, "WastageId"),
                            WastageNumber = GetValue<string>(reader, "WastageNumber"),
                            LocationName = GetValue<string>(reader, "LocationName"),
                            WastageDate = GetValue<DateTime>(reader, "WastageDate"),
                            TotalItems = GetValue<int>(reader, "TotalItems"),
                            RequestedBy = GetValue<string>(reader, "RequestedBy"),
                            Note = GetValue<string>(reader, "Note"),
                            Status = GetValue<string>(reader, "Status"),
                            TotalCost = GetValue<decimal>(reader, "TotalCost")
                        });
                    }
                }
            }

            return queue;
        }

        public async Task<List<WastageDetailModel>> GetWastageDetailsAsync(long wastageId)
        {
            var details = new List<WastageDetailModel>();

            using (var connection = GetConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.Text;
                command.CommandText = @"
SELECT
    p.[Name] AS [ItemName],
    COALESCE(wr.[Name], '') AS [Reason],
    COALESCE(um.[Code], um.[Name], '') AS [UOM],
    wl.[Quantity],
    wl.[UnitCost],
    wl.[Quantity] * wl.[UnitCost] AS [TotalCost]
FROM [Inventory].[WastageLine] wl
INNER JOIN [Inventory].[Product] p ON p.[Id] = wl.[ProductId]
LEFT JOIN [Inventory].[WastageReason] wr ON wr.[Id] = wl.[WastageReasonId]
LEFT JOIN [Inventory].[UnitMeasure] um ON um.[Id] = wl.[UnitMeasureId]
WHERE wl.[WastageId] = @WastageId
ORDER BY p.[Name];";

                command.Parameters.Add("@WastageId", SqlDbType.BigInt).Value = wastageId;

                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        details.Add(new WastageDetailModel
                        {
                            ItemName = GetValue<string>(reader, "ItemName"),
                            Reason = GetValue<string>(reader, "Reason"),
                            UOM = GetValue<string>(reader, "UOM"),
                            Quantity = GetValue<decimal>(reader, "Quantity"),
                            UnitCost = GetValue<decimal>(reader, "UnitCost"),
                            TotalCost = GetValue<decimal>(reader, "TotalCost")
                        });
                    }
                }
            }

            return details;
        }

        public async Task ApproveWastageAsync(long wastageId, int approvedBy)
        {
            using (var connection = GetConnection())
            using (var command = CreateCommand(connection, "[Inventory].[uspApproveWastage]"))
            {
                command.Parameters.Add("@WastageId", SqlDbType.BigInt).Value = wastageId;
                command.Parameters.Add("@ApprovedBy", SqlDbType.Int).Value = approvedBy;

                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();
            }
        }

        public async Task RejectWastageAsync(long wastageId, int rejectedBy, string remarks)
        {
            using (var connection = GetConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.Text;
                command.CommandText = @"
UPDATE [Inventory].[Wastage]
SET [Status] = 'REJECTED',
    [Note] = CASE
        WHEN @Remarks IS NULL THEN [Note]
        WHEN [Note] IS NULL OR LTRIM(RTRIM([Note])) = '' THEN @Remarks
        ELSE [Note] + CHAR(13) + CHAR(10) + 'Rejection: ' + @Remarks
    END
WHERE [Id] = @WastageId
  AND UPPER(LTRIM(RTRIM([Status]))) = 'PENDING';";

                command.Parameters.Add("@WastageId", SqlDbType.BigInt).Value = wastageId;
                command.Parameters.Add("@RejectedBy", SqlDbType.Int).Value = rejectedBy;
                command.Parameters.Add("@Remarks", SqlDbType.NVarChar, 500).Value =
                    string.IsNullOrWhiteSpace(remarks) ? (object)DBNull.Value : remarks.Trim();

                await connection.OpenAsync();
                var affectedRows = await command.ExecuteNonQueryAsync();
                if (affectedRows == 0)
                {
                    throw new InvalidOperationException("The selected wastage document is no longer pending.");
                }
            }
        }

    }
}
