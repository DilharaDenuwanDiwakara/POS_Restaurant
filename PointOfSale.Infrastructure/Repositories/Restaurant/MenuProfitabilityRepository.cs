using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Restaurant;

namespace PointOfSale.Infrastructure.Repositories.Restaurant
{
    public class MenuProfitabilityRepository : BaseRepository, IMenuProfitabilityRepository
    {
        public MenuProfitabilityRepository(DatabaseConnection databaseConnection) : base(databaseConnection)
        {
        }

        public async Task<List<MenuProfitabilityDto>> GetMenuProfitabilityAsync(int? categoryId)
        {
            var list = new List<MenuProfitabilityDto>();

            try
            {
                using (var connection = GetConnection())
                using (var command = CreateCommand(connection, "[Restaurant].[rptGetMenuProfitability]"))
                {
                    command.Parameters.Add("@CategoryId", SqlDbType.Int).Value =
                        categoryId.HasValue ? (object)categoryId.Value : DBNull.Value;

                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new MenuProfitabilityDto
                            {
                                CategoryName = GetValue<string>(reader, "CategoryName"),
                                MenuItemName = GetValue<string>(reader, "MenuItemName"),
                                ItemCode = GetValue<string>(reader, "ItemCode"),
                                VariantName = GetValue<string>(reader, "VariantName"),
                                TotalBOMCost = GetValue<decimal>(reader, "TotalBOMCost"),
                                SellingPrice = GetValue<decimal>(reader, "SellingPrice"),
                                GrossProfit = GetValue<decimal>(reader, "GrossProfit"),
                                FoodCostPercentage = GetValue<decimal>(reader, "FoodCostPercentage"),
                                BOMStatus = GetValue<string>(reader, "BOMStatus")
                            });
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("Failed to generate Menu Profitability report.", ex);
            }

            return list;
        }
    }
}
