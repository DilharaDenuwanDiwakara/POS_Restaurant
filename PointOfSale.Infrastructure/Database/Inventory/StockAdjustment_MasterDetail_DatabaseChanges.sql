/*
Stock Adjustment Master-Detail deployment script

This script changes [Inventory].[uspInsertStockAdjustment] from one product per call
to one header plus many detail rows through [Inventory].[StockAdjustmentLineType].
Run this after validating the existing StockAdjustment table constraints in the target
database.
*/

IF OBJECT_ID(N'[Inventory].[uspInsertStockAdjustment]', N'P') IS NOT NULL
BEGIN
    DROP PROCEDURE [Inventory].[uspInsertStockAdjustment];
END;
GO

IF TYPE_ID(N'[Inventory].[StockAdjustmentLineType]') IS NOT NULL
BEGIN
    DROP TYPE [Inventory].[StockAdjustmentLineType];
END;
GO

CREATE TYPE [Inventory].[StockAdjustmentLineType] AS TABLE
(
    [ProductId] INT NOT NULL,
    [Quantity] DECIMAL(18, 3) NOT NULL,
    [Reason] NVARCHAR(500) NULL
);
GO

IF OBJECT_ID(N'[Inventory].[StockAdjustmentLine]', N'U') IS NULL
BEGIN
    CREATE TABLE [Inventory].[StockAdjustmentLine]
    (
        [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_StockAdjustmentLine] PRIMARY KEY,
        [StockAdjustmentId] BIGINT NOT NULL,
        [ProductId] INT NOT NULL,
        [Quantity] DECIMAL(18, 3) NOT NULL,
        [Reason] NVARCHAR(500) NULL,
        [CreatedDate] DATETIME NOT NULL CONSTRAINT [DF_StockAdjustmentLine_CreatedDate] DEFAULT (GETDATE())
    );
END;
GO

IF COL_LENGTH(N'Inventory.StockAdjustment', N'AdjustDate') IS NULL
BEGIN
    ALTER TABLE [Inventory].[StockAdjustment] ADD [AdjustDate] DATETIME NOT NULL CONSTRAINT [DF_StockAdjustment_AdjustDate] DEFAULT (GETDATE());
END;
GO

IF COL_LENGTH(N'Inventory.StockAdjustment', N'Reason') IS NULL
BEGIN
    ALTER TABLE [Inventory].[StockAdjustment] ADD [Reason] NVARCHAR(500) NULL;
END;
GO

IF COL_LENGTH(N'Inventory.StockAdjustment', N'ProductId') IS NOT NULL
BEGIN
    ALTER TABLE [Inventory].[StockAdjustment] ALTER COLUMN [ProductId] INT NULL;
END;
GO

IF COL_LENGTH(N'Inventory.StockAdjustment', N'Quantity') IS NOT NULL
BEGIN
    ALTER TABLE [Inventory].[StockAdjustment] ALTER COLUMN [Quantity] DECIMAL(18, 3) NULL;
END;
GO

CREATE OR ALTER PROCEDURE [Inventory].[uspInsertStockAdjustment]
    @BranchId INT,
    @UserId INT,
    @LocationId INT,
    @Note NVARCHAR(MAX) = NULL,
    @AdjustmentLines [Inventory].[StockAdjustmentLineType] READONLY,
    @StockAdjustmentId BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF NOT EXISTS (SELECT 1 FROM @AdjustmentLines)
        BEGIN
            THROW 50000, 'At least one stock adjustment line is required.', 1;
        END;

        IF EXISTS (SELECT 1 FROM @AdjustmentLines WHERE [Quantity] = 0)
        BEGIN
            THROW 50000, 'Stock adjustment line quantity cannot be zero.', 1;
        END;

        INSERT INTO [Inventory].[StockAdjustment]
        (
            [BranchId],
            [UserId],
            [LocationId],
            [Reason]
        )
        VALUES
        (
            @BranchId,
            @UserId,
            @LocationId,
            @Note
        );

        SET @StockAdjustmentId = CONVERT(BIGINT, SCOPE_IDENTITY());

        INSERT INTO [Inventory].[StockAdjustmentLine]
        (
            [StockAdjustmentId],
            [ProductId],
            [Quantity],
            [Reason]
        )
        SELECT
            @StockAdjustmentId,
            [ProductId],
            [Quantity],
            COALESCE(NULLIF(LTRIM(RTRIM([Reason])), ''), @Note)
        FROM @AdjustmentLines;

        UPDATE ls
        SET ls.[AvailableQuantity] = ls.[AvailableQuantity] + x.[Quantity]
        FROM [Inventory].[LocationStock] ls
        INNER JOIN
        (
            SELECT [ProductId], SUM([Quantity]) AS [Quantity]
            FROM @AdjustmentLines
            GROUP BY [ProductId]
        ) x ON x.[ProductId] = ls.[ProductId]
        WHERE ls.[LocationId] = @LocationId;

        IF EXISTS
        (
            SELECT 1
            FROM @AdjustmentLines l
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM [Inventory].[LocationStock] ls
                WHERE ls.[LocationId] = @LocationId
                  AND ls.[ProductId] = l.[ProductId]
            )
        )
        BEGIN
            INSERT INTO [Inventory].[LocationStock]
            (
                [LocationId],
                [ProductId],
                [AvailableQuantity]
            )
            SELECT
                @LocationId,
                l.[ProductId],
                SUM(l.[Quantity])
            FROM @AdjustmentLines l
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM [Inventory].[LocationStock] ls
                WHERE ls.[LocationId] = @LocationId
                  AND ls.[ProductId] = l.[ProductId]
            )
            GROUP BY l.[ProductId];
        END;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        THROW;
    END CATCH
END;
GO
