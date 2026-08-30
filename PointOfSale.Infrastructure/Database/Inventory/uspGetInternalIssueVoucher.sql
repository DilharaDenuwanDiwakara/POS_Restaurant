-- ============================================================================
-- DRAFT / REFERENCE ONLY.
-- Column and table names below are inferred from PointOfSale.Core.Models.Inventory
-- (InternalIssue.cs / InternalIssueLine.cs) and are NOT verified against the live
-- schema. Adjust table names, column names and joins to match your actual
-- Inventory.InternalIssue / Inventory.InternalIssueLine tables before deploying.
--
-- Called by InternalIssueRepository.GetInternalIssueVoucherAsync via a
-- SqlDataAdapter.Fill, so this must return ONE result set shaped for the
-- InternalIssueVoucher.rpt Crystal Report data source (one row per issue line,
-- with the header fields repeated on every row is the simplest binding for a
-- single-section voucher report; adjust if the report uses a group header/subreport
-- instead).
-- ============================================================================
CREATE OR ALTER PROCEDURE [Inventory].[uspGetInternalIssueVoucher]
    @InternalIssueId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        ii.Id                   AS InternalIssueId,
        ii.IssueNumber,
        ii.IssueDate,
        ii.IssueType,
        st.Name                 AS StationName,
        loc.Name                AS LocationName,
        br.Name                 AS BranchName,
        ta.Code                 AS TargetAccountCode,
        ta.Name                 AS TargetAccountName,
        ii.Remarks,
        ii.TotalValue,
        u.Username               AS CreatedByName,
        ii.CreatedAt,

        il.Id                   AS LineId,
        il.ProductId,
        il.VariantId,
        COALESCE(p.Name, v.Name) AS ItemName,
        COALESCE(p.Code, mi.ItemCode) AS ItemCode,
        um.Code                 AS UnitMeasureCode,
        il.Qty,
        il.UnitCost,
        il.LineTotal
    FROM [Inventory].[InternalIssue] ii
    INNER JOIN [Inventory].[InternalIssueLine] il ON il.InternalIssueId = ii.Id
    LEFT JOIN [Restaurant].[Station] st ON st.Id = ii.StationId
    LEFT JOIN [Inventory].[Location] loc ON loc.Id = ii.LocationId
    LEFT JOIN [System].[Branch] br ON br.Id = ii.BranchId
    LEFT JOIN [Accounts].[Account] ta ON ta.Id = ii.TargetAccountId
    LEFT JOIN [System].[User] u ON u.Id = ii.CreatedBy
    LEFT JOIN [Inventory].[Product] p ON p.Id = il.ProductId
    LEFT JOIN [Inventory].[UnitMeasure] um ON um.Id = p.UnitMeasureId
    LEFT JOIN [Restaurant].[Variant] v ON v.Id = il.VariantId
    LEFT JOIN [Restaurant].[MenuItem] mi ON mi.Id = v.MenuItemId
    WHERE ii.Id = @InternalIssueId
    ORDER BY il.Id;
END
GO
