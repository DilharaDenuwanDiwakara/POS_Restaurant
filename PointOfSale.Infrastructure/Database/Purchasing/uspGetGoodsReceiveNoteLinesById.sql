CREATE OR ALTER PROCEDURE [Purchasing].[uspGetGoodsReceiveNoteLinesById]
    @GoodsReceiveNoteId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        grnl.[GoodsPurchaseNoteLineId],
        grnl.[ProductId],
        p.[Name] AS [ProductName],
        COALESCE(grnl.[UnitMeasureId], gpol.[UnitMeasureId], p.[UnitMeasureId]) AS [UnitMeasureId],
        ISNULL(gpol.[QuantityOrdered], grnl.[QuantityReceived]) AS [QuantityOrdered],
        grnl.[QuantityReceived],
        grnl.[UnitPrice],
        ISNULL(grnl.[LineDiscount], 0) AS [LineDiscount],
        ISNULL(grnl.[TaxAmount], 0) AS [TaxAmount],
        grnl.[ExpiryDate],
        um.[Code] AS [UnitMeasureCode],
        grnl.[BaseQuantityReceived],
        grnl.[BaseUnitCost],
        p.[TrackExpiry],
        p.[IsTaxApplicable]
    FROM [Purchasing].[GoodsReceiveNoteLine] grnl
    LEFT JOIN [Purchasing].[GoodsPurchaseNoteLine] gpol ON gpol.[Id] = grnl.[GoodsPurchaseNoteLineId]
    INNER JOIN [Inventory].[Product] p ON p.[Id] = grnl.[ProductId]
    LEFT JOIN [Inventory].[UnitMeasure] um
        ON um.[Id] = COALESCE(grnl.[UnitMeasureId], gpol.[UnitMeasureId], p.[UnitMeasureId])
    WHERE grnl.[GoodsReceiveNoteId] = @GoodsReceiveNoteId
    ORDER BY grnl.[Id];
END;
GO
