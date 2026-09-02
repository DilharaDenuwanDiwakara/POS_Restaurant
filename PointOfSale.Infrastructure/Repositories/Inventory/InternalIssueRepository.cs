using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Inventory;

namespace PointOfSale.Infrastructure.Repositories.Inventory
{
    /// <summary>
    /// Provides ADO.NET persistence for internal stock issue transactions.
    /// </summary>
    public class InternalIssueRepository : BaseRepository, IInternalIssueRepository
    {
        public InternalIssueRepository(DatabaseConnection databaseConnection) : base(databaseConnection)
        {
        }

        /// <summary>
        /// Creates a new internal stock issue using the Inventory.uspInsertInternalIssue stored procedure.
        /// </summary>
        /// <param name="dto">The internal issue header and line details to save.</param>
        /// <returns>The newly generated internal issue identifier and issue number.</returns>
        public async Task<InternalIssueSaveResultDto> CreateInternalIssueAsync(InternalIssueSaveDto dto)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            using (var connection = GetConnection())
            using (var command = CreateCommand(connection, "[Inventory].[uspInsertInternalIssue]"))
            {
                AddHeaderParameters(command, dto);
                AddLinesParameter(command, dto.Lines);
                AddOutputParameter(command);

                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();

                return new InternalIssueSaveResultDto
                {
                    InternalIssueId = Convert.ToInt32(command.Parameters["@NewIssueId"].Value),
                    IssueNumber = Convert.ToString(command.Parameters["@IssueNumber"].Value)
                };
            }
        }

        /// <summary>
        /// Gets the print-ready voucher data for a saved internal stock issue using
        /// Inventory.uspGetInternalIssueVoucher.
        /// </summary>
        public async Task<DataTable> GetInternalIssueVoucherAsync(int internalIssueId)
        {
            if (internalIssueId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(internalIssueId), "A valid internal issue ID is required.");
            }

            var reportTable = new DataTable("uspGetInternalIssueNote");

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Inventory].[uspGetInternalIssueNote]"))
                using (var adapter = new SqlDataAdapter(command))
                {
                    command.Parameters.Add("@InternalIssueId", SqlDbType.Int).Value = internalIssueId;

                    await connection.OpenAsync();
                    adapter.Fill(reportTable);
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException(
                    $"A database error occurred while loading Internal Issue Voucher data for InternalIssueId {internalIssueId}.", ex);
            }

            return reportTable;
        }

        private static void AddHeaderParameters(SqlCommand command, InternalIssueSaveDto dto)
        {
            command.Parameters.Add("@IssueDate", SqlDbType.DateTime).Value = dto.IssueDate;
            command.Parameters.Add("@IssueType", SqlDbType.NVarChar, 50).Value =
                string.IsNullOrWhiteSpace(dto.IssueType) ? (object)DBNull.Value : dto.IssueType.Trim();
            command.Parameters.Add("@StationId", SqlDbType.Int).Value = dto.StationId;
            command.Parameters.Add("@BranchId", SqlDbType.Int).Value = dto.BranchId;
            command.Parameters.Add("@LocationId", SqlDbType.Int).Value = dto.LocationId;
            AddDecimalParameter(command, "@TotalValue", dto.TotalValue);
            command.Parameters.Add("@Remarks", SqlDbType.NVarChar, 255).Value =
                string.IsNullOrWhiteSpace(dto.Remarks) ? (object)DBNull.Value : dto.Remarks.Trim();
            command.Parameters.Add("@CreatedBy", SqlDbType.Int).Value = dto.CreatedBy;
            command.Parameters.Add("@TargetAccountId", SqlDbType.Int).Value = (object)dto.TargetAccountId ?? DBNull.Value;
        }

        private static void AddLinesParameter(SqlCommand command, IEnumerable<InternalIssueLineDto> lines)
        {
            var table = new DataTable();
            table.Columns.Add("ProductId", typeof(int));
            table.Columns.Add("VariantId", typeof(int));
            table.Columns.Add("Qty", typeof(decimal));
            table.Columns.Add("UnitCost", typeof(decimal));
            table.Columns.Add("LineTotal", typeof(decimal));

            if (lines != null)
            {
                foreach (var line in lines)
                {
                    table.Rows.Add(
                        (object)line.ProductId ?? DBNull.Value,
                        (object)line.VariantId ?? DBNull.Value,
                        line.Qty,
                        line.UnitCost,
                        line.LineTotal);
                }
            }

            var linesParameter = command.Parameters.Add("@IssueLines", SqlDbType.Structured);
            linesParameter.TypeName = "Inventory.InternalIssueLineType";
            linesParameter.Value = table;
        }

        private static void AddOutputParameter(SqlCommand command)
        {
            var outputParameter = new SqlParameter("@NewIssueId", SqlDbType.Int)
            {
                Direction = ParameterDirection.Output
            };

            command.Parameters.Add(outputParameter);

            var issueNumberParameter = new SqlParameter("@IssueNumber", SqlDbType.NVarChar, 50)
            {
                Direction = ParameterDirection.Output
            };

            command.Parameters.Add(issueNumberParameter);
        }

        private static void AddDecimalParameter(SqlCommand command, string parameterName, decimal value)
        {
            var parameter = command.Parameters.Add(parameterName, SqlDbType.Decimal);
            parameter.Precision = 18;
            parameter.Scale = 2;
            parameter.Value = value;
        }
    }
}
