using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Sales;

namespace PointOfSale.Infrastructure.Repositories.Sales
{
    public class DashboardRepository : BaseRepository, IDashboardRepository
    {
        public DashboardRepository(DatabaseConnection dbConnection) : base(dbConnection) { }

        public async Task<DashboardOverviewDto> GetOverviewAsync(int? branchId, DateTime fromDate, DateTime toDate)
        {
            var result = new DashboardOverviewDto
            {
                FromDate = fromDate.Date,
                ToDate = toDate.Date
            };

            var start = fromDate.Date;
            var endExclusive = toDate.Date.AddDays(1);

            // Inline SQL — uses [Status] = 'COMPLETED' so no IsHold/IsVoid columns are needed.
            // Each SELECT is separated by a semicolon so ADO.NET receives 8 result sets,
            // matching the original stored-procedure contract exactly.
            const string sql = @"
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
-- RS1: Overview KPIs
;WITH FS AS (
    SELECT s.*
    FROM   [Sales].[Sales] s
    WHERE  s.[SalesDate] >= @FromDate
      AND  s.[SalesDate] <  @ToDateExclusive
      AND  s.[Status]    =  'COMPLETED'
      AND  (@BranchId IS NULL OR s.[BranchId] = @BranchId)
)
SELECT
    ISNULL(SUM(fs.[NetAmount]), 0)                                                  AS TotalSales,
    COUNT(1)                                                                        AS OrderCount,
    ISNULL(AVG(CAST(fs.[NetAmount] AS DECIMAL(18,4))), 0)                           AS AverageOrderValue,
    ISNULL(SUM(fs.[DiscountAmount]), 0)                                             AS TotalDiscount,
    CASE WHEN ISNULL(SUM(fs.[SubTotal]), 0) = 0 THEN 0
         ELSE (SUM(fs.[DiscountAmount]) / NULLIF(SUM(fs.[SubTotal]), 0)) * 100 END  AS DiscountRatePercent,
    AVG(CASE WHEN o.[Id] IS NOT NULL
             THEN DATEDIFF(MINUTE, o.[OrderDate], fs.[SalesDate]) END)              AS AverageTableTurnMinutes,
    ISNULL(SUM(CASE WHEN CAST(fs.[SalesDate] AS DATE) = CAST(GETDATE() AS DATE)
                    THEN fs.[NetAmount] ELSE 0 END), 0)                             AS TodaySales,
    ISNULL(SUM(CASE WHEN CAST(fs.[SalesDate] AS DATE) = CAST(GETDATE() AS DATE)
                    THEN 1 ELSE 0 END), 0)                                          AS TodayOrderCount,
    ISNULL((SUM(fs.[NetAmount]) / NULLIF(SUM(o.[GuestCount]), 0)), 0)              AS AveragePerHead
FROM FS fs
LEFT JOIN [Restaurant].[Order] o ON o.[Id] = fs.[OrderId];

-- RS2: Payment method breakdown
;WITH FS AS (
    SELECT s.[Id]
    FROM   [Sales].[Sales] s
    WHERE  s.[SalesDate] >= @FromDate
      AND  s.[SalesDate] <  @ToDateExclusive
      AND  s.[Status]    =  'COMPLETED'
      AND  (@BranchId IS NULL OR s.[BranchId] = @BranchId)
)
SELECT
    ISNULL(SUM(CASE WHEN LOWER(p.[PaymentMethod]) LIKE 'cash%'   THEN p.[Amount] ELSE 0 END), 0) AS CashSales,
    ISNULL(SUM(CASE WHEN LOWER(p.[PaymentMethod]) LIKE 'card%'   THEN p.[Amount] ELSE 0 END), 0) AS CardSales,
    ISNULL(SUM(CASE WHEN LOWER(p.[PaymentMethod]) LIKE 'credit%' THEN p.[Amount] ELSE 0 END), 0) AS CreditSales,
    ISNULL(SUM(CASE WHEN LOWER(p.[PaymentMethod]) NOT LIKE 'cash%'
                     AND LOWER(p.[PaymentMethod]) NOT LIKE 'card%'
                     AND LOWER(p.[PaymentMethod]) NOT LIKE 'credit%'
                    THEN p.[Amount] ELSE 0 END), 0)                                               AS OtherSales
FROM [Sales].[Payment] p
INNER JOIN FS ON FS.[Id] = p.[SalesId];

-- RS3: Daily sales (one row per calendar day in the range)
;WITH Days AS (
    SELECT CAST(@FromDate AS DATE) AS DayDate
    UNION ALL
    SELECT DATEADD(DAY, 1, DayDate) FROM Days WHERE DayDate < CAST(@ToDate AS DATE)
),
SPD AS (
    SELECT CAST(s.[SalesDate] AS DATE) AS DayDate,
           ISNULL(SUM(s.[NetAmount]), 0) AS SalesAmount,
           COUNT(1)                      AS OrderCount
    FROM   [Sales].[Sales] s
    WHERE  s.[SalesDate] >= @FromDate
      AND  s.[SalesDate] <  @ToDateExclusive
      AND  s.[Status]    =  'COMPLETED'
      AND  (@BranchId IS NULL OR s.[BranchId] = @BranchId)
    GROUP BY CAST(s.[SalesDate] AS DATE)
)
SELECT d.DayDate,
       ISNULL(spd.SalesAmount, 0) AS SalesAmount,
       ISNULL(spd.OrderCount,  0) AS OrderCount
FROM Days d
LEFT JOIN SPD spd ON spd.DayDate = d.DayDate
ORDER BY d.DayDate
OPTION (MAXRECURSION 370);

-- RS4: Top 5 items by quantity
;WITH FS AS (
    SELECT s.[Id]
    FROM   [Sales].[Sales] s
    WHERE  s.[SalesDate] >= @FromDate
      AND  s.[SalesDate] <  @ToDateExclusive
      AND  s.[Status]    =  'COMPLETED'
      AND  (@BranchId IS NULL OR s.[BranchId] = @BranchId)
)
SELECT TOP (5)
    sl.[ProductId],
    COALESCE(NULLIF(mi.[Name],''), NULLIF(p.[Name],''),
             CONCAT('Item #', CAST(sl.[ProductId] AS NVARCHAR(20))))                          AS ItemName,
    CAST(SUM(sl.[Quantity]) AS DECIMAL(18,3))                                                 AS Quantity,
    CAST(SUM(ISNULL(sl.[LineTotal],
         (sl.[UnitPrice]*sl.[Quantity])-sl.[DiscountAmount])) AS DECIMAL(18,2))               AS Revenue
FROM [Sales].[SalesLine] sl
INNER JOIN FS ON FS.[Id] = sl.[SalesId]
LEFT JOIN [Restaurant].[Variant]  v  ON v.[Id]  = sl.[ProductId]
LEFT JOIN [Restaurant].[MenuItem] mi ON mi.[Id] = v.[MenuItemId]
LEFT JOIN [Inventory].[Product]   p  ON p.[Id]  = sl.[ProductId]
GROUP BY sl.[ProductId],
         COALESCE(NULLIF(mi.[Name],''), NULLIF(p.[Name],''),
                  CONCAT('Item #', CAST(sl.[ProductId] AS NVARCHAR(20))))
ORDER BY SUM(sl.[Quantity]) DESC,
         SUM(ISNULL(sl.[LineTotal],(sl.[UnitPrice]*sl.[Quantity])-sl.[DiscountAmount])) DESC;

-- RS5: Hourly sales for today
;WITH Hours AS (
    SELECT 0 AS HourOfDay
    UNION ALL
    SELECT HourOfDay + 1 FROM Hours WHERE HourOfDay < 23
),
SPH AS (
    SELECT DATEPART(HOUR, s.[SalesDate])  AS HourOfDay,
           ISNULL(SUM(s.[NetAmount]), 0)  AS SalesAmount,
           COUNT(1)                       AS OrderCount
    FROM   [Sales].[Sales] s
    WHERE  CAST(s.[SalesDate] AS DATE) = CAST(GETDATE() AS DATE)
      AND  s.[Status] = 'COMPLETED'
      AND  (@BranchId IS NULL OR s.[BranchId] = @BranchId)
    GROUP BY DATEPART(HOUR, s.[SalesDate])
)
SELECT h.HourOfDay,
       ISNULL(sph.SalesAmount, 0) AS SalesAmount,
       ISNULL(sph.OrderCount,  0) AS OrderCount
FROM Hours h
LEFT JOIN SPH sph ON sph.HourOfDay = h.HourOfDay
ORDER BY h.HourOfDay
OPTION (MAXRECURSION 24);

-- RS6: Top 5 categories by revenue
;WITH FS AS (
    SELECT s.[Id]
    FROM   [Sales].[Sales] s
    WHERE  s.[SalesDate] >= @FromDate
      AND  s.[SalesDate] <  @ToDateExclusive
      AND  s.[Status]    =  'COMPLETED'
      AND  (@BranchId IS NULL OR s.[BranchId] = @BranchId)
)
SELECT TOP (5)
    COALESCE(NULLIF(mc.[Name],''), 'Uncategorized')                                           AS CategoryName,
    CAST(SUM(ISNULL(sl.[LineTotal],
         (sl.[UnitPrice]*sl.[Quantity])-sl.[DiscountAmount])) AS DECIMAL(18,2))               AS SalesAmount,
    CAST(SUM(sl.[Quantity]) AS DECIMAL(18,3))                                                 AS Quantity
FROM [Sales].[SalesLine] sl
INNER JOIN FS ON FS.[Id] = sl.[SalesId]
LEFT JOIN [Restaurant].[Variant]      v  ON v.[Id]  = sl.[ProductId]
LEFT JOIN [Restaurant].[MenuItem]     mi ON mi.[Id] = v.[MenuItemId]
LEFT JOIN [Restaurant].[MenuCategory] mc ON mc.[Id] = mi.[MenuCategoryId]
GROUP BY COALESCE(NULLIF(mc.[Name],''), 'Uncategorized')
ORDER BY SUM(ISNULL(sl.[LineTotal],(sl.[UnitPrice]*sl.[Quantity])-sl.[DiscountAmount])) DESC;

-- RS7: Inventory summary
;WITH StockByProduct AS (
    SELECT ls.ProductId, SUM(ls.AvailableQuantity) AS Qty
    FROM   [Inventory].[LocationStock] ls
    INNER JOIN [Inventory].[Location] l ON l.Id = ls.LocationId
    WHERE  (@BranchId IS NULL OR l.BranchId = @BranchId)
    GROUP BY ls.ProductId
),
ExpiryByProduct AS (
    SELECT pb.ProductId,
           SUM(CASE WHEN pb.ExpiryDate IS NOT NULL AND pb.ExpiryDate < CAST(GETDATE() AS DATE)
                     AND ls.AvailableQuantity > 0 THEN ls.AvailableQuantity ELSE 0 END)      AS ExpiredQty,
           SUM(CASE WHEN pb.ExpiryDate IS NOT NULL AND pb.ExpiryDate >= CAST(GETDATE() AS DATE)
                     AND pb.ExpiryDate <= DATEADD(DAY,7,CAST(GETDATE() AS DATE))
                     AND ls.AvailableQuantity > 0 THEN ls.AvailableQuantity ELSE 0 END)      AS Expiring7Qty
    FROM [Inventory].[ProductBatch]        pb
    INNER JOIN [Inventory].[LocationStock] ls ON ls.BatchId = pb.Id
    INNER JOIN [Inventory].[Location]      l  ON l.Id       = ls.LocationId
    INNER JOIN [Inventory].[Product]       p  ON p.Id       = pb.ProductId
    WHERE (@BranchId IS NULL OR l.BranchId = @BranchId)
      AND p.TrackExpiry = 1
    GROUP BY pb.ProductId
)
SELECT
    SUM(CASE WHEN ISNULL(sbp.Qty,0) <= 0                                    THEN 1 ELSE 0 END) AS OutOfStockItemCount,
    SUM(CASE WHEN ISNULL(sbp.Qty,0) > 0
              AND ISNULL(sbp.Qty,0) <= ISNULL(p.ReorderPoint,0)             THEN 1 ELSE 0 END) AS LowStockItemCount,
    SUM(CASE WHEN ISNULL(ebp.Expiring7Qty,0) > 0                            THEN 1 ELSE 0 END) AS Expiring7DaysCount,
    SUM(CASE WHEN ISNULL(ebp.ExpiredQty,  0) > 0                            THEN 1 ELSE 0 END) AS ExpiredItemCount
FROM [Inventory].[Product] p
LEFT JOIN StockByProduct  sbp ON sbp.ProductId = p.Id
LEFT JOIN ExpiryByProduct ebp ON ebp.ProductId = p.Id
WHERE p.IsActive = 1;

-- RS8: Alerts
;WITH StockByProduct AS (
    SELECT ls.ProductId, SUM(ls.AvailableQuantity) AS Qty
    FROM   [Inventory].[LocationStock] ls
    INNER JOIN [Inventory].[Location] l ON l.Id = ls.LocationId
    WHERE  (@BranchId IS NULL OR l.BranchId = @BranchId)
    GROUP BY ls.ProductId
),
ExpiryAlert AS (
    SELECT TOP (4)
        'EXPIRY' AS AlertType,
        CASE WHEN pb.ExpiryDate < CAST(GETDATE() AS DATE)                   THEN 'HIGH'
             WHEN pb.ExpiryDate <= DATEADD(DAY,3,CAST(GETDATE() AS DATE))   THEN 'MEDIUM'
             ELSE 'LOW' END                                                                   AS Severity,
        p.Name                                                                                AS ItemName,
        CASE WHEN pb.ExpiryDate < CAST(GETDATE() AS DATE)
             THEN CONCAT('Expired on ', CONVERT(VARCHAR(10),pb.ExpiryDate,120))
             ELSE CONCAT('Expires on ', CONVERT(VARCHAR(10),pb.ExpiryDate,120)) END           AS Message,
        CASE WHEN pb.ExpiryDate < CAST(GETDATE() AS DATE)                   THEN 1
             WHEN pb.ExpiryDate <= DATEADD(DAY,3,CAST(GETDATE() AS DATE))   THEN 2
             ELSE 3 END                                                                       AS SeverityRank,
        pb.ExpiryDate AS SortDate
    FROM [Inventory].[ProductBatch]        pb
    INNER JOIN [Inventory].[LocationStock] ls ON ls.BatchId = pb.Id
    INNER JOIN [Inventory].[Location]      l  ON l.Id       = ls.LocationId
    INNER JOIN [Inventory].[Product]       p  ON p.Id       = pb.ProductId
    WHERE pb.ExpiryDate IS NOT NULL
      AND pb.ExpiryDate <= DATEADD(DAY,7,CAST(GETDATE() AS DATE))
      AND ls.AvailableQuantity > 0
      AND p.TrackExpiry = 1
      AND (@BranchId IS NULL OR l.BranchId = @BranchId)
    ORDER BY pb.ExpiryDate ASC
),
LowStockAlert AS (
    SELECT TOP (4)
        'STOCK' AS AlertType,
        CASE WHEN ISNULL(sbp.Qty,0) <= 0 THEN 'HIGH' ELSE 'MEDIUM' END                      AS Severity,
        p.Name                                                                                AS ItemName,
        CASE WHEN ISNULL(sbp.Qty,0) <= 0 THEN 'Out of stock'
             ELSE CONCAT('Low stock: ',CAST(CAST(sbp.Qty AS DECIMAL(18,2)) AS NVARCHAR(30)),
                         ' (Reorder ',CAST(p.ReorderPoint AS NVARCHAR(30)),')') END           AS Message,
        CASE WHEN ISNULL(sbp.Qty,0) <= 0 THEN 1 ELSE 2 END                                  AS SeverityRank,
        CAST(GETDATE() AS DATE)                                                               AS SortDate
    FROM [Inventory].[Product] p
    LEFT JOIN StockByProduct sbp ON sbp.ProductId = p.Id
    WHERE p.IsActive = 1
      AND (ISNULL(sbp.Qty,0) <= 0
           OR (ISNULL(sbp.Qty,0) > 0 AND ISNULL(sbp.Qty,0) <= ISNULL(p.ReorderPoint,0)))
    ORDER BY CASE WHEN ISNULL(sbp.Qty,0) <= 0 THEN 0 ELSE 1 END, ISNULL(sbp.Qty,0) ASC
),
Alerts AS (
    SELECT AlertType,Severity,ItemName,Message,SeverityRank,SortDate FROM ExpiryAlert
    UNION ALL
    SELECT AlertType,Severity,ItemName,Message,SeverityRank,SortDate FROM LowStockAlert
)
SELECT TOP (10) AlertType, Severity, ItemName, Message
FROM Alerts
ORDER BY SeverityRank ASC, SortDate ASC, ItemName ASC;
";

            using (var connection = GetConnection())
            using (var command = new SqlCommand(sql, connection))
            {
                command.CommandType = CommandType.Text;
                command.CommandTimeout = 60;

                command.Parameters.Add("@BranchId", SqlDbType.Int).Value = (object)branchId ?? DBNull.Value;
                command.Parameters.Add("@FromDate", SqlDbType.DateTime2).Value = start;
                command.Parameters.Add("@ToDate", SqlDbType.DateTime2).Value = toDate.Date;
                command.Parameters.Add("@ToDateExclusive", SqlDbType.DateTime2).Value = endExclusive;

                await connection.OpenAsync();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    // RS1: Overview KPIs
                    if (await reader.ReadAsync())
                    {
                        result.TotalSales = GetValue<decimal>(reader, "TotalSales");
                        result.OrderCount = GetValue<int>(reader, "OrderCount");
                        result.AverageOrderValue = GetValue<decimal>(reader, "AverageOrderValue");
                        result.AveragePerHead = GetValue<decimal>(reader, "AveragePerHead");
                        result.TotalDiscount = GetValue<decimal>(reader, "TotalDiscount");
                        result.DiscountRatePercent = GetValue<decimal>(reader, "DiscountRatePercent");
                        result.TodaySales = GetValue<decimal>(reader, "TodaySales");
                        result.TodayOrderCount = GetValue<int>(reader, "TodayOrderCount");

                        if (!reader.IsDBNull(reader.GetOrdinal("AverageTableTurnMinutes")))
                            result.AverageTableTurnMinutes = Convert.ToDouble(reader["AverageTableTurnMinutes"]);
                    }

                    // RS2: Payment breakdown
                    if (await reader.NextResultAsync() && await reader.ReadAsync())
                    {
                        result.CashSales = GetValue<decimal>(reader, "CashSales");
                        result.CardSales = GetValue<decimal>(reader, "CardSales");
                        result.CreditSales = GetValue<decimal>(reader, "CreditSales");
                        result.OtherSales = GetValue<decimal>(reader, "OtherSales");
                    }

                    // RS3: Daily sales
                    if (await reader.NextResultAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            result.DailySales.Add(new DashboardDailySalesPointDto
                            {
                                Day = GetValue<DateTime>(reader, "DayDate"),
                                SalesAmount = GetValue<decimal>(reader, "SalesAmount"),
                                OrderCount = GetValue<int>(reader, "OrderCount")
                            });
                        }
                    }

                    // RS4: Top items
                    if (await reader.NextResultAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            result.TopItems.Add(new DashboardTopItemDto
                            {
                                ProductId = GetValue<int>(reader, "ProductId"),
                                Name = GetValue<string>(reader, "ItemName"),
                                Quantity = GetValue<decimal>(reader, "Quantity"),
                                Revenue = GetValue<decimal>(reader, "Revenue")
                            });
                        }
                    }

                    // RS5: Hourly sales
                    if (await reader.NextResultAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            result.HourlySalesToday.Add(new DashboardHourlySalesPointDto
                            {
                                HourOfDay = GetValue<int>(reader, "HourOfDay"),
                                SalesAmount = GetValue<decimal>(reader, "SalesAmount"),
                                OrderCount = GetValue<int>(reader, "OrderCount")
                            });
                        }
                    }

                    // RS6: Category sales
                    if (await reader.NextResultAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            result.CategorySales.Add(new DashboardCategorySalesDto
                            {
                                CategoryName = GetValue<string>(reader, "CategoryName"),
                                SalesAmount = GetValue<decimal>(reader, "SalesAmount"),
                                Quantity = GetValue<decimal>(reader, "Quantity")
                            });
                        }
                    }

                    // RS7: Inventory summary
                    if (await reader.NextResultAsync() && await reader.ReadAsync())
                    {
                        result.OutOfStockItemCount = GetValue<int>(reader, "OutOfStockItemCount");
                        result.LowStockItemCount = GetValue<int>(reader, "LowStockItemCount");
                        result.Expiring7DaysCount = GetValue<int>(reader, "Expiring7DaysCount");
                        result.ExpiredItemCount = GetValue<int>(reader, "ExpiredItemCount");
                    }

                    // RS8: Alerts
                    if (await reader.NextResultAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            result.Alerts.Add(new DashboardAlertDto
                            {
                                AlertType = GetValue<string>(reader, "AlertType"),
                                Severity = GetValue<string>(reader, "Severity"),
                                ItemName = GetValue<string>(reader, "ItemName"),
                                Message = GetValue<string>(reader, "Message")
                            });
                        }
                    }
                }
            }

            return result;
        }
    }
}
