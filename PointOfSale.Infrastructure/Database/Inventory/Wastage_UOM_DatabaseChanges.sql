/*
Wastage Multi-UOM deployment script

SQL Server table types cannot be altered. This script drops/recreates the dependent
stored procedure and [Inventory].[WastageLineType], then recreates
[Inventory].[uspInsertWastage] so user-entered wastage quantities are converted to
the product base UOM before inventory stock is reduced.

This script assumes the dynamic UOM conversion function signature is:
[Inventory].[fnConvertQuantity](@ProductId, @FromUnitMeasureId, @ToUnitMeasureId, @Quantity)
*/

IF OBJECT_ID(N'[Inventory].[uspInsertWastage]', N'P') IS NOT NULL
BEGIN
    DROP PROCEDURE [Inventory].[uspInsertWastage];
END;
GO

IF TYPE_ID(N'[Inventory].[WastageLineType]') IS NOT NULL
BEGIN
    DROP TYPE [Inventory].[WastageLineType];
END;
GO

CREATE TYPE [Inventory].[WastageLineType] AS TABLE
(
    [ProductId] INT NOT NULL,
    [BatchId] BIGINT NOT NULL,
    [UnitMeasureId] INT NOT NULL,
    [WastageReasonId] INT NULL,
    [Quantity] DECIMAL(18, 3) NOT NULL,
    [UnitCost] DECIMAL(18, 2) NOT NULL
);
GO

IF COL_LENGTH('Inventory.WastageLine', 'UnitMeasureId') IS NULL
BEGIN
    ALTER TABLE [Inventory].[WastageLine] ADD [UnitMeasureId] INT NULL;
END;
GO

CREATE OR ALTER PROCEDURE [Inventory].[uspInsertWastage]
    @BranchId INT,
    @LocationId INT,
    @WastageDate DATETIME,
    @Note NVARCHAR(500) = NULL,
    @CreatedBy INT,
    @WastageLines [Inventory].[WastageLineType] READONLY,
    @NewWastageId BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @WastageNumber NVARCHAR(50);

        EXEC [System].[uspGetNextCode]
            @BranchId = @BranchId,
            @Prefix = 'WST',
            @FormattedCode = @WastageNumber OUTPUT;

        INSERT INTO [Inventory].[Wastage]
        (
            [BranchId],
            [LocationId],
            [WastageNumber],
            [WastageDate],
            [Note],
            [Status],
            [CreatedBy],
            [CreatedDate]
        )
        VALUES
        (
            @BranchId,
            @LocationId,
            @WastageNumber,
            @WastageDate,
            @Note,
            'PENDING',
            @CreatedBy,
            GETDATE()
        );

        SET @NewWastageId = SCOPE_IDENTITY();

        SELECT
            l.[ProductId],
            l.[BatchId],
            l.[WastageReasonId],
            l.[UnitMeasureId],
            l.[UnitCost],
            CAST([Inventory].[fnConvertQuantity](
                l.[ProductId],
                l.[UnitMeasureId],
                p.[UnitMeasureId],
                l.[Quantity]) AS DECIMAL(18, 3)) AS [BaseQty]
        INTO #ResolvedWastage
        FROM @WastageLines l
        INNER JOIN [Inventory].[Product] p ON p.[Id] = l.[ProductId];

        INSERT INTO [Inventory].[WastageLine]
        (
            [WastageId],
            [ProductId],
            [BatchId],
            [WastageReasonId],
            [UnitMeasureId],
            [Quantity],
            [UnitCost]
        )
        SELECT
            @NewWastageId,
            [ProductId],
            [BatchId],
            [WastageReasonId],
            [UnitMeasureId],
            [BaseQty],
            [UnitCost]
        FROM #ResolvedWastage;

        DROP TABLE #ResolvedWastage;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        THROW;
    END CATCH
END;
GO
