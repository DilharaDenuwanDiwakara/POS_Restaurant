CREATE OR ALTER PROCEDURE [Purchasing].[uspResubmitRejectedGoodsReceiveNote]
    @GoodsReceiveNoteId BIGINT,
    @BranchId INT,
    @SupplierId INT,
    @PurchaseOrderId BIGINT = NULL,
    @InvoiceNumber NVARCHAR(100),
    @DiscountAmount DECIMAL(18, 2),
    @TaxAmount DECIMAL(18, 2),
    @SubTotal DECIMAL(18, 2),
    @TotalAmount DECIMAL(18, 2),
    @ReceivedBy NVARCHAR(100),
    @Notes NVARCHAR(500) = NULL,
    @ReceivedDate DATE,
    @CreditDays INT = 0,
    @DueDate DATETIME = NULL,
    @UpdatedBy INT,
    @GoodsReceiveNoteLines [Purchasing].[GoodsReceiveNoteLineType] READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF NOT EXISTS
        (
            SELECT 1
            FROM [Purchasing].[GoodsReceiveNote]
            WHERE [Id] = @GoodsReceiveNoteId
              AND UPPER([Status]) = 'REJECTED'
        )
        BEGIN
            THROW 51001, 'Only rejected Goods Receive Notes can be resubmitted.', 1;
        END;

        UPDATE [Purchasing].[GoodsReceiveNote]
        SET
            [BranchId] = @BranchId,
            [SupplierId] = @SupplierId,
            [PurchaseOrderId] = COALESCE(@PurchaseOrderId, [PurchaseOrderId]),
            [InvoiceNumber] = @InvoiceNumber,
            [DiscountAmount] = @DiscountAmount,
            [TaxAmount] = @TaxAmount,
            [SubTotal] = @SubTotal,
            [TotalAmount] = @TotalAmount,
            [ReceivedBy] = @ReceivedBy,
            [Note] = @Notes,
            [ReceivedDate] = @ReceivedDate,
            [CreditDays] = ISNULL(@CreditDays, 0),
            [DueDate] = COALESCE(@DueDate, DATEADD(DAY, ISNULL(@CreditDays, 0), CAST(@ReceivedDate AS DATETIME))),
            [Status] = 'PENDING_APPROVAL',
            [ApprovedBy] = NULL,
            [ApprovedAt] = NULL
        WHERE [Id] = @GoodsReceiveNoteId;

        DELETE FROM [Purchasing].[GoodsReceiveNoteLine]
        WHERE [GoodsReceiveNoteId] = @GoodsReceiveNoteId;

        INSERT INTO [Purchasing].[GoodsReceiveNoteLine]
        (
            [GoodsReceiveNoteId],
            [GoodsPurchaseNoteLineId],
            [ProductId],
            [UnitPrice],
            [ExpiryDate],
            [QuantityReceived],
            [LineDiscount],
            [TaxAmount]
        )
        SELECT
            @GoodsReceiveNoteId,
            [GoodsPurchaseNoteLineId],
            [ProductId],
            [UnitPrice],
            [ExpiryDate],
            [QuantityReceived],
            [LineDiscount],
            [TaxAmount]
        FROM @GoodsReceiveNoteLines;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        DECLARE @ErrorNumber INT = ERROR_NUMBER();
        DECLARE @ErrorProcedure NVARCHAR(128) = ERROR_PROCEDURE();
        DECLARE @ErrorLine INT = ERROR_LINE();
        DECLARE @ErrorMessage NVARCHAR(MAX) = ERROR_MESSAGE();

        IF OBJECT_ID('[Logging].[uspLogError]', 'P') IS NOT NULL
            EXEC [Logging].[uspLogError] @ErrorProcedure, @ErrorMessage, @ErrorNumber, @ErrorLine;

        THROW;
    END CATCH
END;
GO
