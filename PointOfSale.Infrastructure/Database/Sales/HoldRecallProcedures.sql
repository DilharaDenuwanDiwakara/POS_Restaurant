CREATE OR ALTER PROCEDURE [Sales].[uspGetHeldSalesForRecall]
    @BranchId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        s.[Id] AS SalesId,
        s.[InvoiceNumber],
        s.[SalesDate],
        ISNULL(c.[Name], 'Walk-in') AS CustomerName,
        CAST(ISNULL(s.[SubTotal], 0) AS DECIMAL(18, 2)) AS TotalAmount,
        CAST(ISNULL(s.[DiscountAmount], 0) AS DECIMAL(18, 2)) AS Discount,
        CAST(ISNULL(s.[NetAmount], 0) AS DECIMAL(18, 2)) AS NetAmount,
        CAST(ISNULL(s.[AmountPaid], 0) AS DECIMAL(18, 2)) AS Cash,
        CAST(ISNULL(s.[BalanceAmount], 0) AS DECIMAL(18, 2)) AS CreditAmount,
        ISNULL(s.[PaymentStatus], 'UNPAID') AS PaymentStatus
    FROM [Sales].[Sales] s
    LEFT JOIN [Sales].[Customer] c ON c.[Id] = s.[CustomerId]
    WHERE s.[BranchId] = @BranchId
      AND UPPER(LTRIM(RTRIM(ISNULL(s.[PaymentStatus], '')))) = 'UNPAID'
    ORDER BY s.[SalesDate] DESC, s.[Id] DESC;
END
GO

CREATE OR ALTER PROCEDURE [Sales].[uspRecallHeldSale]
    @SalesId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 1
        s.[Id] AS SalesId,
        s.[InvoiceNumber],
        s.[CustomerId],
        s.[SalesPersonId],
        CAST(ISNULL(s.[SubTotal], 0) AS DECIMAL(18, 2)) AS TotalAmount,
        CAST(ISNULL(s.[DiscountAmount], 0) AS DECIMAL(18, 2)) AS Discount,
        CAST(ISNULL(s.[TaxAmount], 0) AS DECIMAL(18, 2)) AS TaxAmount,
        CAST(ISNULL(s.[ServiceCharge], 0) AS DECIMAL(18, 2)) AS ServiceChargeAmount,
        s.[OrderId],
        s.[ShiftId]
    FROM [Sales].[Sales] s
    WHERE s.[Id] = @SalesId
      AND UPPER(LTRIM(RTRIM(ISNULL(s.[PaymentStatus], '')))) = 'UNPAID';

    SELECT
        sl.[ProductId],
        COALESCE(
            NULLIF(
                LTRIM(RTRIM(
                    CASE
                        WHEN ISNULL(v.[Name], '') <> '' AND ISNULL(mi.[Name], '') <> ''
                            THEN mi.[Name] + ' - ' + v.[Name]
                        ELSE ISNULL(mi.[Name], '')
                    END)), ''),
            NULLIF(p.[Name], ''),
            CONCAT('Item #', CAST(sl.[ProductId] AS NVARCHAR(20)))) AS ProductName,
        CAST(sl.[Quantity] AS DECIMAL(18, 3)) AS Quantity,
        CAST(sl.[UnitPrice] AS DECIMAL(18, 2)) AS UnitPrice,
        CAST(ISNULL(sl.[DiscountAmount], 0) AS DECIMAL(18, 2)) AS DiscountAmount,
        CAST(ISNULL(sl.[TaxAmount], 0) AS DECIMAL(18, 2)) AS TaxAmount
    FROM [Sales].[SalesLine] sl
    LEFT JOIN [Restaurant].[Variant] v ON v.[Id] = sl.[ProductId]
    LEFT JOIN [Restaurant].[MenuItem] mi ON mi.[Id] = v.[MenuItemId]
    LEFT JOIN [Inventory].[Product] p ON p.[Id] = sl.[ProductId]
    WHERE sl.[SalesId] = @SalesId
    ORDER BY sl.[Id] ASC;
END
GO
