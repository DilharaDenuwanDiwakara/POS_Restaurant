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
        /// Gets the raw product ingredients for a menu item or variant, using current product cost
        /// and converting recipe quantities to the product stock/base unit.
        /// </summary>
        public async Task<IEnumerable<RecipeIngredientDto>> GetRecipeIngredientsForInternalIssueAsync(int? menuItemId, int? variantId)
        {
            if ((!menuItemId.HasValue || menuItemId.Value <= 0) &&
                (!variantId.HasValue || variantId.Value <= 0))
            {
                throw new ArgumentException("Either a menu item or variant is required to fetch recipe ingredients.");
            }

            var result = new List<RecipeIngredientDto>();

            const string sql = @"
DECLARE @ResolvedVariantId INT = @VariantId;

IF (@ResolvedVariantId IS NULL AND @MenuItemId IS NOT NULL)
BEGIN
    SELECT TOP (1) @ResolvedVariantId = v.Id
    FROM [Restaurant].[Variant] v
    WHERE v.MenuItemId = @MenuItemId
    ORDER BY
        CASE WHEN UPPER(LTRIM(RTRIM(v.Name))) = 'STANDARD' THEN 0 ELSE 1 END,
        v.Id;
END;

SELECT
    v.MenuItemId,
    r.VariantId,
    r.ProductId,
    p.Name AS ProductName,
    CAST(
        CASE
            WHEN ISNULL(r.UnitMeasureId, p.UnitMeasureId) = p.UnitMeasureId THEN r.QuantityRequired
            WHEN puc.Id IS NULL THEN NULL
            WHEN puc.IsMultiply = 1 THEN r.QuantityRequired * puc.ConversionRate
            ELSE r.QuantityRequired / NULLIF(puc.ConversionRate, 0)
        END AS DECIMAL(18, 3)) AS QuantityPerItem,
    p.StandardCost AS UnitCost,
    p.UnitMeasureId,
    COALESCE(um.Code, um.Name) AS UnitMeasureName
FROM [Inventory].[Recipe] r
INNER JOIN [Restaurant].[Variant] v ON v.Id = r.VariantId
INNER JOIN [Inventory].[Product] p ON p.Id = r.ProductId
LEFT JOIN [Inventory].[UnitMeasure] um ON um.Id = p.UnitMeasureId
LEFT JOIN [Inventory].[ProductUnitConversion] puc
    ON puc.ProductId = r.ProductId
    AND puc.TargetUnitMeasureId = r.UnitMeasureId
    AND puc.IsActive = 1
WHERE
    r.VariantId = @ResolvedVariantId
    AND p.IsActive = 1
ORDER BY r.Id;";

            using (var connection = GetConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.Text;
                command.CommandText = sql;
                command.Parameters.Add("@MenuItemId", SqlDbType.Int).Value = (object)menuItemId ?? DBNull.Value;
                command.Parameters.Add("@VariantId", SqlDbType.Int).Value = (object)variantId ?? DBNull.Value;

                await connection.OpenAsync();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var quantityPerItem = GetValue<decimal?>(reader, "QuantityPerItem");
                        if (!quantityPerItem.HasValue)
                        {
                            throw new InvalidOperationException(
                                $"No active unit conversion was found for recipe ingredient '{GetValue<string>(reader, "ProductName")}'.");
                        }

                        result.Add(new RecipeIngredientDto
                        {
                            MenuItemId = GetValue<int>(reader, "MenuItemId"),
                            VariantId = GetValue<int>(reader, "VariantId"),
                            ProductId = GetValue<int>(reader, "ProductId"),
                            ProductName = GetValue<string>(reader, "ProductName"),
                            QuantityPerItem = quantityPerItem.Value,
                            UnitCost = GetValue<decimal>(reader, "UnitCost"),
                            UnitMeasureId = GetValue<int>(reader, "UnitMeasureId"),
                            UnitMeasureName = GetValue<string>(reader, "UnitMeasureName")
                        });
                    }
                }
            }

            return result;
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
            command.Parameters.Add("@WastageAccountId", SqlDbType.Int).Value = (object)dto.WastageAccountId ?? DBNull.Value;
        }

        private static void AddLinesParameter(SqlCommand command, IEnumerable<InternalIssueLineDto> lines)
        {
            var table = new DataTable();
            table.Columns.Add("ProductId", typeof(int));
            table.Columns.Add("Qty", typeof(decimal));
            table.Columns.Add("UnitCost", typeof(decimal));
            table.Columns.Add("LineTotal", typeof(decimal));

            if (lines != null)
            {
                foreach (var line in lines)
                {
                    table.Rows.Add(line.ProductId, line.Qty, line.UnitCost, line.LineTotal);
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
