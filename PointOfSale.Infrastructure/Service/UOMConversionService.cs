using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Services;
using PointOfSale.Core.Models.Inventory;

namespace PointOfSale.Infrastructure.Service
{
    public class UOMConversionService : IUOMConversionService
    {
        private readonly DatabaseConnection _databaseConnection;

        public UOMConversionService(DatabaseConnection databaseConnection)
        {
            _databaseConnection = databaseConnection ?? throw new ArgumentNullException(nameof(databaseConnection));
        }

        public decimal GetConvertedQuantity(int productId, int fromUomId, int toUomId, decimal quantity)
        {
            return GetConvertedQuantityAsync(productId, fromUomId, toUomId, quantity)
                .GetAwaiter()
                .GetResult();
        }

        public async Task<decimal> GetConvertedQuantityAsync(int productId, int fromUomId, int toUomId, decimal quantity)
        {
            if (fromUomId == toUomId)
            {
                return quantity;
            }

            if (productId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(productId), "Product is required for UOM conversion.");
            }

            if (fromUomId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fromUomId), "Source UOM is required.");
            }

            if (toUomId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(toUomId), "Destination UOM is required.");
            }

            using (var connection = _databaseConnection.GetConnection())
            {
                await connection.OpenAsync();

                var baseUomId = await GetProductBaseUnitMeasureIdAsync(connection, productId);
                var productConversion = await GetProductConversionAsync(connection, productId, baseUomId, fromUomId, toUomId);

                if (productConversion != null)
                {
                    return ApplyProductConversion(quantity, productConversion, baseUomId, fromUomId, toUomId);
                }

                if (fromUomId != baseUomId && toUomId != baseUomId)
                {
                    var fromConversion = await GetProductConversionAsync(connection, productId, baseUomId, fromUomId, baseUomId);
                    var toConversion = await GetProductConversionAsync(connection, productId, baseUomId, baseUomId, toUomId);

                    if (fromConversion != null && toConversion != null)
                    {
                        var baseQuantity = ApplyProductConversion(quantity, fromConversion, baseUomId, fromUomId, baseUomId);
                        return ApplyProductConversion(baseQuantity, toConversion, baseUomId, baseUomId, toUomId);
                    }
                }

                var globalMultiplier = await GetGlobalMultiplierAsync(connection, fromUomId, toUomId);
                if (globalMultiplier.HasValue)
                {
                    return quantity * globalMultiplier.Value;
                }

                throw new InvalidOperationException(
                    $"No UOM conversion exists for product {productId} from UOM {fromUomId} to UOM {toUomId}.");
            }
        }

        public async Task<IEnumerable<ProductUnitMeasureOption>> GetAvailableUnitMeasuresAsync(int productId)
        {
            return await GetDistinctUOMsForProductAsync(productId);
        }

        public async Task<IEnumerable<ProductUnitMeasureOption>> GetDistinctUOMsForProductAsync(int productId)
        {
            if (productId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(productId), "Product is required.");
            }

            var units = new List<ProductUnitMeasureOption>();

            using (var connection = _databaseConnection.GetConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.Text;
                command.CommandText = @"
WITH BaseProduct AS
(
    SELECT UnitMeasureId
    FROM [Inventory].[Product]
    WHERE Id = @ProductId
)
SELECT
    p.UnitMeasureId,
    um.Code,
    um.Name AS UnitMeasureName,
    CAST(1 AS bit) AS IsBaseUnit,
    CAST(1 AS decimal(18, 6)) AS ConversionRate,
    CAST(1 AS bit) AS IsMultiply
FROM BaseProduct p
INNER JOIN [Inventory].[UnitMeasure] um ON um.Id = p.UnitMeasureId

UNION ALL

SELECT
    puc.TargetUnitMeasureId AS UnitMeasureId,
    um.Code,
    um.Name AS UnitMeasureName,
    CAST(0 AS bit) AS IsBaseUnit,
    puc.ConversionRate,
    puc.IsMultiply
FROM [Inventory].[ProductUnitConversion] puc
INNER JOIN [Inventory].[UnitMeasure] um ON um.Id = puc.TargetUnitMeasureId
WHERE puc.ProductId = @ProductId
  AND puc.IsActive = 1

UNION ALL

SELECT
    guc.ToUnitMeasureId AS UnitMeasureId,
    um.Code,
    um.Name AS UnitMeasureName,
    CAST(0 AS bit) AS IsBaseUnit,
    guc.Multiplier AS ConversionRate,
    CAST(0 AS bit) AS IsMultiply
FROM [Inventory].[GlobalUnitConversion] guc
INNER JOIN BaseProduct p ON p.UnitMeasureId = guc.FromUnitMeasureId
INNER JOIN [Inventory].[UnitMeasure] um ON um.Id = guc.ToUnitMeasureId

UNION ALL

SELECT
    guc.FromUnitMeasureId AS UnitMeasureId,
    um.Code,
    um.Name AS UnitMeasureName,
    CAST(0 AS bit) AS IsBaseUnit,
    guc.Multiplier AS ConversionRate,
    CAST(1 AS bit) AS IsMultiply
FROM [Inventory].[GlobalUnitConversion] guc
INNER JOIN BaseProduct p ON p.UnitMeasureId = guc.ToUnitMeasureId
INNER JOIN [Inventory].[UnitMeasure] um ON um.Id = guc.FromUnitMeasureId;";
                command.Parameters.Add("@ProductId", SqlDbType.Int).Value = productId;

                await connection.OpenAsync();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        units.Add(new ProductUnitMeasureOption
                        {
                            UnitMeasureId = GetValue<int>(reader, "UnitMeasureId"),
                            Code = GetValue<string>(reader, "Code"),
                            UnitMeasureName = GetValue<string>(reader, "UnitMeasureName"),
                            IsBaseUnit = GetValue<bool>(reader, "IsBaseUnit"),
                            ConversionRate = GetValue<decimal>(reader, "ConversionRate"),
                            IsMultiply = GetValue<bool>(reader, "IsMultiply")
                        });
                    }
                }
            }

            return units
                .GroupBy(unit => unit.UnitMeasureId)
                .Select(group => group
                    .OrderByDescending(unit => unit.IsBaseUnit)
                    .ThenBy(unit => unit.UnitMeasureName)
                    .First())
                .OrderByDescending(unit => unit.IsBaseUnit)
                .ThenBy(unit => unit.UnitMeasureName)
                .ToList();
        }

        public async Task<int> GetProductBaseUnitMeasureIdAsync(int productId)
        {
            if (productId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(productId), "Product is required.");
            }

            using (var connection = _databaseConnection.GetConnection())
            {
                await connection.OpenAsync();
                return await GetProductBaseUnitMeasureIdAsync(connection, productId);
            }
        }

        private static decimal ApplyProductConversion(
            decimal quantity,
            ProductConversionDefinition conversion,
            int baseUomId,
            int fromUomId,
            int toUomId)
        {
            if (fromUomId == conversion.TargetUnitMeasureId && toUomId == baseUomId)
            {
                return conversion.IsMultiply
                    ? quantity * conversion.ConversionRate
                    : quantity / conversion.ConversionRate;
            }

            if (fromUomId == baseUomId && toUomId == conversion.TargetUnitMeasureId)
            {
                return conversion.IsMultiply
                    ? quantity / conversion.ConversionRate
                    : quantity * conversion.ConversionRate;
            }

            throw new InvalidOperationException("The product UOM conversion does not match the requested direction.");
        }

        private static async Task<int> GetProductBaseUnitMeasureIdAsync(SqlConnection connection, int productId)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.Text;
                command.CommandText = "SELECT UnitMeasureId FROM [Inventory].[Product] WHERE Id = @ProductId";
                command.Parameters.Add("@ProductId", SqlDbType.Int).Value = productId;

                var result = await command.ExecuteScalarAsync();
                if (result == null || result == DBNull.Value)
                {
                    throw new InvalidOperationException($"Product {productId} was not found.");
                }

                return Convert.ToInt32(result);
            }
        }

        private static async Task<ProductConversionDefinition> GetProductConversionAsync(
            SqlConnection connection,
            int productId,
            int baseUomId,
            int fromUomId,
            int toUomId)
        {
            var targetUomId = fromUomId == baseUomId ? toUomId : fromUomId;

            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.Text;
                command.CommandText = @"
SELECT TOP 1 TargetUnitMeasureId, ConversionRate, IsMultiply
FROM [Inventory].[ProductUnitConversion]
WHERE ProductId = @ProductId
  AND TargetUnitMeasureId = @TargetUnitMeasureId
  AND IsActive = 1;";
                command.Parameters.Add("@ProductId", SqlDbType.Int).Value = productId;
                command.Parameters.Add("@TargetUnitMeasureId", SqlDbType.Int).Value = targetUomId;

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync())
                    {
                        return null;
                    }

                    return new ProductConversionDefinition(
                        GetValue<int>(reader, "TargetUnitMeasureId"),
                        GetValue<decimal>(reader, "ConversionRate"),
                        GetValue<bool>(reader, "IsMultiply"));
                }
            }
        }

        private static async Task<decimal?> GetGlobalMultiplierAsync(SqlConnection connection, int fromUomId, int toUomId)
        {
            var direct = await GetGlobalMultiplierAsync(connection, fromUomId, toUomId, reverse: false);
            if (direct.HasValue)
            {
                return direct.Value;
            }

            return await GetGlobalMultiplierAsync(connection, toUomId, fromUomId, reverse: true);
        }

        private static async Task<decimal?> GetGlobalMultiplierAsync(
            SqlConnection connection,
            int fromUomId,
            int toUomId,
            bool reverse)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.Text;
                command.CommandText = @"
SELECT TOP 1 Multiplier
FROM [Inventory].[GlobalUnitConversion]
WHERE FromUnitMeasureId = @FromUnitMeasureId
  AND ToUnitMeasureId = @ToUnitMeasureId;";
                command.Parameters.Add("@FromUnitMeasureId", SqlDbType.Int).Value = fromUomId;
                command.Parameters.Add("@ToUnitMeasureId", SqlDbType.Int).Value = toUomId;

                var result = await command.ExecuteScalarAsync();
                if (result == null || result == DBNull.Value)
                {
                    return null;
                }

                var multiplier = Convert.ToDecimal(result);
                return reverse ? 1m / multiplier : multiplier;
            }
        }

        private static T GetValue<T>(IDataRecord record, string columnName)
        {
            var value = record[columnName];
            if (value == DBNull.Value || value == null)
            {
                return default(T);
            }

            var type = typeof(T);
            var underlyingType = Nullable.GetUnderlyingType(type);
            return (T)Convert.ChangeType(value, underlyingType ?? type);
        }

        private sealed class ProductConversionDefinition
        {
            public ProductConversionDefinition(int targetUnitMeasureId, decimal conversionRate, bool isMultiply)
            {
                TargetUnitMeasureId = targetUnitMeasureId;
                ConversionRate = conversionRate;
                IsMultiply = isMultiply;
            }

            public int TargetUnitMeasureId { get; }
            public decimal ConversionRate { get; }
            public bool IsMultiply { get; }
        }
    }
}
