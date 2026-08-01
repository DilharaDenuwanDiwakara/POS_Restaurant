CREATE OR ALTER PROCEDURE [Purchasing].[uspGetAllGoodsPurchaseNotes]
    @SupplierID INT = NULL,
    @DateFrom DATE = NULL,
    @DateTo DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        P.[Id],
        P.[BranchId],
        P.[SupplierId],
        P.[PONumber],
        S.[Name] AS [SupplierName],
        P.[SubTotal],
        P.[DiscountAmount],
        P.[TaxAmount],
        P.[TotalAmount],
        P.[Note],
        P.[Status],
        P.[OrderBy],
        P.[OrderDate],
        P.[ExpectedDeliveryDate],
        U.[Username],
        P.[CreatedBy],
        P.[CreatedDate],
        P.[ApprovedBy],
        P.[ApprovedAt],
        P.[CancelledBy],
        P.[CancelledAt]
    FROM [Purchasing].[GoodsPurchaseNote] P
    INNER JOIN [Purchasing].[Supplier] S ON P.[SupplierId] = S.[Id]
    INNER JOIN [Auth].[User] U ON U.[Id] = P.[CreatedBy]
    WHERE
        (@SupplierID IS NULL OR P.[SupplierId] = @SupplierID)
        AND (@DateFrom IS NULL OR P.[OrderDate] >= @DateFrom)
        AND (@DateTo IS NULL OR P.[OrderDate] < DATEADD(DAY, 1, @DateTo))
    ORDER BY P.[CreatedDate] DESC
    OPTION (RECOMPILE);
END;
GO
