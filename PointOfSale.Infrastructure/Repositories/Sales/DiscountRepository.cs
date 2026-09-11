using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Sales;
using PointOfSale.Core.Models.Sales;

namespace PointOfSale.Infrastructure.Repositories.Sales
{
    public class DiscountRepository : BaseRepository, IDiscountRepository
    {
        public DiscountRepository(DatabaseConnection dbConnection) : base(dbConnection) { }

        public async Task<IEnumerable<DiscountDefinitionDto>> GetAllAsync()
        {
            var list = new List<DiscountDefinitionDto>();

            using (var connection = GetConnection())
            using (var command = CreateCommand(connection, "[Sales].[uspGetAllDiscounts]"))
            {
                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        list.Add(MapDiscount(reader));
                    }
                }

                await PopulateExcludedProductIdsAsync(connection, list);
            }

            return list;
        }

        public async Task<int> CreateAsync(DiscountDefinitionDto discount)
        {
            using (var connection = GetConnection())
            using (var command = CreateCommand(connection, "[Sales].[uspInsertDiscount]"))
            {
                AddDiscountParameters(command, discount);

                var id = command.Parameters.Add("@DiscountId", SqlDbType.Int);
                id.Direction = ParameterDirection.Output;

                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();

                return (int)id.Value;
            }
        }

        public async Task UpdateAsync(DiscountDefinitionDto discount)
        {
            using (var connection = GetConnection())
            using (var command = CreateCommand(connection, "[Sales].[uspUpdateDiscount]"))
            {
                command.Parameters.Add("@DiscountId", SqlDbType.Int).Value = discount.DiscountId;
                AddDiscountParameters(command, discount);

                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();
            }
        }

        public async Task<DiscountValidationResult> ValidateBillDiscountCodeAsync(string code, int branchId, decimal subTotal)
        {
            using (var connection = GetConnection())
            using (var command = CreateCommand(connection, "[Sales].[uspValidateBillDiscountCode]"))
            {
                command.Parameters.Add("@Code", SqlDbType.NVarChar, 50).Value = (object)code ?? DBNull.Value;
                command.Parameters.Add("@BranchId", SqlDbType.Int).Value = branchId;
                command.Parameters.Add("@SubTotal", SqlDbType.Decimal).Value = subTotal;
                command.Parameters["@SubTotal"].Precision = 18;
                command.Parameters["@SubTotal"].Scale = 2;

                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow))
                {
                    if (!await reader.ReadAsync())
                    {
                        return new DiscountValidationResult
                        {
                            IsValid = false,
                            Message = "Invalid discount code."
                        };
                    }

                    return new DiscountValidationResult
                    {
                        IsValid = GetValue<bool>(reader, "IsValid"),
                        Message = GetValue<string>(reader, "Message"),
                        DiscountId = GetValue<int>(reader, "DiscountId"),
                        Code = GetValue<string>(reader, "Code"),
                        Name = GetValue<string>(reader, "DiscountName"),
                        DiscountType = GetValue<string>(reader, "DiscountType"),
                        DiscountValue = GetValue<decimal>(reader, "DiscountValue"),
                        MinimumBillAmount = GetValue<decimal>(reader, "MinimumBillAmount"),
                        MaximumDiscountAmount = GetValue<decimal?>(reader, "MaximumDiscountAmount"),
                        IsSingleUse = GetValue<bool>(reader, "IsSingleUse")
                    };
                }
            }
        }

        public async Task<IEnumerable<DiscountDefinitionDto>> GetActiveAutoDiscountsAsync(int branchId, decimal subTotal)
        {
            var list = new List<DiscountDefinitionDto>();

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Sales].[uspGetActiveAutoDiscounts]"))
                {
                    command.Parameters.Add("@BranchId", SqlDbType.Int).Value = branchId;

                    var subTotalParameter = command.Parameters.Add("@SubTotal", SqlDbType.Decimal);
                    subTotalParameter.Precision = 18;
                    subTotalParameter.Scale = 2;
                    subTotalParameter.Value = subTotal;

                    await connection.OpenAsync();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(MapDiscount(reader));
                        }
                    }

                    await PopulateExcludedProductIdsAsync(connection, list);
                }
            }
            catch (SqlException ex) when (ex.Number == 2812) // Procedure not found
            {
                // Backward compatibility: older DBs may not have auto-discount SP yet.
                // Return empty list to keep sales screen functional.
                return list;
            }

            return list;
        }

        public async Task RedeemDiscountForSaleAsync(long salesId, int discountId, string code, int branchId, decimal subTotal, decimal discountAmount, int usedBy)
        {
            using (var connection = GetConnection())
            using (var command = CreateCommand(connection, "[Sales].[uspRedeemDiscountForSale]"))
            {
                command.Parameters.Add("@SalesId", SqlDbType.BigInt).Value = salesId;
                command.Parameters.Add("@DiscountId", SqlDbType.Int).Value = discountId;
                command.Parameters.Add("@Code", SqlDbType.NVarChar, 50).Value = (object)code ?? DBNull.Value;
                command.Parameters.Add("@BranchId", SqlDbType.Int).Value = branchId;
                command.Parameters.Add("@SubTotal", SqlDbType.Decimal).Value = subTotal;
                command.Parameters["@SubTotal"].Precision = 18;
                command.Parameters["@SubTotal"].Scale = 2;
                command.Parameters.Add("@DiscountAmount", SqlDbType.Decimal).Value = discountAmount;
                command.Parameters["@DiscountAmount"].Precision = 18;
                command.Parameters["@DiscountAmount"].Scale = 2;
                command.Parameters.Add("@UsedBy", SqlDbType.Int).Value = usedBy;

                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();
            }
        }

        private void AddDiscountParameters(SqlCommand command, DiscountDefinitionDto discount)
        {
            command.Parameters.Add("@Code", SqlDbType.NVarChar, 50).Value = discount.Code;
            command.Parameters.Add("@DiscountName", SqlDbType.NVarChar, 150).Value = discount.Name;
            command.Parameters.Add("@DiscountType", SqlDbType.VarChar, 10).Value = discount.DiscountType;
            command.Parameters.Add("@DiscountValue", SqlDbType.Decimal).Value = discount.DiscountValue;
            command.Parameters["@DiscountValue"].Precision = 18;
            command.Parameters["@DiscountValue"].Scale = 2;

            command.Parameters.Add("@MinimumBillAmount", SqlDbType.Decimal).Value = discount.MinimumBillAmount;
            command.Parameters["@MinimumBillAmount"].Precision = 18;
            command.Parameters["@MinimumBillAmount"].Scale = 2;

            command.Parameters.Add("@MaximumDiscountAmount", SqlDbType.Decimal).Value = (object)discount.MaximumDiscountAmount ?? DBNull.Value;
            command.Parameters["@MaximumDiscountAmount"].Precision = 18;
            command.Parameters["@MaximumDiscountAmount"].Scale = 2;

            command.Parameters.Add("@IsSingleUse", SqlDbType.Bit).Value = discount.IsSingleUse;
            command.Parameters.Add("@MaxRedemptionCount", SqlDbType.Int).Value = (object)discount.MaxRedemptionCount ?? DBNull.Value;
            command.Parameters.Add("@ValidFrom", SqlDbType.DateTime).Value = (object)discount.ValidFrom ?? DBNull.Value;
            command.Parameters.Add("@ValidTo", SqlDbType.DateTime).Value = (object)discount.ValidTo ?? DBNull.Value;
            command.Parameters.Add("@BranchId", SqlDbType.Int).Value = (object)discount.BranchId ?? DBNull.Value;
            command.Parameters.Add("@IsActive", SqlDbType.Bit).Value = discount.IsActive;
            command.Parameters.Add("@IsAutoApply", SqlDbType.Bit).Value = discount.IsAutoApply;
            command.Parameters.Add("@ApplyScope", SqlDbType.VarChar, 20).Value = (object)discount.ApplyScope ?? DBNull.Value;
            command.Parameters.Add("@TargetMenuCategoryId", SqlDbType.Int).Value = (object)discount.TargetMenuCategoryId ?? DBNull.Value;
            command.Parameters.Add("@DayOfWeekMask", SqlDbType.Int).Value = (object)discount.DayOfWeekMask ?? DBNull.Value;
            command.Parameters.Add("@StartTime", SqlDbType.Time).Value = (object)discount.StartTime ?? DBNull.Value;
            command.Parameters.Add("@EndTime", SqlDbType.Time).Value = (object)discount.EndTime ?? DBNull.Value;
            command.Parameters.Add("@CreatedBy", SqlDbType.Int).Value = discount.CreatedBy;

            var excludedProductsTable = new DataTable();
            excludedProductsTable.Columns.Add("ProductId", typeof(int));

            foreach (var productId in (discount.ExcludedProductIds ?? new List<int>())
                .Where(id => id > 0)
                .Distinct())
            {
                excludedProductsTable.Rows.Add(productId);
            }

            var excludedProductsParameter = command.Parameters.Add("@ExcludedProductIds", SqlDbType.Structured);
            excludedProductsParameter.TypeName = "Sales.ExcludedProductListType";
            excludedProductsParameter.Value = excludedProductsTable;
        }

        private DiscountDefinitionDto MapDiscount(IDataRecord record)
        {
            return new DiscountDefinitionDto
            {
                DiscountId = GetValue<int>(record, "DiscountId"),
                Code = GetValue<string>(record, "Code"),
                Name = GetValue<string>(record, "DiscountName"),
                DiscountType = GetValue<string>(record, "DiscountType"),
                DiscountValue = GetValue<decimal>(record, "DiscountValue"),
                MinimumBillAmount = GetValue<decimal>(record, "MinimumBillAmount"),
                MaximumDiscountAmount = GetValue<decimal?>(record, "MaximumDiscountAmount"),
                IsSingleUse = GetValue<bool>(record, "IsSingleUse"),
                MaxRedemptionCount = GetValue<int?>(record, "MaxRedemptionCount"),
                CurrentRedemptionCount = GetValue<int>(record, "CurrentRedemptionCount"),
                ValidFrom = GetValue<DateTime?>(record, "ValidFrom"),
                ValidTo = GetValue<DateTime?>(record, "ValidTo"),
                BranchId = GetValue<int?>(record, "BranchId"),
                IsActive = GetValue<bool>(record, "IsActive"),
                IsAutoApply = GetOptionalValue(record, "IsAutoApply", false),
                ApplyScope = GetOptionalValue<string>(record, "ApplyScope", null),
                TargetMenuCategoryId = GetOptionalValue<int?>(record, "TargetMenuCategoryId", null),
                DayOfWeekMask = GetOptionalValue<int?>(record, "DayOfWeekMask", null),
                StartTime = GetOptionalValue<TimeSpan?>(record, "StartTime", null),
                EndTime = GetOptionalValue<TimeSpan?>(record, "EndTime", null),
                CreatedBy = GetValue<int>(record, "CreatedBy"),
                CreatedAt = GetValue<DateTime>(record, "CreatedAt")
            };
        }

        private async Task PopulateExcludedProductIdsAsync(
            SqlConnection connection,
            IList<DiscountDefinitionDto> discounts)
        {
            if (discounts == null || discounts.Count == 0)
                return;

            var discountsById = discounts
                .Where(discount => discount != null && discount.DiscountId > 0)
                .GroupBy(discount => discount.DiscountId)
                .ToDictionary(group => group.Key, group => group.First());

            if (discountsById.Count == 0)
                return;

            using (var command = connection.CreateCommand())
            {
                var parameterNames = discountsById.Keys
                    .Select((discountId, index) => new
                    {
                        DiscountId = discountId,
                        Name = "@DiscountId" + index
                    })
                    .ToList();

                command.CommandType = CommandType.Text;
                command.CommandText = $@"
                    SELECT DiscountId, ProductId
                    FROM [Sales].[DiscountExcludedProduct]
                    WHERE DiscountId IN ({string.Join(", ", parameterNames.Select(parameter => parameter.Name))})
                    ORDER BY DiscountId, ProductId;";

                foreach (var parameter in parameterNames)
                    command.Parameters.Add(parameter.Name, SqlDbType.Int).Value = parameter.DiscountId;

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var discountId = GetValue<int>(reader, "DiscountId");
                        var productId = GetValue<int>(reader, "ProductId");

                        if (productId > 0 &&
                            discountsById.TryGetValue(discountId, out var discount) &&
                            !discount.ExcludedProductIds.Contains(productId))
                        {
                            discount.ExcludedProductIds.Add(productId);
                        }
                    }
                }
            }
        }

        private T GetOptionalValue<T>(IDataRecord record, string columnName, T defaultValue)
        {
            for (int i = 0; i < record.FieldCount; i++)
            {
                if (string.Equals(record.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
                    return GetValue<T>(record, columnName);
            }

            return defaultValue;
        }
    }
}
