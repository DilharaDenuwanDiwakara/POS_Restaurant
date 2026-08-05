CREATE OR ALTER PROCEDURE [Purchasing].[uspGetPurchaseNoteLines]
    @PurchaseNoteId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        pol.[Id],
        pol.[GoodsPurchaseNoteId],
        pol.[ProductId],
        p.[Name] AS [ProductName],
        COALESCE(pol.[UnitMeasureId], p.[UnitMeasureId]) AS [UnitMeasureId],
        pol.[QuantityOrdered],
        ISNULL(received.[AlreadyReceivedQuantity], 0) AS [AlreadyReceivedQuantity],
        pol.[UnitPrice],
        ISNULL(pol.[LineDiscount], 0) AS [LineDiscount],
        ISNULL(pol.[TaxAmount], 0) AS [TaxAmount],
        COALESCE(lineUm.[Code], baseUm.[Code]) AS [UnitMeasureCode],
        p.[TrackExpiry]
    FROM [Purchasing].[GoodsPurchaseNoteLine] pol
    INNER JOIN [Inventory].[Product] p ON p.[Id] = pol.[ProductId]
    LEFT JOIN [Inventory].[UnitMeasure] lineUm ON lineUm.[Id] = pol.[UnitMeasureId]
    LEFT JOIN [Inventory].[UnitMeasure] baseUm ON baseUm.[Id] = p.[UnitMeasureId]
    OUTER APPLY
    (
        SELECT SUM(grnl.[QuantityReceived]) AS [AlreadyReceivedQuantity]
        FROM [Purchasing].[GoodsReceiveNoteLine] grnl
        INNER JOIN [Purchasing].[GoodsReceiveNote] grn ON grn.[Id] = grnl.[GoodsReceiveNoteId]
        WHERE grnl.[GoodsPurchaseNoteLineId] = pol.[Id]
          AND grn.[Status] <> 'REJECTED'
    ) received
    WHERE pol.[GoodsPurchaseNoteId] = @PurchaseNoteId
    ORDER BY pol.[Id];
END;
GO
