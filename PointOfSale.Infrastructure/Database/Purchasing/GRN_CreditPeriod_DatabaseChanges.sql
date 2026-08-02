/* GRN credit period support
   Run this once before deploying the application changes.
   Then merge the stored procedure snippets into the live procedures if their
   full definitions differ from this repository's database scripts. */

IF COL_LENGTH('Purchasing.GoodsReceiveNote', 'CreditDays') IS NULL
BEGIN
    ALTER TABLE [Purchasing].[GoodsReceiveNote]
    ADD [CreditDays] INT NOT NULL
        CONSTRAINT [DF_GoodsReceiveNote_CreditDays] DEFAULT (0);
END;
GO

IF COL_LENGTH('Purchasing.GoodsReceiveNote', 'DueDate') IS NULL
BEGIN
    ALTER TABLE [Purchasing].[GoodsReceiveNote]
    ADD [DueDate] DATETIME NULL;
END;
GO

UPDATE [Purchasing].[GoodsReceiveNote]
SET [DueDate] = DATEADD(DAY, ISNULL([CreditDays], 0), CAST([ReceivedDate] AS DATETIME))
WHERE [DueDate] IS NULL;
GO

ALTER TABLE [Purchasing].[GoodsReceiveNote]
ALTER COLUMN [DueDate] DATETIME NOT NULL;
GO

/* Required changes for [Purchasing].[uspInsertGoodsReceiveNote]

1. Add these parameters:

    @CreditDays INT = 0,
    @DueDate DATETIME = NULL,

2. Add these columns to the [Purchasing].[GoodsReceiveNote] INSERT column list:

    [CreditDays],
    [DueDate],

3. Add these values to the matching INSERT VALUES list:

    ISNULL(@CreditDays, 0),
    COALESCE(@DueDate, DATEADD(DAY, ISNULL(@CreditDays, 0), CAST(@ReceivedDate AS DATETIME))),

The application repository now passes @CreditDays and @DueDate into
[Purchasing].[uspInsertGoodsReceiveNote].
*/

/* Required SELECT changes

Add these columns to every GRN header/list stored procedure that returns
[Purchasing].[GoodsReceiveNote] rows, especially:

    [Purchasing].[uspGetAllGoodsReceiveNotes]
    [Purchasing].[uspGetGoodsReceiveNotesForApproval]

Use the aliases expected by the repository mapper:

    G.[CreditDays],
    G.[DueDate],

where G is the [Purchasing].[GoodsReceiveNote] alias in the procedure.
*/

/* Required changes for [Purchasing].[uspResubmitRejectedGoodsReceiveNote]

This repository contains the updated full procedure script at:
PointOfSale.Infrastructure\Database\Purchasing\uspResubmitRejectedGoodsReceiveNote.sql

The important changes are:

    @CreditDays INT = 0,
    @DueDate DATETIME = NULL,

and in the UPDATE statement:

    [CreditDays] = ISNULL(@CreditDays, 0),
    [DueDate] = COALESCE(@DueDate, DATEADD(DAY, ISNULL(@CreditDays, 0), CAST(@ReceivedDate AS DATETIME))),
*/
