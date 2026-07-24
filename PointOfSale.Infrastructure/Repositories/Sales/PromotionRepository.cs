using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Repositories.Sales;
using PointOfSale.Core.Models.Sales;

namespace PointOfSale.Infrastructure.Repositories.Sales
{
    public class PromotionRepository : BaseRepository, IPromotionRepository
    {
        public PromotionRepository(DatabaseConnection dbConnection) : base(dbConnection) { }

        public async Task<IEnumerable<PromotionRule>> GetAllAsync()
        {
            return await GetPromotionRulesAsync("[Sales].[uspGetAllPromotionRules]");
        }

        public async Task<IEnumerable<PromotionRule>> GetActiveAsync(int branchId, DateTime when)
        {
            return await GetPromotionRulesAsync("[Sales].[uspGetActivePromotionRules]", command =>
            {
                command.Parameters.Add("@BranchId", SqlDbType.Int).Value = branchId;
                command.Parameters.Add("@Now", SqlDbType.DateTime).Value = when;
            });
        }

        public async Task<int> CreateAsync(PromotionRule rule)
        {
            using (var connection = GetConnection())
            using (var command = CreateCommand(connection, "[Sales].[uspInsertPromotionRule]"))
            {
                AddParameters(command, rule);

                var id = command.Parameters.Add("@PromotionRuleId", SqlDbType.Int);
                id.Direction = ParameterDirection.Output;

                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();

                return (int)id.Value;
            }
        }

        public async Task UpdateAsync(PromotionRule rule)
        {
            using (var connection = GetConnection())
            using (var command = CreateCommand(connection, "[Sales].[uspUpdatePromotionRule]"))
            {
                command.Parameters.Add("@PromotionRuleId", SqlDbType.Int).Value = rule.PromotionRuleId;
                AddParameters(command, rule);

                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();
            }
        }

        private void AddParameters(SqlCommand command, PromotionRule rule)
        {
            command.Parameters.Add("@RuleName", SqlDbType.NVarChar, 150).Value = rule.RuleName;
            command.Parameters.Add("@PromotionType", SqlDbType.VarChar, 40).Value = rule.PromotionType;
            command.Parameters.Add("@BuyProductId", SqlDbType.Int).Value = rule.BuyProductId;
            command.Parameters.Add("@GetProductId", SqlDbType.Int).Value = (object)rule.GetProductId ?? DBNull.Value;
            command.Parameters.Add("@BuyQuantity", SqlDbType.Int).Value = rule.BuyQuantity;
            command.Parameters.Add("@GetQuantity", SqlDbType.Int).Value = rule.GetQuantity;
            command.Parameters.Add("@DiscountPercent", SqlDbType.Decimal).Value = rule.DiscountPercent;
            command.Parameters["@DiscountPercent"].Precision = 5;
            command.Parameters["@DiscountPercent"].Scale = 2;
            command.Parameters.Add("@DayOfWeekMask", SqlDbType.Int).Value = (object)rule.DayOfWeekMask ?? DBNull.Value;
            command.Parameters.Add("@ValidFrom", SqlDbType.DateTime).Value = (object)rule.ValidFrom ?? DBNull.Value;
            command.Parameters.Add("@ValidTo", SqlDbType.DateTime).Value = (object)rule.ValidTo ?? DBNull.Value;
            command.Parameters.Add("@BranchId", SqlDbType.Int).Value = (object)rule.BranchId ?? DBNull.Value;
            command.Parameters.Add("@IsActive", SqlDbType.Bit).Value = rule.IsActive;
            command.Parameters.Add("@CreatedBy", SqlDbType.Int).Value = rule.CreatedBy;
        }

        private async Task<IEnumerable<PromotionRule>> GetPromotionRulesAsync(string storedProcedure, Action<SqlCommand> configureCommand = null)
        {
            var rulesById = new Dictionary<int, PromotionRule>();

            using (var connection = GetConnection())
            using (var command = CreateCommand(connection, storedProcedure))
            {
                configureCommand?.Invoke(command);

                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var rule = MapRule(reader);
                        if (rule == null || rule.PromotionRuleId <= 0)
                            continue;

                        rulesById[rule.PromotionRuleId] = rule;
                    }
                }
            }

            return rulesById.Values
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.PromotionRuleId)
                .ToList();
        }

        private PromotionRule MapRule(IDataRecord record)
        {
            return new PromotionRule
            {
                PromotionRuleId = GetValue<int>(record, "PromotionRuleId"),
                RuleName = GetValue<string>(record, "RuleName"),
                PromotionType = GetValue<string>(record, "PromotionType"),
                BuyProductId = GetValue<int>(record, "BuyProductId"),
                GetProductId = GetValue<int?>(record, "GetProductId"),
                BuyQuantity = GetValue<int>(record, "BuyQuantity"),
                GetQuantity = GetValue<int>(record, "GetQuantity"),
                DiscountPercent = GetValue<decimal>(record, "DiscountPercent"),
                DayOfWeekMask = GetValue<int?>(record, "DayOfWeekMask"),
                ValidFrom = GetValue<DateTime?>(record, "ValidFrom"),
                ValidTo = GetValue<DateTime?>(record, "ValidTo"),
                BranchId = GetValue<int?>(record, "BranchId"),
                IsActive = GetValue<bool>(record, "IsActive"),
                CreatedBy = GetValue<int>(record, "CreatedBy"),
                CreatedAt = GetValue<DateTime>(record, "CreatedAt")
            };
        }
    }
}
