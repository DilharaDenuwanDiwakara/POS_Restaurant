CREATE OR ALTER PROCEDURE [Purchasing].[uspGetPurchaseNotesForApproval]
    @BranchId INT,
    @SupplierId INT = NULL,
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
        P.[BranchId] = @BranchId
        AND P.[Status] = 'PENDING_APPROVAL'
        AND (@SupplierId IS NULL OR P.[SupplierId] = @SupplierId)
        AND (@DateFrom IS NULL OR P.[OrderDate] >= @DateFrom)
        AND (@DateTo IS NULL OR P.[OrderDate] < DATEADD(DAY, 1, @DateTo))
    ORDER BY P.[CreatedDate] DESC
    OPTION (RECOMPILE);
END;
GO
