SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER PROCEDURE [Accounts].[uspGetAllExpenses]
    @BranchId INT,
    @FromDate DATE,
    @ToDate DATE
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        eh.Id,
        eh.VoucherNo AS VoucherNumber,
        eh.ExpenseDate AS ExpensesDate,
        eh.BranchId,
        ExpenseAccount = STUFF((
            SELECT DISTINCT ', ' + a2.Code + ' - ' + a2.Name
            FROM [Accounts].[ExpenseLine] el2
            INNER JOIN [Accounts].[Account] a2 ON a2.Id = el2.AccountId
            WHERE el2.ExpenseHeaderId = eh.Id
            FOR XML PATH(''), TYPE
        ).value('.', 'NVARCHAR(MAX)'), 1, 2, ''),
        eh.Description,
        eh.TotalAmount AS Amount
    FROM [Accounts].[ExpenseHeader] eh
    WHERE eh.BranchId = @BranchId
      AND CAST(eh.ExpenseDate AS DATE) BETWEEN @FromDate AND @ToDate
    ORDER BY eh.ExpenseDate DESC, eh.Id DESC;
END
GO
