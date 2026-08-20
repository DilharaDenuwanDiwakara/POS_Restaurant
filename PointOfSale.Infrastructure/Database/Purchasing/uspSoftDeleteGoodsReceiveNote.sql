CREATE OR ALTER PROCEDURE [Purchasing].[uspSoftDeleteGoodsReceiveNote]
    @GoodsReceiveNoteId BIGINT,
    @DeletedBy INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE [Purchasing].[GoodsReceiveNote]
    SET
        [Status] = 'CANCELLED'
    WHERE [Id] = @GoodsReceiveNoteId
      AND [Status] = 'DRAFT';

    IF @@ROWCOUNT = 0
    BEGIN
        THROW 51005, 'Only active draft Goods Receipt Notes can be deleted.', 1;
    END;
END;
GO
