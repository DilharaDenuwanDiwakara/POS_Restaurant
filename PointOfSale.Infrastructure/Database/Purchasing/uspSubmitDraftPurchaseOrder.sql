CREATE OR ALTER PROCEDURE [Purchasing].[uspSubmitDraftPurchaseOrder]
    @Id BIGINT,
    @BranchId INT,
    @SupplierId INT,
    @SubTotal DECIMAL(18, 2),
    @DiscountAmount DECIMAL(18, 2),
    @TaxAmount DECIMAL(18, 2),
    @Note NVARCHAR(MAX) = NULL,
    @OrderBy NVARCHAR(100),
    @OrderDate DATE,
    @ExpectedDeliveryDate DATE = NULL,
    @OrderLines [Purchasing].[GoodsPurchaseNoteLineType] READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE [Purchasing].[GoodsPurchaseNote]
        SET
            [BranchId] = @BranchId,
            [SupplierId] = @SupplierId,
            [SubTotal] = @SubTotal,
            [DiscountAmount] = @DiscountAmount,
            [TaxAmount] = @TaxAmount,
            [Note] = @Note,
            [Status] = 'PENDING_APPROVAL',
            [OrderBy] = @OrderBy,
            [OrderDate] = @OrderDate,
            [ExpectedDeliveryDate] = @ExpectedDeliveryDate
        WHERE [Id] = @Id
          AND [Status] = 'DRAFT';

        IF @@ROWCOUNT = 0
        BEGIN
            THROW 51002, 'Only draft purchase orders can be submitted.', 1;
        END;

        DELETE FROM [Purchasing].[GoodsPurchaseNoteLine]
        WHERE [GoodsPurchaseNoteId] = @Id;

        INSERT INTO [Purchasing].[GoodsPurchaseNoteLine]
        (
            [GoodsPurchaseNoteId],
            [ProductId],
            [UnitPrice],
            [QuantityOrdered],
            [LineDiscount],
            [TaxAmount]
        )
        SELECT
            @Id,
            [ProductId],
            [UnitPrice],
            [QuantityOrdered],
            ISNULL([LineDiscount], 0),
            ISNULL([TaxAmount], 0)
        FROM @OrderLines;

        COMMIT TRANSACTION;

        SELECT CONVERT(INT, @Id) AS [PurchaseNoteId];
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
        BEGIN
            ROLLBACK TRANSACTION;
        END;

        THROW;
    END CATCH;
END;
GO
