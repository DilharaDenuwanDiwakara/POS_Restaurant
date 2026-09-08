using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Repositories.Sales;
using PointOfSale.Core.Models.Sales;

namespace PointOfSale.Infrastructure.Repositories.Sales
{
    public class SalesHoldRepository : BaseRepository, ISalesHoldRepository
    {
        public SalesHoldRepository(DatabaseConnection databaseConnection) : base(databaseConnection) { }

        public async Task<List<SalesHold>> GetAllHoldsAsync(int locationId)
        {
            var list = new List<SalesHold>();
            using (var connection = GetConnection())
            {
                using (var command = CreateCommand(connection, "[Sales].[uspGetAllHolds]"))
                {
                    command.Parameters.Add("@LocationId", SqlDbType.Int).Value = locationId;

                    await connection.OpenAsync();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new SalesHold
                            {
                                HoldId = GetValue<long>(reader, "HoldId"),
                                HoldDate = GetValue<DateTime>(reader, "HoldDate"),
                                ReferenceNote = GetValue<string>(reader, "ReferenceNote"),
                                TotalAmount = GetValue<decimal>(reader, "TotalAmount")
                            });
                        }
                    }
                }
            }
            return list;
        }
        public async Task<SalesHold> RecallHoldAsync(long holdId)
        {
            SalesHold hold = null;

            using (var connection = GetConnection())
            {
                using (var command = CreateCommand(connection, "[Sales].[uspRecallHold]"))
                {
                    command.Parameters.AddWithValue("@HoldId", holdId);

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        // 1. Read Header (Result Set 1)
                        if (await reader.ReadAsync())
                        {
                            hold = new SalesHold
                            {
                                HoldId = GetValue<long>(reader, "HoldId"),
                                HoldDate = GetValue<DateTime>(reader, "HoldDate"),
                                ReferenceNote = GetValue<string>(reader, "ReferenceNote"),
                                TotalAmount = GetValue<decimal>(reader, "TotalAmount"),
                                CreatedBy = GetValue<int>(reader, "CreatedBy"),
                                Lines = new List<SalesHoldLine>()
                            };
                        }

                        // 2. Read Lines (Result Set 2)
                        if (hold != null && await reader.NextResultAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                hold.Lines.Add(new SalesHoldLine
                                {
                                    ProductId = GetValue<int>(reader, "ProductId"),
                                    BatchId = GetValue<long>(reader, "BatchId"),
                                    ProductCode = GetValue<string>(reader, "ProductCode"), // Joined from Product table
                                    ProductName = GetValue<string>(reader, "ProductName"), // Joined from Product table
                                    Quantity = GetValue<decimal>(reader, "Quantity"),
                                    UnitPrice = GetValue<decimal>(reader, "UnitPrice"),
                                    Discount = GetValue<decimal>(reader, "Discount")
                                });
                            }
                        }
                    }
                }
            }

            return hold;
        }
        public async Task SalesHoldAsync(SalesHold hold)
        {
            using (var connection = GetConnection())
            {
                using (var command = CreateCommand(connection, "[Sales].[uspHoldSale]"))
                {
                    command.CommandTimeout = 60;

                    command.Parameters.AddWithValue("@LocationId", hold.LocationId);
                    command.Parameters.AddWithValue("@HoldDate", hold.HoldDate);
                    command.Parameters.AddWithValue("@ReferenceNote", (object)hold.ReferenceNote ?? DBNull.Value);
                    command.Parameters.AddWithValue("@TotalAmount", hold.TotalAmount);
                    command.Parameters.AddWithValue("@CreatedBy", hold.CreatedBy);

                    // Add TVP for Lines
                    var linesTable = CreateHoldLinesTable(hold.Lines);
                    var pLines = command.Parameters.AddWithValue("@Lines", linesTable);
                    pLines.SqlDbType = SqlDbType.Structured;
                    pLines.TypeName = "[Sales].[SalesHoldLineType]";

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                }
            }
        }
        private DataTable CreateHoldLinesTable(List<SalesHoldLine> lines)
        {
            var table = new DataTable();
            table.Columns.Add("ProductId", typeof(int));
            table.Columns.Add("BatchId", typeof(long));
            table.Columns.Add("ProductCode", typeof(string));
            table.Columns.Add("ProductName", typeof(string));
            table.Columns.Add("Quantity", typeof(decimal));
            table.Columns.Add("UnitPrice", typeof(decimal));
            table.Columns.Add("Discount", typeof(decimal));

            foreach (var line in lines)
            {
                table.Rows.Add(line.ProductId, line.BatchId, line.ProductCode, line.ProductName, line.Quantity, line.UnitPrice, line.Discount);
            }
            return table;
        }
    }
}
