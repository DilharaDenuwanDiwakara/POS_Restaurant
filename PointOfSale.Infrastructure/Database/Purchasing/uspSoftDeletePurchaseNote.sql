CREATE OR ALTER PROCEDURE [Purchasing].[uspSoftDeletePurchaseNote]
    @PurchaseNoteId BIGINT,
    @DeletedBy INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE [Purchasing].[GoodsPurchaseNote]
    SET
        [Status] = 'CANCELLED',
        [CancelledBy] = @DeletedBy,
        [CancelledAt] = DATEADD(MINUTE, 330, SYSUTCDATETIME())
    WHERE [Id] = @PurchaseNoteId
      AND [Status] = 'DRAFT'
      AND [CancelledAt] IS NULL;

    IF @@ROWCOUNT = 0
    BEGIN
        THROW 51001, 'Only active draft purchase orders can be deleted.', 1;
    END;
END;
GO
