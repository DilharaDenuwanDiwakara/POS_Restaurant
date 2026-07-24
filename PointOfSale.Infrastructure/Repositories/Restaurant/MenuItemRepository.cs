using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Restaurant;
using PointOfSale.Core.Models.Restaurant;

namespace PointOfSale.Infrastructure.Repositories.Restaurant
{
    public class MenuItemRepository : BaseRepository, IMenuItemRepository
    {
        public MenuItemRepository(DatabaseConnection databaseConnection) : base(databaseConnection)
        {
        }

        public async Task<int> CreateAsync(MenuItem menuItem, IEnumerable<int> taxIds, int createdBy)
        {
            using (var connection = GetConnection())
            {
                await connection.OpenAsync();

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // 1. INSERT HEADER (Menu Item)
                        int newItemId;

                        // Use the NEW overload passing 'transaction'
                        using (var cmd = CreateCommand(connection, "[Restaurant].[uspInsertMenuItem]", transaction))
                        {
                            AddMenuItemParameters(cmd, menuItem, createdBy);
                            AddMenuItemTaxParameters(cmd, taxIds);

                            var paramId = cmd.Parameters.Add("@Id", SqlDbType.Int);
                            paramId.Direction = ParameterDirection.Output;

                            await cmd.ExecuteNonQueryAsync();
                            newItemId = (int)paramId.Value;
                        }

                        // 2. INSERT VARIANTS (Loop)
                        foreach (var variant in menuItem.Variants)
                        {
                            int newVariantId;

                            using (var cmdVar = CreateCommand(connection, "[Restaurant].[uspInsertVariant]", transaction))
                            {
                                cmdVar.Parameters.Add("@MenuItemId", SqlDbType.Int).Value = newItemId;
                                AddVariantParameters(cmdVar, variant, createdBy);

                                var paramVarId = cmdVar.Parameters.Add("@Id", SqlDbType.Int);
                                paramVarId.Direction = ParameterDirection.Output;

                                await cmdVar.ExecuteNonQueryAsync();
                                newVariantId = (int)paramVarId.Value;
                            }

                            // 3. INSERT RECIPE LINES (Nested Loop)
                            // These belong to the specific variant we just created
                            foreach (var line in variant.RecipeLines)
                            {
                                using (var cmdRecipe = CreateCommand(connection, "[Restaurant].[uspInsertRecipe]", transaction))
                                {
                                    cmdRecipe.Parameters.Add("@VariantId", SqlDbType.Int).Value = newVariantId;
                                    AddRecipeParameters(cmdRecipe, line);

                                    await cmdRecipe.ExecuteNonQueryAsync();
                                }
                            }
                        }

                        // If we reach here, all inserts succeeded. Commit changes.
                        transaction.Commit();
                        return newItemId;
                    }
                    catch (SqlException ex)
                    {
                        try { transaction.Rollback(); } catch { }

                        if (ex.Number == 2627 || ex.Number == 2601)
                            throw new InvalidOperationException($"Item '{menuItem.Name}' already exists.", ex);

                        // Include ex.Message so the real SQL Server error surfaces to the UI.
                        throw new InvalidOperationException($"Database error during menu creation. SQL: {ex.Message}", ex);
                    }
                    catch (Exception)
                    {
                        try { transaction.Rollback(); } catch { }
                        throw;
                    }
                }
            }
        }

        public async Task<MenuItem> GetByIdAsync(int id)
        {
            using (var connection = GetConnection())
            using (var command = CreateCommand(connection, "[Restaurant].[uspGetMenuItemDetails]"))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@MenuItemId", id);

                await connection.OpenAsync();

                MenuItem item = null;
                var variantDict = new Dictionary<int, Variant>();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        // 1. Parse Menu Item (Only once)
                        if (item == null)
                        {
                            item = new MenuItem
                            {
                                Id = GetValue<int>(reader, "MenuItemId"),
                                Name = GetValue<string>(reader, "ItemName"),
                                Description = GetValue<string>(reader, "Description"),
                                CategoryId = GetValue<int>(reader, "CategoryId"),
                                StationId = GetValue<int>(reader, "StationId"),
                                ImageUrl = GetValue<string>(reader, "ImageUrl"),
                                IsActive = GetValue<bool>(reader, "IsActive"),
                                IsAvailable = GetValue<bool>(reader, "IsAvailable"),
                                Variants = new List<Variant>()
                            };
                        }

                        // 2. Parse Variant (if exists)
                        int? variantIdOpt = GetValue<int?>(reader, "VariantId");
                        if (variantIdOpt.HasValue)
                        {
                            int variantId = variantIdOpt.Value;
                            if (!variantDict.ContainsKey(variantId))
                            {
                                var variant = new Variant
                                {
                                    Id = variantId,
                                    Name = GetValue<string>(reader, "VariantName"),
                                    Price = GetValue<decimal>(reader, "Price"),
                                    PortionSize = GetValue<string>(reader, "PortionSize"),
                                    RecipeLines = new ObservableCollection<MenuRecipe>()
                                };
                                variantDict[variantId] = variant;
                                item.Variants.Add(variant);
                            }

                            // 3. Parse Recipe Line (if exists)
                            int? recipeId = GetValue<int?>(reader, "RecipeId");
                            if (recipeId.HasValue)
                            {
                                var recipe = new MenuRecipe
                                {
                                    Id = recipeId.Value,
                                    ProductId = GetValue<int>(reader, "ProductId"),
                                    UnitMeasureId = HasColumn(reader, "UnitMeasureId")
                                        ? GetValue<int>(reader, "UnitMeasureId")
                                        : 0,
                                    ProductName = GetValue<string>(reader, "ProductName"),
                                    UnitName = HasColumn(reader, "UnitMeasureName")
                                        ? GetValue<string>(reader, "UnitMeasureName")
                                        : (HasColumn(reader, "UnitName") ? GetValue<string>(reader, "UnitName") : null),
                                    Quantity = GetValue<decimal>(reader, "QuantityRequired")
                                };
                                variantDict[variantId].RecipeLines.Add(recipe);
                            }
                        }
                    }
                }

                // uspGetMenuItemDetails does not return ingredient cost or a reliable unit
                // measure, so fetch the current StandardCost (WAC) and UnitMeasure for every
                // ingredient used across this item's variants in one follow-up query, then
                // map both onto each line - the same source of truth used by the Add flow.
                if (item != null)
                {
                    await PopulateIngredientMasterDataAsync(connection, item.Variants);
                }

                return item;
            }
        }

        private async Task PopulateIngredientMasterDataAsync(SqlConnection connection, IEnumerable<Variant> variants)
        {
            var productIds = variants
                .SelectMany(v => v.RecipeLines)
                .Select(r => r.ProductId)
                .Distinct()
                .ToList();

            if (!productIds.Any()) return;

            await PopulateSavedRecipeUnitsAsync(connection, variants);

            var costByProductId = new Dictionary<int, decimal>();
            var baseUnitIdByProductId = new Dictionary<int, int>();
            var baseUnitCodeByProductId = new Dictionary<int, string>();
            var baseUnitNameByProductId = new Dictionary<int, string>();

            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.Text;
                command.CommandText =
                    "SELECT p.Id, p.StandardCost, p.UnitMeasureId, um.Code AS UnitMeasureCode, um.Name AS UnitMeasureName " +
                    "FROM Inventory.Product p " +
                    "LEFT JOIN Inventory.UnitMeasure um ON um.Id = p.UnitMeasureId " +
                    "WHERE p.Id IN (" + string.Join(",", productIds.Select((_, i) => "@p" + i)) + ")";

                for (int i = 0; i < productIds.Count; i++)
                {
                    command.Parameters.AddWithValue("@p" + i, productIds[i]);
                }

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var productId = GetValue<int>(reader, "Id");
                        costByProductId[productId] = GetValue<decimal>(reader, "StandardCost");
                        baseUnitIdByProductId[productId] = GetValue<int>(reader, "UnitMeasureId");
                        baseUnitCodeByProductId[productId] = GetValue<string>(reader, "UnitMeasureCode");
                        baseUnitNameByProductId[productId] = GetValue<string>(reader, "UnitMeasureName");
                    }
                }
            }

            var selectedUnitIds = variants
                .SelectMany(v => v.RecipeLines)
                .Select(r => r.UnitMeasureId)
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            var selectedUnitCodeById = new Dictionary<int, string>();
            var selectedUnitNameById = new Dictionary<int, string>();

            if (selectedUnitIds.Any())
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText =
                        "SELECT Id, Code, Name " +
                        "FROM Inventory.UnitMeasure " +
                        "WHERE Id IN (" + string.Join(",", selectedUnitIds.Select((_, i) => "@u" + i)) + ")";

                    for (int i = 0; i < selectedUnitIds.Count; i++)
                    {
                        command.Parameters.AddWithValue("@u" + i, selectedUnitIds[i]);
                    }

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var unitId = GetValue<int>(reader, "Id");
                            selectedUnitCodeById[unitId] = GetValue<string>(reader, "Code");
                            selectedUnitNameById[unitId] = GetValue<string>(reader, "Name");
                        }
                    }
                }
            }

            var conversionRateByProductAndUnit = new Dictionary<string, decimal>();
            if (selectedUnitIds.Any())
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText =
                        "SELECT ProductId, TargetUnitMeasureId, ConversionRate " +
                        "FROM Inventory.ProductUnitConversion " +
                        "WHERE IsActive = 1 " +
                        "AND ProductId IN (" + string.Join(",", productIds.Select((_, i) => "@cp" + i)) + ") " +
                        "AND TargetUnitMeasureId IN (" + string.Join(",", selectedUnitIds.Select((_, i) => "@cu" + i)) + ")";

                    for (int i = 0; i < productIds.Count; i++)
                    {
                        command.Parameters.AddWithValue("@cp" + i, productIds[i]);
                    }

                    for (int i = 0; i < selectedUnitIds.Count; i++)
                    {
                        command.Parameters.AddWithValue("@cu" + i, selectedUnitIds[i]);
                    }

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var productId = GetValue<int>(reader, "ProductId");
                            var unitId = GetValue<int>(reader, "TargetUnitMeasureId");
                            conversionRateByProductAndUnit[BuildConversionKey(productId, unitId)] =
                                GetValue<decimal>(reader, "ConversionRate");
                        }
                    }
                }
            }

            foreach (var line in variants.SelectMany(v => v.RecipeLines))
            {
                if (!costByProductId.TryGetValue(line.ProductId, out var baseCost))
                {
                    continue;
                }

                if (line.UnitMeasureId <= 0 && baseUnitIdByProductId.TryGetValue(line.ProductId, out var baseUnitId))
                {
                    line.UnitMeasureId = baseUnitId;
                }

                selectedUnitCodeById.TryGetValue(line.UnitMeasureId, out var selectedUnitCode);
                selectedUnitNameById.TryGetValue(line.UnitMeasureId, out var selectedUnitName);

                if (!string.IsNullOrWhiteSpace(selectedUnitCode) ||
                    !string.IsNullOrWhiteSpace(selectedUnitName))
                {
                    line.UnitName = !string.IsNullOrWhiteSpace(selectedUnitCode)
                        ? selectedUnitCode
                        : selectedUnitName;
                }

                var conversionRate = ResolveRecipeUnitConversionRate(
                    line,
                    baseUnitIdByProductId,
                    baseUnitCodeByProductId,
                    baseUnitNameByProductId,
                    selectedUnitCodeById,
                    selectedUnitNameById,
                    conversionRateByProductAndUnit);

                line.CostPerUnit = conversionRate > 0m
                    ? baseCost / conversionRate
                    : baseCost;
            }
        }

        private async Task PopulateSavedRecipeUnitsAsync(SqlConnection connection, IEnumerable<Variant> variants)
        {
            var recipeLines = variants
                .SelectMany(v => v.RecipeLines)
                .Where(r => r.Id > 0)
                .ToList();

            if (!recipeLines.Any()) return;

            var recipeIds = recipeLines
                .Select(r => r.Id)
                .Distinct()
                .ToList();

            var unitByRecipeId = new Dictionary<int, Tuple<int, string>>();

            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.Text;
                command.CommandText =
                    "SELECT r.Id AS RecipeId, r.UnitMeasureId, um.Code AS UnitMeasureCode, um.Name AS UnitMeasureName " +
                    "FROM Inventory.Recipe r " +
                    "LEFT JOIN Inventory.UnitMeasure um ON um.Id = r.UnitMeasureId " +
                    "WHERE r.Id IN (" + string.Join(",", recipeIds.Select((_, i) => "@r" + i)) + ")";

                for (int i = 0; i < recipeIds.Count; i++)
                {
                    command.Parameters.AddWithValue("@r" + i, recipeIds[i]);
                }

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var recipeId = GetValue<int>(reader, "RecipeId");
                        var unitMeasureId = GetValue<int>(reader, "UnitMeasureId");
                        var unitMeasureCode = GetValue<string>(reader, "UnitMeasureCode");
                        var unitMeasureName = GetValue<string>(reader, "UnitMeasureName");
                        var displayName = !string.IsNullOrWhiteSpace(unitMeasureCode)
                            ? unitMeasureCode
                            : unitMeasureName;

                        unitByRecipeId[recipeId] = Tuple.Create(unitMeasureId, displayName);
                    }
                }
            }

            foreach (var line in recipeLines)
            {
                if (!unitByRecipeId.TryGetValue(line.Id, out var savedUnit))
                {
                    continue;
                }

                line.UnitMeasureId = savedUnit.Item1;
                line.UnitName = savedUnit.Item2;
            }
        }

        private static string BuildConversionKey(int productId, int unitMeasureId)
        {
            return productId + ":" + unitMeasureId;
        }

        private static decimal ResolveRecipeUnitConversionRate(
            MenuRecipe line,
            IDictionary<int, int> baseUnitIdByProductId,
            IDictionary<int, string> baseUnitCodeByProductId,
            IDictionary<int, string> baseUnitNameByProductId,
            IDictionary<int, string> selectedUnitCodeById,
            IDictionary<int, string> selectedUnitNameById,
            IDictionary<string, decimal> conversionRateByProductAndUnit)
        {
            if (!baseUnitIdByProductId.TryGetValue(line.ProductId, out var baseUnitId) ||
                line.UnitMeasureId <= 0 ||
                line.UnitMeasureId == baseUnitId)
            {
                return 1m;
            }

            if (conversionRateByProductAndUnit.TryGetValue(BuildConversionKey(line.ProductId, line.UnitMeasureId), out var configuredRate) &&
                configuredRate > 0m)
            {
                return configuredRate;
            }

            baseUnitCodeByProductId.TryGetValue(line.ProductId, out var baseUnitCode);
            baseUnitNameByProductId.TryGetValue(line.ProductId, out var baseUnitName);
            selectedUnitCodeById.TryGetValue(line.UnitMeasureId, out var selectedUnitCode);
            selectedUnitNameById.TryGetValue(line.UnitMeasureId, out var selectedUnitName);

            if (MatchesAnyUnit(new[] { baseUnitCode, baseUnitName }, "Kg", "KILOGRAM") &&
                MatchesAnyUnit(new[] { selectedUnitCode, selectedUnitName }, "g", "gram"))
            {
                return 1000m;
            }

            if (MatchesAnyUnit(new[] { baseUnitCode, baseUnitName }, "L", "LITER") &&
                MatchesAnyUnit(new[] { selectedUnitCode, selectedUnitName }, "ml", "milliliter", "millilitre"))
            {
                return 1000m;
            }

            return 1m;
        }

        private static bool MatchesAnyUnit(IEnumerable<string> unitValues, params string[] matches)
        {
            return unitValues
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Any(value => matches.Any(match => string.Equals(value.Trim(), match, StringComparison.OrdinalIgnoreCase)));
        }

        public async Task<IEnumerable<MenuVariantDto>> GetAllVariantsForSalesAsync()
        {
            var resultByVariantId = new Dictionary<int, MenuVariantDto>();

            using (var connection = GetConnection()) // Use BaseRepository.GetConnection()
            {
                await connection.OpenAsync();

                bool hasItemCodeColumn;
                using (var schemaCommand = connection.CreateCommand())
                {
                    schemaCommand.CommandType = CommandType.Text;
                    schemaCommand.CommandText = @"
                        SELECT COUNT(1)
                        FROM INFORMATION_SCHEMA.COLUMNS
                        WHERE TABLE_SCHEMA = 'Restaurant'
                          AND TABLE_NAME = 'Variant'
                          AND COLUMN_NAME = 'ItemCode'";

                    hasItemCodeColumn = Convert.ToInt32(await schemaCommand.ExecuteScalarAsync()) > 0;
                }

                // Raw SQL Query to join Menu Items and Variants.
                // Fallback: if ItemCode column doesn't exist, use zero-padded Variant Id as ItemCode (e.g. 00003).
                string sql = hasItemCodeColumn
                    ? @"
                        SELECT 
                             v.Id,
                             m.MenuCategoryId,
                             m.ImageURL,
                             COALESCE(NULLIF(LTRIM(RTRIM(v.ItemCode)), ''), RIGHT(REPLICATE('0', 5) + CAST(v.Id AS VARCHAR(10)), 5)) AS ItemCode,
                             m.Name as MenuItemName,
                             v.Name as VariantName,
                             v.Price,
                             v.DiscountAmount,
                             pb.Barcode, -- Single barcode per variant (see OUTER APPLY below)
                             tax.TaxIdsCsv
                         FROM Restaurant.Variant v
                         INNER JOIN Restaurant.MenuItem m ON v.MenuItemId = m.Id
                         OUTER APPLY (
                             -- A variant's recipe can list several ingredients/products; TOP 1 keeps
                             -- this a one-row-per-variant query so we never fan out duplicate VariantIds.
                             SELECT TOP 1 p.Barcode
                             FROM Inventory.Recipe r
                             INNER JOIN Inventory.Product p ON p.Id = r.ProductId
                             WHERE r.VariantId = v.Id
                             ORDER BY r.Id
                         ) pb
                         LEFT JOIN (
                             SELECT MenuItemId, STRING_AGG(CAST(TaxId AS VARCHAR(10)), ',') AS TaxIdsCsv
                             FROM Restaurant.MenuItemTax
                             GROUP BY MenuItemId
                         ) tax ON tax.MenuItemId = m.Id
                         -- NOTE: Availability is intentionally not enforced here; existing sales loading only filters active menu items.
                         WHERE m.IsActive = 1"
                     : @"
                         SELECT
                              v.Id,
                              m.MenuCategoryId,
                              m.ImageURL,
                              RIGHT(REPLICATE('0', 5) + CAST(v.Id AS VARCHAR(10)), 5) AS ItemCode,
                             m.Name as MenuItemName,
                             v.Name as VariantName,
                             v.Price,
                             v.DiscountAmount,
                             pb.Barcode, -- Single barcode per variant (see OUTER APPLY below)
                             tax.TaxIdsCsv
                         FROM Restaurant.Variant v
                         INNER JOIN Restaurant.MenuItem m ON v.MenuItemId = m.Id
                         OUTER APPLY (
                             -- A variant's recipe can list several ingredients/products; TOP 1 keeps
                             -- this a one-row-per-variant query so we never fan out duplicate VariantIds.
                             SELECT TOP 1 p.Barcode
                             FROM Inventory.Recipe r
                             INNER JOIN Inventory.Product p ON p.Id = r.ProductId
                             WHERE r.VariantId = v.Id
                             ORDER BY r.Id
                         ) pb
                         LEFT JOIN (
                             SELECT MenuItemId, STRING_AGG(CAST(TaxId AS VARCHAR(10)), ',') AS TaxIdsCsv
                             FROM Restaurant.MenuItemTax
                             GROUP BY MenuItemId
                         ) tax ON tax.MenuItemId = m.Id
                          -- NOTE: Availability is intentionally not enforced here; existing sales loading only filters active menu items.
                          WHERE m.IsActive = 1";

                using (var command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = sql;

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var dto = new MenuVariantDto
                            {
                                // Use BaseRepository.GetValue<T>() for safe mapping
                                VariantId = GetValue<int>(reader, "Id"),
                                MenuCategoryId = GetValue<int>(reader, "MenuCategoryId"),
                                ItemCode = GetValue<string>(reader, "ItemCode"),
                                MenuItemName = GetValue<string>(reader, "MenuItemName"),
                                VariantName = GetValue<string>(reader, "VariantName"),
                                ImageUrl = GetValue<string>(reader, "ImageURL"),
                                DefaultPrice = GetValue<decimal>(reader, "Price"),
                                DiscountAmount = GetValue<decimal?>(reader, "DiscountAmount"), // Assuming this column exists
                                Barcode = GetValue<string>(reader, "Barcode"),
                                TaxIds = (GetValue<string>(reader, "TaxIdsCsv") ?? string.Empty)
                                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                    .Select(int.Parse)
                                    .ToList()
                            };

                            // Indexer upsert: if a join ever produces more than one row for the
                            // same VariantId, the later row safely overwrites the earlier one
                            // instead of throwing "An item with the same key has already been added".
                            resultByVariantId[dto.VariantId] = dto;
                        }
                    }
                }
            }

            return resultByVariantId.Values;
        }

        public async Task<IEnumerable<VariantPriceDto>> GetVariantsByItemIdAsync(int menuItemId)
        {
            var list = new List<VariantPriceDto>();
            string sql = "SELECT Id, Name, Price FROM Restaurant.Variant WHERE MenuItemId = @Id";

            using (var connection = GetConnection())
            {
                await connection.OpenAsync();
                using (var command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = sql;

                    var p = command.CreateParameter(); p.ParameterName = "@Id"; p.Value = menuItemId; command.Parameters.Add(p);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new VariantPriceDto
                            {
                                VariantId = GetValue<int>(reader, "Id"),
                                VariantName = GetValue<string>(reader, "Name"),
                                Price = GetValue<decimal>(reader, "Price")
                            });
                        }
                    }
                }
            }
            return list;
        }

        public async Task UpdateVariantPricesAsync(IEnumerable<VariantPriceDto> variants)
        {
            using (var connection = GetConnection())
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        string sql = "UPDATE Restaurant.Variant SET Price = @Price WHERE Id = @Id";

                        foreach (var v in variants)
                        {
                            using (var command = connection.CreateCommand())
                            {
                                command.Transaction = transaction;
                                command.CommandType = CommandType.Text;
                                command.CommandText = sql;

                                var p1 = command.CreateParameter(); p1.ParameterName = "@Price"; p1.Value = v.Price; command.Parameters.Add(p1);
                                var p2 = command.CreateParameter(); p2.ParameterName = "@Id"; p2.Value = v.VariantId; command.Parameters.Add(p2);

                                await command.ExecuteNonQueryAsync();
                            }
                        }
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

        public async Task<IEnumerable<MenuItemDto>> GetAllWithDetailsAsync()
        {
            var resultList = new List<MenuItemDto>();

            // SQL: 
            // 1. Joins MenuItem with Category to get CategoryName.
            // 2. Uses a Subquery (SELECT TOP 1...) to get the lowest price from variants as the 'Base Price'.
            string sql = @"
                SELECT 
                    m.Id, 
                    m.Name, 
                    m.Description, 
                    m.ImageURL, 
                    m.MenuCategoryId, 
                    c.Name AS CategoryName, 
                    m.TargetStationId,
                    s.Name AS TargetStation,
                    ISNULL(tax.AppliedTaxes, '') AS AppliedTaxes,
                    m.IsActive, 
                    m.IsAvailable,
                    (SELECT TOP 1 v.Price 
                     FROM Restaurant.Variant v 
                     WHERE v.MenuItemId = m.Id 
                     ORDER BY v.Price ASC) AS Price
                FROM Restaurant.MenuItem m
                LEFT JOIN Restaurant.MenuCategory c ON m.MenuCategoryId = c.Id
                LEFT JOIN Restaurant.Station s ON s.Id = m.TargetStationId
                LEFT JOIN
                (
                    SELECT
                        mit.MenuItemId,
                        STRING_AGG(tc.TaxCode, ', ') WITHIN GROUP (ORDER BY tc.CalculationOrder, tc.TaxCode) AS AppliedTaxes
                    FROM Restaurant.MenuItemTax mit
                    INNER JOIN [System].TaxConfiguration tc ON tc.Id = mit.TaxId
                    GROUP BY mit.MenuItemId
                ) tax ON tax.MenuItemId = m.Id
                ORDER BY m.Name";

            using (var connection = GetConnection())
            {
                await connection.OpenAsync();

                using (var command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = sql;

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var dto = new MenuItemDto
                            {
                                Id = GetValue<int>(reader, "Id"),
                                Name = GetValue<string>(reader, "Name"),
                                Description = GetValue<string>(reader, "Description"),
                                ImageUrl = GetValue<string>(reader, "ImageURL"),
                                CategoryId = GetValue<int>(reader, "MenuCategoryId"),
                                CategoryName = GetValue<string>(reader, "CategoryName"),
                                StationId = GetValue<int?>(reader, "TargetStationId") ?? 0,
                                TargetStation = GetValue<string>(reader, "TargetStation"),
                                AppliedTaxes = GetValue<string>(reader, "AppliedTaxes"),
                                IsActive = GetValue<bool>(reader, "IsActive"),
                                IsAvailable = GetValue<bool>(reader, "IsAvailable"),

                                // Handle Price: If null (no variants), default to 0
                                Price = GetValue<decimal?>(reader, "Price") ?? 0
                            };

                            resultList.Add(dto);
                        }
                    }
                }
            }

            return resultList;
        }

        public async Task<IEnumerable<int>> GetSelectedTaxIdsAsync(int menuItemId)
        {
            var taxIds = new List<int>();
            string sql = "SELECT TaxId FROM Restaurant.MenuItemTax WHERE MenuItemId = @MenuItemId";

            using (var connection = GetConnection())
            {
                await connection.OpenAsync();
                using (var command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = sql;
                    command.Parameters.AddWithValue("@MenuItemId", menuItemId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            taxIds.Add(reader.GetInt32(0));
                        }
                    }
                }
            }
            return taxIds;
        }

        public async Task UpdateAsync(MenuItem menuItem, IEnumerable<int> taxIds, int updatedBy)
        {
            using (var connection = GetConnection())
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // 1. UPDATE HEADER
                        using (var cmd = CreateCommand(connection, "[Restaurant].[uspUpdateMenuItem]", transaction))
                        {
                            cmd.Parameters.Add("@Id", SqlDbType.Int).Value = menuItem.Id;
                            AddMenuItemParameters(cmd, menuItem, updatedBy);
                            AddMenuItemTaxParameters(cmd, taxIds);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // 2. SYNC VARIANTS
                        foreach (var variant in menuItem.Variants)
                        {
                            int currentVariantId = variant.Id;

                            if (variant.Id == 0) // NEW VARIANT
                            {
                                using (var cmdIns = CreateCommand(connection, "[Restaurant].[uspInsertVariant]", transaction))
                                {
                                    cmdIns.Parameters.Add("@MenuItemId", SqlDbType.Int).Value = menuItem.Id;
                                    AddVariantParameters(cmdIns, variant, updatedBy);
                                    var pId = cmdIns.Parameters.Add("@Id", SqlDbType.Int);
                                    pId.Direction = ParameterDirection.Output;
                                    await cmdIns.ExecuteNonQueryAsync();
                                    currentVariantId = (int)pId.Value;
                                }
                            }
                            else // UPDATE VARIANT
                            {
                                using (var cmdUpd = CreateCommand(connection, "[Restaurant].[uspUpdateVariant]", transaction))
                                {
                                    cmdUpd.Parameters.Add("@Id", SqlDbType.Int).Value = variant.Id;
                                    AddVariantParameters(cmdUpd, variant, updatedBy);
                                    await cmdUpd.ExecuteNonQueryAsync();
                                }
                            }

                            // 3. SYNC RECIPES (Delete all & Re-insert strategy)
                            using (var cmdDel = CreateCommand(connection, "DELETE FROM [Inventory].[Recipe] WHERE VariantId = @Vid", transaction))
                            {
                                cmdDel.CommandType = CommandType.Text;
                                cmdDel.Parameters.AddWithValue("@Vid", currentVariantId);
                                await cmdDel.ExecuteNonQueryAsync();
                            }

                            foreach (var line in variant.RecipeLines)
                            {
                                using (var cmdRec = CreateCommand(connection, "[Restaurant].[uspInsertRecipe]", transaction))
                                {
                                    cmdRec.Parameters.Add("@VariantId", SqlDbType.Int).Value = currentVariantId;
                                    AddRecipeParameters(cmdRec, line);
                                    await cmdRec.ExecuteNonQueryAsync();
                                }
                            }
                        }

                        transaction.Commit();
                    }
                    catch (SqlException ex)
                    {
                        try { transaction.Rollback(); } catch { }
                        throw new InvalidOperationException($"Database error during menu update. SQL: {ex.Message}", ex);
                    }
                    catch (Exception)
                    {
                        try { transaction.Rollback(); } catch { }
                        throw;
                    }
                }
            }
        }

        public async Task DeleteAsync(int id)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    using (var command = CreateCommand(connection, "[Restaurant].[uspDeleteMenuItem]"))
                    {
                        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;

                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number == 547)
                    throw new InvalidOperationException("Cannot delete this menu item because it is referenced by existing transaction data (sales/orders).", ex);

                throw new InvalidOperationException($"Database error during menu deletion. SQL: {ex.Message}", ex);
            }
        }

        private void AddMenuItemParameters(SqlCommand command, MenuItem item, int createdBy)
        {
            command.Parameters.Add("@Name", SqlDbType.NVarChar, 100).Value = item.Name;
            command.Parameters.Add("@Description", SqlDbType.NVarChar, 500).Value = (object)item.Description ?? DBNull.Value;
            command.Parameters.Add("@CategoryId", SqlDbType.Int).Value = item.CategoryId;
            command.Parameters.Add("@StationId", SqlDbType.Int).Value = item.StationId;
            command.Parameters.Add("@ImageUrl", SqlDbType.NVarChar, 1000).Value = (object)item.ImageUrl ?? DBNull.Value;
            command.Parameters.Add("@IsActive", SqlDbType.Bit).Value = item.IsActive;
            command.Parameters.Add("@IsAvailable", SqlDbType.Bit).Value = item.IsAvailable;
            command.Parameters.Add("@CreatedBy", SqlDbType.Int).Value = createdBy;
        }

        private void AddMenuItemTaxParameters(SqlCommand command, IEnumerable<int> taxIds)
        {
            // Build the TVP table — always pass a DataTable (never null).
            // ADO.NET accepts an empty DataTable for a READONLY TVP with 0 rows,
            // which the stored procedure must handle as "no taxes selected."
            var taxTable = new DataTable();
            taxTable.Columns.Add("TaxId", typeof(int));

            if (taxIds != null)
            {
                foreach (var id in taxIds)
                    taxTable.Rows.Add(id);
            }

            // IMPORTANT: TypeName must use the plain two-part name (Schema.TypeName).
            // ADO.NET does NOT resolve bracket-quoted identifiers ([Schema].[TypeName])
            // for SqlDbType.Structured parameters — it throws a SqlException at bind time.
            var param = command.Parameters.Add("@TaxIds", SqlDbType.Structured);
            param.TypeName = "Restaurant.TaxIdListType";
            param.Value = taxTable;
        }

        private void AddVariantParameters(SqlCommand command, Variant variant, int createdBy)
        {
            command.Parameters.Add("@Name", SqlDbType.NVarChar, 50).Value = variant.Name;
            command.Parameters.Add("@PortionSize", SqlDbType.NVarChar, 50).Value = (object)variant.PortionSize ?? DBNull.Value;
            command.Parameters.Add("@Price", SqlDbType.Decimal).Value = variant.Price;
            command.Parameters.Add("@CreatedBy", SqlDbType.Int).Value = createdBy;
        }

        private void AddRecipeParameters(SqlCommand command, MenuRecipe line)
        {
            command.Parameters.Add("@ProductId", SqlDbType.Int).Value = line.ProductId;
            command.Parameters.Add("@UnitMeasureId", SqlDbType.Int).Value = line.UnitMeasureId;
            command.Parameters.Add("@Quantity", SqlDbType.Decimal).Value = line.Quantity;
        }

        private static bool HasColumn(IDataRecord record, string columnName)
        {
            for (var i = 0; i < record.FieldCount; i++)
            {
                if (string.Equals(record.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}





