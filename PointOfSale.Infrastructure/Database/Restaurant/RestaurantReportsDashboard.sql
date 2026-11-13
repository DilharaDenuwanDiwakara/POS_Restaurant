/*
    Restaurant Reports Dashboard
    Reporting eligibility follows the existing Sales Reports convention:
      - Sales.vw_SalesReportData determines which sales are report-eligible
      - date filtering uses Sales.SalesDate
      - revenue uses Sales.Sales.NetAmount

    Completed sales are aggregated by OrderId before joining to restaurant visits.
    This keeps each OpenAccount/table visit to one row and prevents guest-count or
    serving-count multiplication if more than one completed invoice ever references
    the same restaurant order.
*/

/*
    Customer classification migration.
    The names below are used only once to classify legacy rows. Runtime report
    logic relies exclusively on Sales.Customer.IsAnonymous.
*/
IF COL_LENGTH('Sales.Customer', 'IsAnonymous') IS NULL
BEGIN
    ALTER TABLE [Sales].[Customer]
        ADD [IsAnonymous] BIT NOT NULL
            CONSTRAINT [DF_Sales_Customer_IsAnonymous] DEFAULT (0) WITH VALUES;

    EXEC sys.sp_executesql N'
        UPDATE [Sales].[Customer]
        SET [IsAnonymous] = 1
        WHERE UPPER(LTRIM(RTRIM([Name]))) IN (''WALKING CUSTOMER'', ''OTHER INCOME'');';
END;
GO

CREATE OR ALTER PROCEDURE [Restaurant].[uspGetRestaurantDashboardSummary]
    @BranchId INT = NULL,
    @StartDate DATE,
    @EndDate DATE
AS
BEGIN
    SET NOCOUNT ON;

    IF @StartDate IS NULL OR @EndDate IS NULL OR @EndDate < @StartDate
        THROW 50001, 'A valid Start Date and End Date are required.', 1;

    ;WITH SalesByOrder AS
    (
        SELECT s.[OrderId], SUM(ISNULL(s.[NetAmount], 0)) AS [NetSales]
        FROM [Sales].[Sales] s
        INNER JOIN
        (
            SELECT DISTINCT rd.[SalesId]
            FROM [Sales].[vw_SalesReportData] rd
            WHERE rd.[SalesDate] >= @StartDate
              AND rd.[SalesDate] < DATEADD(DAY, 1, @EndDate)
              AND (@BranchId IS NULL OR rd.[BranchId] = @BranchId)
        ) eligible ON eligible.[SalesId] = s.[Id]
        WHERE s.[OrderId] IS NOT NULL
          AND s.[SalesDate] >= @StartDate
          AND s.[SalesDate] < DATEADD(DAY, 1, @EndDate)
          AND (@BranchId IS NULL OR s.[BranchId] = @BranchId)
        GROUP BY s.[OrderId]
    ),
    EligibleVisits AS
    (
        SELECT
            oa.[Id] AS [VisitId],
            oa.[CustomerId],
            ISNULL(oa.[CustomerCount], 0) AS [GuestCount],
            oa.[OpenedAt],
            o.[WaiterId],
            sbo.[NetSales]
        FROM SalesByOrder sbo
        INNER JOIN [Restaurant].[Order] o ON o.[Id] = sbo.[OrderId]
        INNER JOIN [Restaurant].[OpenAccount] oa ON oa.[Id] = o.[OpenAccountId]
        WHERE (@BranchId IS NULL OR o.[BranchId] = @BranchId)
    ),
    RepeatCustomers AS
    (
        SELECT ev.[CustomerId], COUNT_BIG(*) AS [VisitCount]
        FROM EligibleVisits ev
        INNER JOIN [Sales].[Customer] c ON c.[Id] = ev.[CustomerId]
        WHERE ev.[CustomerId] IS NOT NULL
          AND c.[IsAnonymous] = 0
        GROUP BY ev.[CustomerId]
        HAVING COUNT_BIG(*) > 1
    ),
    StewardTotals AS
    (
        SELECT ev.[WaiterId], COUNT_BIG(*) AS [ServingCount]
        FROM EligibleVisits ev
        WHERE ev.[WaiterId] IS NOT NULL
        GROUP BY ev.[WaiterId]
    )
    SELECT
        CAST(ISNULL((SELECT SUM(ev.[GuestCount]) FROM EligibleVisits ev), 0) AS INT) AS [TotalGuestsServed],
        CAST((SELECT COUNT_BIG(*) FROM EligibleVisits) AS INT) AS [TotalTableVisits],
        CAST((SELECT COUNT_BIG(*) FROM RepeatCustomers) AS INT) AS [RepeatCustomerCount],
        ISNULL((
            SELECT STUFF((
                SELECT ' / ' + ISNULL(NULLIF(LTRIM(RTRIM(c.[Name])), ''), CONCAT('Customer #', rc.[CustomerId]))
                FROM RepeatCustomers rc
                INNER JOIN [Sales].[Customer] c ON c.[Id] = rc.[CustomerId]
                WHERE rc.[VisitCount] = (SELECT MAX(rcMax.[VisitCount]) FROM RepeatCustomers rcMax)
                ORDER BY c.[Name], rc.[CustomerId]
                FOR XML PATH(''), TYPE
            ).value('.', 'NVARCHAR(MAX)'), 1, 3, '')
        ), '') AS [HighestRepeatCustomerName],
        CAST(ISNULL((SELECT MAX(rc.[VisitCount]) FROM RepeatCustomers rc), 0) AS INT) AS [HighestRepeatVisitCount],
        ISNULL((
            SELECT TOP (1) COALESCE(NULLIF(LTRIM(RTRIM(u.[FullName])), ''), NULLIF(LTRIM(RTRIM(u.[Username])), ''), CONCAT('User #', st.[WaiterId]))
            FROM StewardTotals st
            LEFT JOIN [Auth].[User] u ON u.[Id] = st.[WaiterId]
            ORDER BY st.[ServingCount] DESC, u.[FullName], st.[WaiterId]
        ), '') AS [TopPerformingStewardName],
        CAST(ISNULL((SELECT TOP (1) st.[ServingCount] FROM StewardTotals st ORDER BY st.[ServingCount] DESC, st.[WaiterId]), 0) AS INT) AS [TopPerformingStewardServingCount],
        CAST(ISNULL((SELECT SUM(ev.[NetSales]) FROM EligibleVisits ev), 0) AS DECIMAL(18,2)) AS [TotalNetSales],
        CAST(ISNULL(
            (SELECT SUM(ev.[NetSales]) FROM EligibleVisits ev)
            / NULLIF((SELECT SUM(CONVERT(DECIMAL(18,2), ev.[GuestCount])) FROM EligibleVisits ev), 0),
            0) AS DECIMAL(18,2)) AS [AveragePerHeadValue];
END;
GO

CREATE OR ALTER PROCEDURE [Restaurant].[uspGetRepeatCustomerDetails]
    @BranchId INT = NULL,
    @StartDate DATE,
    @EndDate DATE
AS
BEGIN
    SET NOCOUNT ON;

    IF @StartDate IS NULL OR @EndDate IS NULL OR @EndDate < @StartDate
        THROW 50001, 'A valid Start Date and End Date are required.', 1;

    ;WITH SalesByOrder AS
    (
        SELECT s.[OrderId]
        FROM [Sales].[Sales] s
        INNER JOIN
        (
            SELECT DISTINCT rd.[SalesId]
            FROM [Sales].[vw_SalesReportData] rd
            WHERE rd.[SalesDate] >= @StartDate
              AND rd.[SalesDate] < DATEADD(DAY, 1, @EndDate)
              AND (@BranchId IS NULL OR rd.[BranchId] = @BranchId)
        ) eligible ON eligible.[SalesId] = s.[Id]
        WHERE s.[OrderId] IS NOT NULL
          AND s.[SalesDate] >= @StartDate
          AND s.[SalesDate] < DATEADD(DAY, 1, @EndDate)
          AND (@BranchId IS NULL OR s.[BranchId] = @BranchId)
        GROUP BY s.[OrderId]
    ),
    EligibleVisits AS
    (
        SELECT oa.[Id] AS [VisitId], oa.[CustomerId], ISNULL(oa.[CustomerCount], 0) AS [GuestCount], oa.[OpenedAt]
        FROM SalesByOrder sbo
        INNER JOIN [Restaurant].[Order] o ON o.[Id] = sbo.[OrderId]
        INNER JOIN [Restaurant].[OpenAccount] oa ON oa.[Id] = o.[OpenAccountId]
        INNER JOIN [Sales].[Customer] c ON c.[Id] = oa.[CustomerId]
        WHERE oa.[CustomerId] IS NOT NULL
          AND c.[IsAnonymous] = 0
          AND (@BranchId IS NULL OR o.[BranchId] = @BranchId)
    )
    SELECT
        ev.[CustomerId],
        ISNULL(NULLIF(LTRIM(RTRIM(c.[Name])), ''), CONCAT('Customer #', ev.[CustomerId])) AS [CustomerName],
        ISNULL(c.[ContactNumber], '') AS [ContactNumber],
        CAST(COUNT_BIG(*) AS INT) AS [VisitCount],
        CAST(SUM(ev.[GuestCount]) AS INT) AS [TotalGuests],
        MIN(ev.[OpenedAt]) AS [FirstVisit],
        MAX(ev.[OpenedAt]) AS [LastVisit]
    FROM EligibleVisits ev
    LEFT JOIN [Sales].[Customer] c ON c.[Id] = ev.[CustomerId]
    GROUP BY ev.[CustomerId], c.[Name], c.[ContactNumber]
    HAVING COUNT_BIG(*) > 1
    ORDER BY COUNT_BIG(*) DESC, [CustomerName], ev.[CustomerId];
END;
GO

CREATE OR ALTER PROCEDURE [Restaurant].[uspGetStewardPerformance]
    @BranchId INT = NULL,
    @StartDate DATE,
    @EndDate DATE
AS
BEGIN
    SET NOCOUNT ON;

    IF @StartDate IS NULL OR @EndDate IS NULL OR @EndDate < @StartDate
        THROW 50001, 'A valid Start Date and End Date are required.', 1;

    ;WITH SalesByOrder AS
    (
        SELECT s.[OrderId], SUM(ISNULL(s.[NetAmount], 0)) AS [NetSales]
        FROM [Sales].[Sales] s
        INNER JOIN
        (
            SELECT DISTINCT rd.[SalesId]
            FROM [Sales].[vw_SalesReportData] rd
            WHERE rd.[SalesDate] >= @StartDate
              AND rd.[SalesDate] < DATEADD(DAY, 1, @EndDate)
              AND (@BranchId IS NULL OR rd.[BranchId] = @BranchId)
        ) eligible ON eligible.[SalesId] = s.[Id]
        WHERE s.[OrderId] IS NOT NULL
          AND s.[SalesDate] >= @StartDate
          AND s.[SalesDate] < DATEADD(DAY, 1, @EndDate)
          AND (@BranchId IS NULL OR s.[BranchId] = @BranchId)
        GROUP BY s.[OrderId]
    ),
    EligibleVisits AS
    (
        SELECT oa.[Id] AS [VisitId], ISNULL(oa.[CustomerCount], 0) AS [GuestCount], o.[WaiterId], sbo.[NetSales]
        FROM SalesByOrder sbo
        INNER JOIN [Restaurant].[Order] o ON o.[Id] = sbo.[OrderId]
        INNER JOIN [Restaurant].[OpenAccount] oa ON oa.[Id] = o.[OpenAccountId]
        WHERE o.[WaiterId] IS NOT NULL
          AND (@BranchId IS NULL OR o.[BranchId] = @BranchId)
    )
    SELECT
        ev.[WaiterId] AS [StewardId],
        COALESCE(NULLIF(LTRIM(RTRIM(u.[FullName])), ''), NULLIF(LTRIM(RTRIM(u.[Username])), ''), CONCAT('User #', ev.[WaiterId])) AS [StewardName],
        CAST(COUNT_BIG(*) AS INT) AS [ServingCount],
        CAST(SUM(ev.[GuestCount]) AS INT) AS [TotalGuestsServed],
        CAST(SUM(ev.[NetSales]) AS DECIMAL(18,2)) AS [NetSales],
        CAST(ISNULL(SUM(ev.[NetSales]) / NULLIF(SUM(CONVERT(DECIMAL(18,2), ev.[GuestCount])), 0), 0) AS DECIMAL(18,2)) AS [AverageSalesPerGuest]
    FROM EligibleVisits ev
    LEFT JOIN [Auth].[User] u ON u.[Id] = ev.[WaiterId]
    GROUP BY ev.[WaiterId], u.[FullName], u.[Username]
    ORDER BY COUNT_BIG(*) DESC, [StewardName], ev.[WaiterId];
END;
GO
