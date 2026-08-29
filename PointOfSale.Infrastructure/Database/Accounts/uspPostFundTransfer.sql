/* ============================================================================
   Module   : Accounts - Fund Transfer (Banking)
   Purpose  : Internal double-entry transfers between two accounts in the
              Chart of Accounts (e.g. Cashier Float -> Bank Account deposit).

   Integrates with the existing ledger/support objects already in the
   database (not created here):
     - [Accounts].[JournalEntry] / [Accounts].[JournalEntryLine]  (GL)
     - [Accounts].[FiscalPeriod]                                  (period lookup + open/closed guard)
     - [System].[CodeGenerate] via [System].[uspGetNextCode]       (document numbering, Prefix = 'TRN')
   ============================================================================ */

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

/* ---------------------------------------------------------------------------
   1. Header table - one row per transfer (module-local, for a fast list/
      history screen without pivoting the two JournalEntryLine rows back
      apart). Links to its GL posting via JournalEntryId.
   --------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'FundTransfer' AND schema_id = SCHEMA_ID('Accounts'))
BEGIN
    CREATE TABLE [Accounts].[FundTransfer]
    (
        [Id]                    BIGINT          IDENTITY(1,1) NOT NULL,
        [JournalEntryId]        BIGINT          NOT NULL,
        [TransferNumber]        NVARCHAR(30)    NOT NULL,
        [BranchId]              INT             NOT NULL,
        [TransferDate]          DATE            NOT NULL,
        [SourceAccountId]       INT             NOT NULL,
        [DestinationAccountId]  INT             NOT NULL,
        [Amount]                DECIMAL(18,2)   NOT NULL,
        [ReferenceNo]           VARCHAR(100)    NULL,
        [Description]           NVARCHAR(500)   NULL,
        [CreatedBy]             INT             NOT NULL,
        [CreatedAt]             DATETIME        NOT NULL CONSTRAINT [DF_FundTransfer_CreatedAt] DEFAULT (GETDATE()),

        CONSTRAINT [PK_FundTransfer] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_FundTransfer_JournalEntry] FOREIGN KEY ([JournalEntryId]) REFERENCES [Accounts].[JournalEntry] ([Id]),
        CONSTRAINT [FK_FundTransfer_SourceAccount] FOREIGN KEY ([SourceAccountId]) REFERENCES [Accounts].[Account] ([Id]),
        CONSTRAINT [FK_FundTransfer_DestinationAccount] FOREIGN KEY ([DestinationAccountId]) REFERENCES [Accounts].[Account] ([Id]),
        CONSTRAINT [CK_FundTransfer_Amount_Positive] CHECK ([Amount] > 0),
        CONSTRAINT [CK_FundTransfer_DifferentAccounts] CHECK ([SourceAccountId] <> [DestinationAccountId])
    );

    CREATE NONCLUSTERED INDEX [IX_FundTransfer_Branch_Date] ON [Accounts].[FundTransfer] ([BranchId], [TransferDate]);
END
GO

/* ---------------------------------------------------------------------------
   2. uspPostFundTransfer
      Inserts one JournalEntry header + two balancing JournalEntryLine rows
      (Debit Destination / Credit Source) plus the FundTransfer header row,
      all in a single strict transaction.
   --------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE [Accounts].[uspPostFundTransfer]
    @TransferDate           DATE,
    @BranchId               INT,
    @SourceAccountId        INT,
    @DestinationAccountId   INT,
    @Amount                 DECIMAL(18,2),
    @ReferenceNo            VARCHAR(100)    = NULL,
    @Description            NVARCHAR(500)   = NULL,
    @CreatedBy              INT,
    @FundTransferId         BIGINT          OUTPUT,
    @TransferNumber         NVARCHAR(30)    OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    -- Guard clauses run before opening a transaction so validation failures
    -- never leave an open TRAN behind.
    IF @Amount IS NULL OR @Amount <= 0
    BEGIN
        RAISERROR('Transfer amount must be greater than zero.', 16, 1);
        RETURN;
    END

    IF @SourceAccountId = @DestinationAccountId
    BEGIN
        RAISERROR('Source and destination accounts must be different.', 16, 1);
        RETURN;
    END

    IF NOT EXISTS (SELECT 1 FROM [Accounts].[Account] WHERE [Id] = @SourceAccountId AND [IsActive] = 1)
    BEGIN
        RAISERROR('Source account does not exist or is inactive.', 16, 1);
        RETURN;
    END

    IF NOT EXISTS (SELECT 1 FROM [Accounts].[Account] WHERE [Id] = @DestinationAccountId AND [IsActive] = 1)
    BEGIN
        RAISERROR('Destination account does not exist or is inactive.', 16, 1);
        RETURN;
    END

    DECLARE @PeriodId INT;
    DECLARE @PeriodStatus NVARCHAR(10);
    DECLARE @PeriodName NVARCHAR(20);

    SELECT
        @PeriodId = [Id],
        @PeriodStatus = [Status],
        @PeriodName = [PeriodName]
    FROM [Accounts].[FiscalPeriod]
    WHERE @TransferDate BETWEEN [StartDate] AND [EndDate];

    IF @PeriodId IS NULL
    BEGIN
        RAISERROR('No fiscal period is defined for the transfer date.', 16, 1);
        RETURN;
    END

    IF @PeriodStatus <> 'OPEN'
    BEGIN
        DECLARE @ClosedPeriodMessage NVARCHAR(200) = 'The fiscal period ''' + @PeriodName + ''' is closed. Cannot post entries to a closed period.';
        RAISERROR(@ClosedPeriodMessage, 16, 1);
        RETURN;
    END

    DECLARE @JournalEntryId BIGINT;
    DECLARE @SafeReference NVARCHAR(50) = LEFT(NULLIF(LTRIM(RTRIM(@ReferenceNo)), ''), 50);
    DECLARE @SafeMemo NVARCHAR(255) = LEFT(NULLIF(LTRIM(RTRIM(@Description)), ''), 255);

    BEGIN TRY
        BEGIN TRANSACTION;

        EXEC [System].[uspGetNextCode]
            @BranchId = @BranchId,
            @Prefix = 'TRN',
            @FormattedCode = @TransferNumber OUTPUT;

        INSERT INTO [Accounts].[JournalEntry]
        (
            [PeriodId], [EntryDate], [EntryNumber], [SourceType], [SourceId],
            [Reference], [Memo], [Status], [TotalDebit], [TotalCredit],
            [CreatedBy], [CreatedAt], [PostedBy], [PostedAt]
        )
        VALUES
        (
            @PeriodId, @TransferDate, @TransferNumber, 'FundTransfer', NULL,
            @SafeReference, @SafeMemo, 'Posted', @Amount, @Amount,
            @CreatedBy, SYSDATETIME(), @CreatedBy, SYSDATETIME()
        );

        SET @JournalEntryId = SCOPE_IDENTITY();

        -- Debit the destination account (value moving IN)
        INSERT INTO [Accounts].[JournalEntryLine]
        (
            [JournalEntryId], [AccountId], [EntityId], [DebitAmount], [CreditAmount],
            [Memo], [DueDate], [IsReconciled]
        )
        VALUES
        (
            @JournalEntryId, @DestinationAccountId, NULL, @Amount, 0,
            @SafeMemo, NULL, 0
        );

        -- Credit the source account (value moving OUT)
        INSERT INTO [Accounts].[JournalEntryLine]
        (
            [JournalEntryId], [AccountId], [EntityId], [DebitAmount], [CreditAmount],
            [Memo], [DueDate], [IsReconciled]
        )
        VALUES
        (
            @JournalEntryId, @SourceAccountId, NULL, 0, @Amount,
            @SafeMemo, NULL, 0
        );

        INSERT INTO [Accounts].[FundTransfer]
        (
            [JournalEntryId], [TransferNumber], [BranchId], [TransferDate],
            [SourceAccountId], [DestinationAccountId], [Amount],
            [ReferenceNo], [Description], [CreatedBy]
        )
        VALUES
        (
            @JournalEntryId, @TransferNumber, @BranchId, @TransferDate,
            @SourceAccountId, @DestinationAccountId, @Amount,
            NULLIF(LTRIM(RTRIM(@ReferenceNo)), ''), NULLIF(LTRIM(RTRIM(@Description)), ''), @CreatedBy
        );

        SET @FundTransferId = SCOPE_IDENTITY();

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0
            ROLLBACK TRANSACTION;

        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
        DECLARE @ErrorState INT = ERROR_STATE();

        RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END
GO

/* ---------------------------------------------------------------------------
   3. uspGetAllFundTransfers
      Read model for the Fund Transfer list/history screen.
   --------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE [Accounts].[uspGetAllFundTransfers]
    @BranchId INT,
    @FromDate DATE,
    @ToDate   DATE
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        ft.[Id]                     AS Id,
        ft.[TransferNumber]         AS TransferNumber,
        ft.[TransferDate]           AS TransferDate,
        ft.[SourceAccountId]        AS SourceAccountId,
        src.[Name]                  AS SourceAccountName,
        ft.[DestinationAccountId]   AS DestinationAccountId,
        dst.[Name]                  AS DestinationAccountName,
        ft.[Amount]                 AS Amount,
        ft.[ReferenceNo]            AS ReferenceNo,
        ft.[Description]            AS Description,
        ft.[CreatedBy]              AS CreatedBy,
        ft.[CreatedAt]              AS CreatedAt
    FROM [Accounts].[FundTransfer] ft
    INNER JOIN [Accounts].[Account] src ON src.[Id] = ft.[SourceAccountId]
    INNER JOIN [Accounts].[Account] dst ON dst.[Id] = ft.[DestinationAccountId]
    WHERE ft.[BranchId] = @BranchId
      AND ft.[TransferDate] >= @FromDate
      AND ft.[TransferDate] <= @ToDate
    ORDER BY ft.[TransferDate] DESC, ft.[Id] DESC;
END
GO
