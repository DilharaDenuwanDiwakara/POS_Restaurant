CREATE OR ALTER PROCEDURE [Purchasing].[uspCancelPurchaseOrder]
    @Id INT,
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @CurrentStatus NVARCHAR(50);

    BEGIN TRANSACTION;

    SELECT @CurrentStatus = [Status]
    FROM [Purchasing].[GoodsPurchaseNote] WITH (UPDLOCK, HOLDLOCK)
    WHERE [Id] = @Id;

    IF @CurrentStatus IS NULL OR @CurrentStatus NOT IN ('DRAFT', 'APPROVED')
    BEGIN
        ROLLBACK TRANSACTION;
        RAISERROR('Only DRAFT or APPROVED purchase orders can be cancelled.', 16, 1);
        RETURN;
    END;

    UPDATE [Purchasing].[GoodsPurchaseNote]
    SET [Status] = CASE @CurrentStatus
                       WHEN 'DRAFT' THEN 'DELETED_DRAFT'
                       WHEN 'APPROVED' THEN 'CANCELLED'
                   END,
        [CancelledBy] = @UserId,
        [CancelledAt] = GETDATE()
    WHERE [Id] = @Id;

    COMMIT TRANSACTION;
END;
GO
