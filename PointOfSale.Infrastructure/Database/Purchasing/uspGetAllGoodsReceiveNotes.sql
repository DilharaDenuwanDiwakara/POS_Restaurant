CREATE OR ALTER PROCEDURE [Purchasing].[uspGetAllGoodsReceiveNotes]
    @SupplierID INT = NULL,
    @DateFrom DATE = NULL,
    @DateTo DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        G.[Id],
        G.[BranchId],
        G.[SupplierId],
        G.[GoodsPurchaseNoteId],
        S.[Name] AS [SupplierName],
        G.[GoodsReceiveNoteNumber],
        G.[InvoiceNumber],
        G.[SubTotal],
        G.[DiscountAmount],
        G.[TaxAmount],
        G.[TotalAmount],
        G.[Note],
        G.[ReceivedBy],
        G.[ReceivedDate],
        G.[CreditDays],
        G.[DueDate],
        G.[Status],
        G.[CreatedBy],
        G.[CreatedAt],
        U.[Username],
        P.[PONumber]
    FROM [Purchasing].[GoodsReceiveNote] G
    INNER JOIN [Purchasing].[Supplier] S ON S.[Id] = G.[SupplierId]
    INNER JOIN [Auth].[User] U ON U.[Id] = G.[CreatedBy]
    LEFT JOIN [Purchasing].[GoodsPurchaseNote] P ON P.[Id] = G.[GoodsPurchaseNoteId]
    WHERE
        (@SupplierID IS NULL OR G.[SupplierId] = @SupplierID)
        AND (@DateFrom IS NULL OR G.[ReceivedDate] >= @DateFrom)
        AND (@DateTo IS NULL OR G.[ReceivedDate] < DATEADD(DAY, 1, @DateTo))
        AND G.[Status] <> 'CANCELLED'
    ORDER BY G.[CreatedAt] DESC
    OPTION (RECOMPILE);
END;
GO
