using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Sales;

namespace PointOfSale.Infrastructure.Repositories.Sales
{
    public class ShiftRepository : BaseRepository, IShiftRepository
    {
        public ShiftRepository(DatabaseConnection dbConnection) : base(dbConnection)
        {
        }

        public async Task<ShiftDto> GetActiveShiftAsync(int userId)
        {
            using (var connection = GetConnection())
            using (var command = CreateCommand(connection, "[Sales].[uspGetActiveShiftByUser]"))
            {
                command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;

                await connection.OpenAsync();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return MapShift(reader);
                    }
                }
            }

            return null;
        }

        public async Task<TillShiftStatusDto> CheckActiveShiftAsync(string machineName, int userId)
        {
            if (string.IsNullOrWhiteSpace(machineName))
            {
                throw new ArgumentException("Machine name is required.", nameof(machineName));
            }

            using (var connection = GetConnection())
            using (var command = CreateCommand(connection, "[Sales].[uspGetActiveTillShift]"))
            {
                command.Parameters.Add("@MachineName", SqlDbType.NVarChar, 100).Value = machineName.Trim();
                command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;

                await connection.OpenAsync();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return new TillShiftStatusDto
                        {
                            TillId = GetValue<int>(reader, "TillId"),
                            RegisterName = GetValue<string>(reader, "RegisterName"),
                            ShiftId = GetValue<int?>(reader, "ShiftId"),
                            RequiresFloat = GetValue<bool>(reader, "RequiresFloat")
                        };
                    }
                }
            }

            return null;
        }

        public async Task<IEnumerable<ShiftDto>> GetShiftsForReconciliationAsync(int branchId)
        {
            var shifts = new List<ShiftDto>();

            using (var connection = GetConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.Text;
                command.CommandText = @"
SELECT
    s.[Id],
    s.[UserId],
    s.[BranchId],
    s.[TillId],
    pr.[RegisterName] AS [TillName],
    COALESCE(NULLIF(LTRIM(RTRIM(u.[FullName])), ''), u.[Username]) AS [CashierName],
    s.[StartTime],
    s.[EndTime],
    s.[StartingFloat],
    s.[Status]
FROM [Sales].[Shift] s
LEFT JOIN [System].[POSRegister] pr ON pr.[Id] = s.[TillId]
LEFT JOIN [Auth].[User] u ON u.[Id] = s.[UserId]
WHERE s.[BranchId] = @BranchId
  AND EXISTS (
      SELECT 1
      FROM [Sales].[ShiftReconciliation] sr
      WHERE sr.[ShiftId] = s.[Id]
  )
ORDER BY s.[EndTime] DESC, s.[StartTime] DESC;";

                command.Parameters.Add("@BranchId", SqlDbType.Int).Value = branchId;

                await connection.OpenAsync();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        shifts.Add(MapShift(reader));
                    }
                }
            }

            return shifts;
        }

        public async Task OpenShiftAsync(ShiftDto shift)
        {
            if (shift == null)
            {
                throw new ArgumentNullException(nameof(shift));
            }

            using (var connection = GetConnection())
            using (var command = CreateCommand(connection, "[Sales].[uspOpenShift]"))
            {
                command.Parameters.Add("@UserId", SqlDbType.Int).Value = shift.UserId;
                command.Parameters.Add("@BranchId", SqlDbType.Int).Value = shift.BranchId;
                command.Parameters.Add("@TillId", SqlDbType.Int).Value = (object)shift.TillId ?? DBNull.Value;
                command.Parameters.Add("@RegisterId", SqlDbType.Int).Value = (object)shift.TillId ?? DBNull.Value;
                command.Parameters.Add("@StartingFloat", SqlDbType.Decimal).Value = shift.StartingFloat;
                command.Parameters["@StartingFloat"].Precision = 18;
                command.Parameters["@StartingFloat"].Scale = 2;

                await connection.OpenAsync();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        var dbShift = MapShift(reader);
                        shift.Id = dbShift.Id;
                        shift.UserId = dbShift.UserId;
                        shift.BranchId = dbShift.BranchId;
                        shift.TillId = dbShift.TillId;
                        shift.TillName = dbShift.TillName;
                        shift.CashierName = dbShift.CashierName;
                        shift.StartTime = dbShift.StartTime;
                        shift.EndTime = dbShift.EndTime;
                        shift.StartingFloat = dbShift.StartingFloat;
                        shift.Status = dbShift.Status;
                    }
                }
            }
        }

        public async Task<ShiftReconciliationDto> GetShiftReconciliationAsync(int shiftId)
        {
            using (var connection = GetConnection())
            {
                await connection.OpenAsync();

                var hasId = await ColumnExistsAsync(connection, "Sales.ShiftReconciliation", "Id");
                var hasNotes = await ColumnExistsAsync(connection, "Sales.ShiftReconciliation", "Notes");
                var hasManagerNotes = await ColumnExistsAsync(connection, "Sales.ShiftReconciliation", "ManagerNotes");

                ShiftReconciliationDto dto = null;

                using (var command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = $@"
SELECT TOP (1)
    {(hasId ? "[Id]" : "CAST(0 AS INT) AS [Id]")},
    [ShiftId],
    [SystemCash],
    [PhysicalCash],
    [Variance],
    {(hasManagerNotes ? "[ManagerNotes] AS [Notes]" : hasNotes ? "[Notes]" : "CAST(NULL AS NVARCHAR(MAX)) AS [Notes]")}
FROM [Sales].[ShiftReconciliation]
WHERE [ShiftId] = @ShiftId
ORDER BY {(hasId ? "[Id] DESC" : "[ShiftId] DESC")};";

                    command.Parameters.Add("@ShiftId", SqlDbType.Int).Value = shiftId;

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            dto = MapShiftReconciliation(reader, shiftId);
                        }
                    }
                }

                if (dto != null)
                {
                    await EnrichWithZReportTotalsAsync(connection, dto, shiftId);
                }

                return dto;
            }
        }

        private async Task EnrichWithZReportTotalsAsync(IDbConnection connection, ShiftReconciliationDto dto, int shiftId)
        {
            try
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandText = "[Sales].[rptGetShiftZReport]";
                    AddParameter(command, "@ShiftId", DbType.Int32, shiftId);

                    var sqlCommand = command as global::System.Data.SqlClient.SqlCommand;
                    if (sqlCommand == null)
                    {
                        return;
                    }

                    using (var reader = await sqlCommand.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            dto.ExpectedCardTotal = HasColumn(reader, "ExpectedCardTotal")
                                ? GetValue<decimal>(reader, "ExpectedCardTotal")
                                : 0m;
                            dto.ExpectedCreditTotal = HasColumn(reader, "ExpectedCreditTotal")
                                ? GetValue<decimal>(reader, "ExpectedCreditTotal")
                                : 0m;
                        }
                    }
                }
            }
            catch
            {
                // SP may not yet be deployed on this environment; totals default to zero.
            }
        }

        public async Task<ShiftReconciliationDto> CloseShiftAsync(int shiftId, int userId, decimal physicalCashDrop)
        {
            using (var connection = GetConnection())
            using (var command = CreateCommand(connection, "[Sales].[uspCloseShift]"))
            {
                command.Parameters.Add("@ShiftId", SqlDbType.Int).Value = shiftId;
                command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                command.Parameters.Add("@PhysicalCashDrop", SqlDbType.Decimal).Value = physicalCashDrop;
                command.Parameters["@PhysicalCashDrop"].Precision = 18;
                command.Parameters["@PhysicalCashDrop"].Scale = 2;

                await connection.OpenAsync();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return MapShiftReconciliation(reader, shiftId);
                    }
                }
            }

            return null;
        }

        public Task<ShiftReconciliationDto> CloseShiftAndGenerateZReportAsync(int shiftId, int userId, decimal physicalCash)
        {
            return CloseShiftAsync(shiftId, userId, physicalCash);
        }

        public DataSet GetZReportDataSet(int shiftId)
        {
            if (shiftId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(shiftId), "A valid shift id is required.");
            }

            var dataSet = new DataSet("ZReportDataSet");

            using (var connection = GetConnection())
            using (var command = CreateCommand(connection, "[Sales].[uspGetZReport]"))
            using (var adapter = new SqlDataAdapter(command))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add("@ShiftId", SqlDbType.Int).Value = shiftId;
                command.CommandTimeout = 120;

                adapter.Fill(dataSet);
            }

            RenameZReportTables(dataSet);

            return dataSet;
        }

        public async Task PostShiftToLedgerAsync(int shiftId, int userId, string managerNotes)
        {
            using (var connection = GetConnection())
            {
                await connection.OpenAsync();

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        if (!await ShiftReconciliationExistsAsync(connection, transaction, shiftId))
                        {
                            throw new InvalidOperationException("No reconciliation record was found for the selected shift.");
                        }

                        await UpdateShiftReconciliationForLedgerAsync(connection, transaction, shiftId, userId, managerNotes);
                        await FinalizeShiftAsync(connection, transaction, shiftId);

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private ShiftDto MapShift(IDataRecord record)
        {
            return new ShiftDto
            {
                Id = GetValue<int>(record, "Id"),
                UserId = GetValue<int>(record, "UserId"),
                BranchId = GetValue<int>(record, "BranchId"),
                TillId = GetValue<int?>(record, "TillId"),
                TillName = TryGetValue(record, "TillName"),
                CashierName = TryGetValue(record, "CashierName"),
                StartTime = GetValue<DateTime>(record, "StartTime"),
                EndTime = GetValue<DateTime?>(record, "EndTime"),
                StartingFloat = GetValue<decimal>(record, "StartingFloat"),
                Status = GetValue<string>(record, "Status")
            };
        }

        private string TryGetValue(IDataRecord record, string columnName)
        {
            try
            {
                return GetValue<string>(record, columnName);
            }
            catch (IndexOutOfRangeException)
            {
                return null;
            }
        }

        private ShiftReconciliationDto MapShiftReconciliation(IDataRecord record, int fallbackShiftId)
        {
            return new ShiftReconciliationDto
            {
                Id = HasColumn(record, "Id") ? GetValue<int>(record, "Id") : 0,
                ShiftId = HasColumn(record, "ShiftId") ? GetValue<int>(record, "ShiftId") : fallbackShiftId,
                SystemCash = GetValue<decimal>(record, "SystemCash"),
                PhysicalCash = GetValue<decimal>(record, "PhysicalCash"),
                Variance = GetValue<decimal>(record, "Variance"),
                Notes = HasColumn(record, "Notes") ? GetValue<string>(record, "Notes") : null
            };
        }

        private static void RenameZReportTables(DataSet dataSet)
        {
            var tableNames = new[]
            {
                "Company",
                "ShiftHeader",
                "SalesSummary",
                "TenderSummary",
                "ItemSummary"
            };

            for (int i = 0; i < dataSet.Tables.Count && i < tableNames.Length; i++)
            {
                dataSet.Tables[i].TableName = tableNames[i];
            }
        }

        private async Task<bool> ShiftReconciliationExistsAsync(IDbConnection connection, IDbTransaction transaction, int shiftId)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandType = CommandType.Text;
                command.CommandText = @"
SELECT CASE WHEN EXISTS (
    SELECT 1
    FROM [Sales].[ShiftReconciliation]
    WHERE [ShiftId] = @ShiftId
) THEN 1 ELSE 0 END;";

                var parameter = command.CreateParameter();
                parameter.ParameterName = "@ShiftId";
                parameter.DbType = DbType.Int32;
                parameter.Value = shiftId;
                command.Parameters.Add(parameter);

                return Convert.ToInt32(await ExecuteScalarAsync(command)) == 1;
            }
        }

        private async Task UpdateShiftReconciliationForLedgerAsync(IDbConnection connection, IDbTransaction transaction, int shiftId, int userId, string managerNotes)
        {
            var setClauses = new List<string>();

            if (await ColumnExistsAsync(connection, transaction, "Sales.ShiftReconciliation", "ManagerNotes"))
            {
                setClauses.Add("[ManagerNotes] = @ManagerNotes");
            }
            else if (await ColumnExistsAsync(connection, transaction, "Sales.ShiftReconciliation", "Notes"))
            {
                setClauses.Add("[Notes] = @ManagerNotes");
            }

            if (await ColumnExistsAsync(connection, transaction, "Sales.ShiftReconciliation", "IsFinalized"))
            {
                setClauses.Add("[IsFinalized] = 1");
            }

            if (await ColumnExistsAsync(connection, transaction, "Sales.ShiftReconciliation", "FinalizedBy"))
            {
                setClauses.Add("[FinalizedBy] = @UserId");
            }

            if (await ColumnExistsAsync(connection, transaction, "Sales.ShiftReconciliation", "FinalizedAt"))
            {
                setClauses.Add("[FinalizedAt] = GETDATE()");
            }

            if (await ColumnExistsAsync(connection, transaction, "Sales.ShiftReconciliation", "ReadyForLedger"))
            {
                setClauses.Add("[ReadyForLedger] = 1");
            }

            if (await ColumnExistsAsync(connection, transaction, "Sales.ShiftReconciliation", "LedgerStatus"))
            {
                setClauses.Add("[LedgerStatus] = 'READY'");
            }

            if (await ColumnExistsAsync(connection, transaction, "Sales.ShiftReconciliation", "Status"))
            {
                setClauses.Add("[Status] = 'FINALIZED'");
            }

            if (!setClauses.Any())
            {
                return;
            }

            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandType = CommandType.Text;
                command.CommandText = $@"
UPDATE [Sales].[ShiftReconciliation]
SET {string.Join(", ", setClauses)}
WHERE [ShiftId] = @ShiftId;";

                AddParameter(command, "@ShiftId", DbType.Int32, shiftId);
                AddParameter(command, "@UserId", DbType.Int32, userId);
                AddParameter(command, "@ManagerNotes", DbType.String, string.IsNullOrWhiteSpace(managerNotes) ? (object)DBNull.Value : managerNotes.Trim());

                await ExecuteNonQueryAsync(command);
            }
        }

        private async Task FinalizeShiftAsync(IDbConnection connection, IDbTransaction transaction, int shiftId)
        {
            if (!await ColumnExistsAsync(connection, transaction, "Sales.Shift", "Status"))
            {
                return;
            }

            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandType = CommandType.Text;
                command.CommandText = @"
UPDATE [Sales].[Shift]
SET [Status] = 'FINALIZED'
WHERE [Id] = @ShiftId;";

                AddParameter(command, "@ShiftId", DbType.Int32, shiftId);

                await ExecuteNonQueryAsync(command);
            }
        }

        private async Task<bool> ColumnExistsAsync(IDbConnection connection, string tableName, string columnName)
        {
            return await ColumnExistsAsync(connection, null, tableName, columnName);
        }

        private async Task<bool> ColumnExistsAsync(IDbConnection connection, IDbTransaction transaction, string tableName, string columnName)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandType = CommandType.Text;
                command.CommandText = "SELECT CASE WHEN COL_LENGTH(@TableName, @ColumnName) IS NULL THEN 0 ELSE 1 END;";

                AddParameter(command, "@TableName", DbType.String, tableName);
                AddParameter(command, "@ColumnName", DbType.String, columnName);

                return Convert.ToInt32(await ExecuteScalarAsync(command)) == 1;
            }
        }

        private static void AddParameter(IDbCommand command, string name, DbType dbType, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.DbType = dbType;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        private static async Task<object> ExecuteScalarAsync(IDbCommand command)
        {
            var sqlCommand = command as global::System.Data.SqlClient.SqlCommand;
            if (sqlCommand != null)
            {
                return await sqlCommand.ExecuteScalarAsync();
            }

            return command.ExecuteScalar();
        }

        private static async Task ExecuteNonQueryAsync(IDbCommand command)
        {
            var sqlCommand = command as global::System.Data.SqlClient.SqlCommand;
            if (sqlCommand != null)
            {
                await sqlCommand.ExecuteNonQueryAsync();
                return;
            }

            command.ExecuteNonQuery();
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
    }
}
