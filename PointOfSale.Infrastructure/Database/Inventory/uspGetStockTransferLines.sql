CREATE OR ALTER PROCEDURE [Inventory].[uspGetStockTransferLines]
    @TransferId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        stl.[Id],
        stl.[TransferId],
        stl.[ProductId],
        p.[Code] AS [ProductCode],
        p.[Name] AS [ProductName],
        stl.[BatchId],
        stl.[Quantity],
        COALESCE(stl.[UnitMeasureId], p.[UnitMeasureId]) AS [UnitMeasureId],
        COALESCE(lineUm.[Code], baseUm.[Code]) AS [UnitMeasureCode],
        COALESCE(lineUm.[Name], baseUm.[Name]) AS [UnitMeasureName],
        pb.[ExpiryDate],
        ISNULL(pb.[CostPrice], p.[StandardCost]) AS [UnitCost]
    FROM [Inventory].[StockTransferLine] stl
    INNER JOIN [Inventory].[Product] p ON p.[Id] = stl.[ProductId]
    LEFT JOIN [Inventory].[ProductBatch] pb ON pb.[Id] = stl.[BatchId]
    LEFT JOIN [Inventory].[UnitMeasure] lineUm ON lineUm.[Id] = stl.[UnitMeasureId]
    LEFT JOIN [Inventory].[UnitMeasure] baseUm ON baseUm.[Id] = p.[UnitMeasureId]
    WHERE stl.[TransferId] = @TransferId
    ORDER BY stl.[Id];
END;
GO
