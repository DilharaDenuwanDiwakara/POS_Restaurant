/*
Adds the navigation permission used by Wastage Approval.

The application checks this key in MainViewModel:
    NAV_WASTAGE_APPROVAL
*/

IF NOT EXISTS
(
    SELECT 1
    FROM [Auth].[Permission]
    WHERE [PermissionKey] = N'NAV_WASTAGE_APPROVAL'
)
BEGIN
    INSERT INTO [Auth].[Permission]
    (
        [Module],
        [PermissionKey],
        [Description]
    )
    VALUES
    (
        N'Inventory',
        N'NAV_WASTAGE_APPROVAL',
        N'Access wastage approval'
    );
END;
GO
