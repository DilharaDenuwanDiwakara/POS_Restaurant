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
                    table.Columns.Add("WastageReasonId", typeof(int));
                    table.Columns.Add("Quantity", typeof(decimal));
                    table.Columns.Add("UnitCost", typeof(decimal));

                    foreach (var line in wastage.Lines)
                    {
                        table.Rows.Add(line.ProductId, line.BatchId, line.WastageReasonId, line.Quantity, line.UnitCost);
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

    }
}
