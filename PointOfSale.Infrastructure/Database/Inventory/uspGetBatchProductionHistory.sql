CREATE OR ALTER PROCEDURE [Inventory].[uspGetBatchProductionHistory]
    @FromDate DATETIME,
    @ToDate DATETIME
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @StartDate DATE = CAST(@FromDate AS DATE);
    DECLARE @EndDate DATE = DATEADD(DAY, 1, CAST(@ToDate AS DATE));

    SELECT
        st.[Id] AS StockTransactionId,
        st.[ProductId],
        p.[Name] AS ItemName,
        st.[Quantity] AS YieldQuantity,
        um.[Name] AS UnitMeasureName,
        st.[TransactionDate] AS ProcessedAt
    FROM [Inventory].[StockTransaction] st
    INNER JOIN [Inventory].[Product] p
        ON p.[Id] = st.[ProductId]
    LEFT JOIN [Inventory].[UnitMeasure] um
        ON um.[Id] = p.[UnitMeasureId]
    WHERE st.[TransactionType] = 'PRODUCTION_IN'
      AND st.[TransactionDate] >= @StartDate
      AND st.[TransactionDate] < @EndDate
    ORDER BY st.[TransactionDate] DESC, st.[Id] DESC;
END;
