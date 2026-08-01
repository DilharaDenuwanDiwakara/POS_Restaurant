CREATE OR ALTER PROCEDURE [Purchasing].[uspUpsertDraftPurchaseOrder]
    @Id INT,
    @BranchId INT,
    @SupplierId INT = NULL,
    @PONumber VARCHAR(50) = NULL,
    @SubTotal DECIMAL(18, 2) = 0,
    @DiscountAmount DECIMAL(18, 2) = 0,
    @TaxAmount DECIMAL(18, 2) = 0,
    @Note NVARCHAR(MAX) = NULL,
    @Status NVARCHAR(50),
    @OrderBy VARCHAR(100) = NULL,
    @OrderDate DATE,
    @ExpectedDeliveryDate DATE = NULL,
    @CreatedBy INT,
    @OrderLines [Purchasing].[GoodsPurchaseNoteLineType] READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @CurrentOrderId INT = @Id;

        IF ISNULL(@CurrentOrderId, 0) = 0
        BEGIN
            IF NULLIF(LTRIM(RTRIM(@PONumber)), '') IS NULL
            BEGIN
                EXEC [System].[uspGetNextCode]
                    @BranchId = @BranchId,
                    @Prefix = 'PO',
                    @FormattedCode = @PONumber OUTPUT;
            END;

            INSERT INTO [Purchasing].[GoodsPurchaseNote]
            (
                [BranchId],
                [SupplierId],
                [PONumber],
                [SubTotal],
                [DiscountAmount],
                [TaxAmount],
                [Note],
                [Status],
                [OrderBy],
                [OrderDate],
                [ExpectedDeliveryDate],
                [CreatedBy]
            )
            VALUES
            (
                @BranchId,
                @SupplierId,
                @PONumber,
                @SubTotal,
                @DiscountAmount,
                @TaxAmount,
                @Note,
                @Status,
                ISNULL(@OrderBy, ''),
                @OrderDate,
                @ExpectedDeliveryDate,
                @CreatedBy
            );

            SET @CurrentOrderId = CONVERT(INT, SCOPE_IDENTITY());
        END
        ELSE
        BEGIN
            UPDATE [Purchasing].[GoodsPurchaseNote]
            SET
                [BranchId] = @BranchId,
                [SupplierId] = @SupplierId,
                [PONumber] = COALESCE(NULLIF(LTRIM(RTRIM(@PONumber)), ''), [PONumber]),
                [SubTotal] = @SubTotal,
                [DiscountAmount] = @DiscountAmount,
                [TaxAmount] = @TaxAmount,
                [Note] = @Note,
                [Status] = @Status,
                [OrderBy] = ISNULL(@OrderBy, ''),
                [OrderDate] = @OrderDate,
                [ExpectedDeliveryDate] = @ExpectedDeliveryDate
            WHERE [Id] = @CurrentOrderId;

            DELETE FROM [Purchasing].[GoodsPurchaseNoteLine]
            WHERE [GoodsPurchaseNoteId] = @CurrentOrderId;
        END;

        IF EXISTS (SELECT 1 FROM @OrderLines)
        BEGIN
            INSERT INTO [Purchasing].[GoodsPurchaseNoteLine]
            (
                [GoodsPurchaseNoteId],
                [ProductId],
                [QuantityOrdered],
                [QuantityReceived],
                [UnitPrice],
                [LineDiscount],
                [TaxAmount]
            )
            SELECT
                @CurrentOrderId,
                [ProductId],
                [QuantityOrdered],
                0,
                [UnitPrice],
                [LineDiscount],
                [TaxAmount]
            FROM @OrderLines;
        END;

        COMMIT TRANSACTION;

        SELECT @CurrentOrderId AS [NewOrderId];
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
