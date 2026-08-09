/*
Stock Transfer Multi-UOM deployment notes

SQL Server table types cannot be altered. Run this in a maintenance window after
dropping/recreating stored procedures that depend on [Inventory].[StockTransferLineType].

This patch assumes the dynamic UOM function signature is:
    [Inventory].[fnConvertProductQuantity](@ProductId, @FromUnitMeasureId, @ToUnitMeasureId, @Quantity)

If the deployed database uses a different function name, replace only that function call
in [Inventory].[uspInsertStockTransfer].
*/

IF TYPE_ID(N'[Inventory].[StockTransferLineType]') IS NOT NULL
BEGIN
    DROP TYPE [Inventory].[StockTransferLineType];
END;
GO

CREATE TYPE [Inventory].[StockTransferLineType] AS TABLE
(
    [ProductId] INT NOT NULL,
    [BatchId] BIGINT NOT NULL,
    [Quantity] DECIMAL(18, 3) NOT NULL,
    [UnitMeasureId] INT NOT NULL
);
GO

/*
Stored procedure save rule for [Inventory].[uspInsertStockTransfer]:

Before updating [Inventory].[LocationStock], [Inventory].[ProductBatch], and
[Inventory].[StockTransaction], convert each line quantity to the product base UOM.

Use the user-entered transfer quantity for transfer document lines, and use BaseQuantity
for every inventory movement/ledger quantity.

Example conversion projection:

    SELECT
        l.ProductId,
        l.BatchId,
        l.Quantity AS TransferQuantity,
        l.UnitMeasureId,
        p.UnitMeasureId AS BaseUnitMeasureId,
        [Inventory].[fnConvertProductQuantity](
            l.ProductId,
            l.UnitMeasureId,
            p.UnitMeasureId,
            l.Quantity) AS BaseQuantity
    FROM @Lines l
    INNER JOIN [Inventory].[Product] p ON p.Id = l.ProductId;

Then:
    - subtract BaseQuantity from the source location stock
    - add BaseQuantity to the destination location stock
    - write BaseQuantity to stock ledger / stock transaction rows
    - keep TransferQuantity + UnitMeasureId on the transfer detail rows for audit/reporting
*/
