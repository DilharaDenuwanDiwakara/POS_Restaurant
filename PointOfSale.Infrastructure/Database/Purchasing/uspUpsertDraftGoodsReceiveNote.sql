CREATE OR ALTER PROCEDURE [Purchasing].[uspUpsertDraftGoodsReceiveNote]
    @Id BIGINT,
    @BranchId INT,
    @SupplierId INT = NULL,
    @PurchaseOrderId BIGINT = NULL,
    @GoodsReceiveNoteNumber VARCHAR(50) = NULL,
    @InvoiceNumber NVARCHAR(100) = NULL,
    @DiscountAmount DECIMAL(18, 2) = 0,
    @TaxAmount DECIMAL(18, 2) = 0,
    @SubTotal DECIMAL(18, 2) = 0,
    @ReceivedBy NVARCHAR(100) = NULL,
    @Notes NVARCHAR(500) = NULL,
    @ReceivedDate DATE,
    @CreditDays INT = 0,
    @DueDate DATETIME = NULL,
    @Status NVARCHAR(50),
    @CreatedBy INT,
    @GoodsReceiveNoteLines [Purchasing].[GoodsReceiveNoteLineType] READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @CurrentGoodsReceiveNoteId BIGINT = ISNULL(@Id, 0);
        DECLARE @NormalizedStatus NVARCHAR(50) = UPPER(LTRIM(RTRIM(ISNULL(@Status, 'DRAFT'))));

        IF @NormalizedStatus NOT IN ('DRAFT', 'PENDING_APPROVAL')
        BEGIN
            THROW 51003, 'Draft GRN upsert supports only DRAFT and PENDING_APPROVAL statuses.', 1;
        END;

        IF @CurrentGoodsReceiveNoteId = 0
        BEGIN
            IF NULLIF(LTRIM(RTRIM(@GoodsReceiveNoteNumber)), '') IS NULL
            BEGIN
                EXEC [System].[uspGetNextCode]
                    @BranchId = @BranchId,
                    @Prefix = 'GRN',
                    @FormattedCode = @GoodsReceiveNoteNumber OUTPUT;
            END;

            INSERT INTO [Purchasing].[GoodsReceiveNote]
            (
                [BranchId],
                [SupplierId],
                [GoodsPurchaseNoteId],
                [GoodsReceiveNoteNumber],
                [InvoiceNumber],
                [DiscountAmount],
                [TaxAmount],
                [SubTotal],
                [ReceivedBy],
                [Note],
                [ReceivedDate],
                [CreditDays],
                [DueDate],
                [Status],
                [CreatedBy]
            )
            VALUES
            (
                @BranchId,
                @SupplierId,
                @PurchaseOrderId,
                @GoodsReceiveNoteNumber,
                ISNULL(@InvoiceNumber, ''),
                @DiscountAmount,
                @TaxAmount,
                @SubTotal,
                @ReceivedBy,
                @Notes,
                @ReceivedDate,
                ISNULL(@CreditDays, 0),
                COALESCE(@DueDate, DATEADD(DAY, ISNULL(@CreditDays, 0), CAST(@ReceivedDate AS DATETIME))),
                @NormalizedStatus,
                @CreatedBy
            );

            SET @CurrentGoodsReceiveNoteId = CONVERT(BIGINT, SCOPE_IDENTITY());
        END
        ELSE
        BEGIN
            UPDATE [Purchasing].[GoodsReceiveNote]
            SET
                [BranchId] = @BranchId,
                [SupplierId] = @SupplierId,
                [GoodsPurchaseNoteId] = COALESCE(@PurchaseOrderId, [GoodsPurchaseNoteId]),
                [GoodsReceiveNoteNumber] = COALESCE(NULLIF(LTRIM(RTRIM(@GoodsReceiveNoteNumber)), ''), [GoodsReceiveNoteNumber]),
                [InvoiceNumber] = ISNULL(@InvoiceNumber, [InvoiceNumber]),
                [DiscountAmount] = @DiscountAmount,
                [TaxAmount] = @TaxAmount,
                [SubTotal] = @SubTotal,
                [ReceivedBy] = @ReceivedBy,
                [Note] = @Notes,
                [ReceivedDate] = @ReceivedDate,
                [CreditDays] = ISNULL(@CreditDays, 0),
                [DueDate] = COALESCE(@DueDate, DATEADD(DAY, ISNULL(@CreditDays, 0), CAST(@ReceivedDate AS DATETIME))),
                [Status] = @NormalizedStatus,
                [ApprovedBy] = NULL,
                [ApprovedAt] = NULL
            WHERE [Id] = @CurrentGoodsReceiveNoteId
              AND [Status] = 'DRAFT';

            IF @@ROWCOUNT = 0
            BEGIN
                THROW 51004, 'Only draft Goods Receipt Notes can be updated by draft upsert.', 1;
            END;

            DELETE FROM [Purchasing].[GoodsReceiveNoteLine]
            WHERE [GoodsReceiveNoteId] = @CurrentGoodsReceiveNoteId;
        END;

        IF EXISTS (SELECT 1 FROM @GoodsReceiveNoteLines)
        BEGIN
            INSERT INTO [Purchasing].[GoodsReceiveNoteLine]
            (
                [GoodsReceiveNoteId],
                [GoodsPurchaseNoteLineId],
                [ProductId],
                [UnitMeasureId],
                [UnitPrice],
                [ExpiryDate],
                [QuantityReceived],
                [BaseQuantityReceived],
                [BaseUnitCost],
                [LineDiscount],
                [TaxAmount]
            )
            SELECT
                @CurrentGoodsReceiveNoteId,
                [GoodsPurchaseNoteLineId],
                [ProductId],
                [UnitMeasureId],
                [UnitPrice],
                [ExpiryDate],
                [QuantityReceived],
                [BaseQuantityReceived],
                [BaseUnitCost],
                ISNULL([LineDiscount], 0),
                ISNULL([TaxAmount], 0)
            FROM @GoodsReceiveNoteLines;
        END;

        COMMIT TRANSACTION;

        SELECT @CurrentGoodsReceiveNoteId AS [GoodsReceiveNoteId];
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
