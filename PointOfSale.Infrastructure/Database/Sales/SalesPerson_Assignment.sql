-- Run before deploying the Sales Person combo changes.
-- The existing hold/finalize procedures retain their parameters and business logic.
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'[Sales].[Sales]', N'U') IS NULL
        THROW 52010, 'Sales.Sales was not found.', 1;

    IF OBJECT_ID(N'[Sales].[SalesPerson]', N'U') IS NULL
        THROW 52011, 'Sales.SalesPerson was not found.', 1;

    IF COL_LENGTH(N'Sales.Sales', N'SalesPersonId') IS NULL
        ALTER TABLE [Sales].[Sales] ADD [SalesPersonId] INT NULL;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.foreign_key_columns AS fkc
        JOIN sys.columns AS c
            ON c.object_id = fkc.parent_object_id
           AND c.column_id = fkc.parent_column_id
        JOIN sys.columns AS referencedColumn
            ON referencedColumn.object_id = fkc.referenced_object_id
           AND referencedColumn.column_id = fkc.referenced_column_id
        WHERE fkc.parent_object_id = OBJECT_ID(N'[Sales].[Sales]')
          AND c.name = N'SalesPersonId'
          AND fkc.referenced_object_id = OBJECT_ID(N'[Sales].[SalesPerson]')
          AND referencedColumn.name = N'Id'
    )
    BEGIN
        ALTER TABLE [Sales].[Sales] WITH CHECK
        ADD CONSTRAINT [FK_Sales_SalesPerson]
            FOREIGN KEY ([SalesPersonId]) REFERENCES [Sales].[SalesPerson] ([Id]);
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
