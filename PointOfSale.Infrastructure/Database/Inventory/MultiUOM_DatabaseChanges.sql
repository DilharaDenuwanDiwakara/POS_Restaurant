/*
Multi-UOM deployment notes
Run these changes in a maintenance window. SQL Server table types cannot be altered,
so dependent stored procedures must be dropped/recreated around TYPE changes.
Adjust table/column names if your deployed schema uses different physical names.
*/

/* 1. Product conversion direction */
IF COL_LENGTH('Inventory.ProductUnitConversion', 'IsMultiply') IS NULL
BEGIN
    ALTER TABLE [Inventory].[ProductUnitConversion]
    ADD [IsMultiply] BIT NOT NULL
        CONSTRAINT [DF_ProductUnitConversion_IsMultiply] DEFAULT (1);
END;
GO

/* 2. Purchase/GRN lines retain purchase UOM and converted inventory values */
IF COL_LENGTH('Purchasing.GoodsPurchaseNoteLine', 'UnitMeasureId') IS NULL
BEGIN
    ALTER TABLE [Purchasing].[GoodsPurchaseNoteLine] ADD [UnitMeasureId] INT NULL;
END;
GO

IF COL_LENGTH('Purchasing.GoodsReceiveNoteLine', 'UnitMeasureId') IS NULL
BEGIN
    ALTER TABLE [Purchasing].[GoodsReceiveNoteLine] ADD [UnitMeasureId] INT NULL;
END;
GO

IF COL_LENGTH('Purchasing.GoodsReceiveNoteLine', 'BaseQuantityReceived') IS NULL
BEGIN
    ALTER TABLE [Purchasing].[GoodsReceiveNoteLine] ADD [BaseQuantityReceived] DECIMAL(18, 4) NULL;
END;
GO

IF COL_LENGTH('Purchasing.GoodsReceiveNoteLine', 'BaseUnitCost') IS NULL
BEGIN
    ALTER TABLE [Purchasing].[GoodsReceiveNoteLine] ADD [BaseUnitCost] DECIMAL(18, 4) NULL;
END;
GO

/*
3. Recreate TVPs.
Before dropping types, drop/recreate procedures that reference them:
- [Purchasing].[uspInsertGoodPurchaseNote]
- [Purchasing].[uspUpsertDraftPurchaseOrder]
- [Purchasing].[uspSubmitDraftPurchaseOrder]
- [Purchasing].[uspInsertGoodsReceiveNote]
- [Purchasing].[uspResubmitRejectedGoodsReceiveNote]
*/

/*
CREATE TYPE [Purchasing].[GoodsPurchaseNoteLineType] AS TABLE
(
    [ProductId] INT NOT NULL,
    [UnitMeasureId] INT NOT NULL,
    [UnitPrice] DECIMAL(18, 2) NOT NULL,
    [QuantityOrdered] DECIMAL(18, 4) NOT NULL,
    [LineDiscount] DECIMAL(18, 2) NULL,
    [TaxAmount] DECIMAL(18, 2) NULL
);
GO

CREATE TYPE [Purchasing].[GoodsReceiveNoteLineType] AS TABLE
(
    [GoodsPurchaseNoteLineId] BIGINT NULL,
    [ProductId] INT NOT NULL,
    [UnitMeasureId] INT NOT NULL,
    [UnitPrice] DECIMAL(18, 2) NOT NULL,
    [ExpiryDate] DATETIME NULL,
    [QuantityReceived] DECIMAL(18, 4) NOT NULL,
    [BaseQuantityReceived] DECIMAL(18, 4) NOT NULL,
    [BaseUnitCost] DECIMAL(18, 4) NOT NULL,
    [LineDiscount] DECIMAL(18, 2) NULL,
    [TaxAmount] DECIMAL(18, 2) NULL
);
GO
*/

/*
4. Stored procedure save rules.

PO procedures:
- Insert [UnitMeasureId] from @OrderLines into [Purchasing].[GoodsPurchaseNoteLine].[UnitMeasureId].
- Return [UnitMeasureId] from [Purchasing].[uspGetPurchaseNoteLines].

GRN procedures:
- Insert purchase-facing [QuantityReceived], [UnitMeasureId], [BaseQuantityReceived], and [BaseUnitCost]
  into [Purchasing].[GoodsReceiveNoteLine].
- When writing [Inventory].[ProductBatch], use:
      Quantity    = [BaseQuantityReceived]
      CostPrice   = [BaseUnitCost]
- When updating [Inventory].[LocationStock], add [BaseQuantityReceived].
- When inserting [Inventory].[StockTransaction], use [BaseQuantityReceived] as the inventory movement quantity.

Do not recalculate conversion inside these procedures; the application service calculates
base quantity and base unit cost once before calling the stored procedure.
*/
