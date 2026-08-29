/*
    Recipe explosion read for Internal Stock Issue / Wastage.

    Existing BOM table detected:
        [Inventory].[Recipe]
            VariantId -> [Restaurant].[Variant].[Id]
            ProductId -> [Inventory].[Product].[Id]
            QuantityRequired -> ingredient quantity for one finished variant

    This does not modify [Inventory].[uspInsertInternalIssue].
*/

SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    c.name AS ColumnName,
    ty.name AS DataTypeName
FROM sys.tables t
INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
INNER JOIN sys.columns c ON c.object_id = t.object_id
INNER JOIN sys.types ty ON ty.user_type_id = c.user_type_id
WHERE
    (s.name = 'Inventory' AND t.name = 'Recipe')
    OR (s.name = 'Restaurant' AND t.name IN ('MenuItem', 'Variant'))
ORDER BY s.name, t.name, c.column_id;
GO

CREATE OR ALTER PROCEDURE [Inventory].[uspGetInternalIssueRecipeIngredients]
    @MenuItemId INT = NULL,
    @VariantId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ResolvedVariantId INT = @VariantId;

    IF (@ResolvedVariantId IS NULL AND @MenuItemId IS NOT NULL)
    BEGIN
        SELECT TOP (1) @ResolvedVariantId = v.Id
        FROM [Restaurant].[Variant] v
        WHERE v.MenuItemId = @MenuItemId
        ORDER BY
            CASE WHEN UPPER(LTRIM(RTRIM(v.Name))) = 'STANDARD' THEN 0 ELSE 1 END,
            v.Id;
    END;

    SELECT
        v.MenuItemId,
        r.VariantId,
        r.ProductId,
        p.Name AS ProductName,
        CAST(
            CASE
                WHEN ISNULL(r.UnitMeasureId, p.UnitMeasureId) = p.UnitMeasureId THEN r.QuantityRequired
                WHEN puc.Id IS NULL THEN NULL
                WHEN puc.IsMultiply = 1 THEN r.QuantityRequired * puc.ConversionRate
                ELSE r.QuantityRequired / NULLIF(puc.ConversionRate, 0)
            END AS DECIMAL(18, 3)) AS QuantityPerItem,
        p.StandardCost AS UnitCost,
        p.UnitMeasureId,
        COALESCE(um.Code, um.Name) AS UnitMeasureName
    FROM [Inventory].[Recipe] r
    INNER JOIN [Restaurant].[Variant] v ON v.Id = r.VariantId
    INNER JOIN [Inventory].[Product] p ON p.Id = r.ProductId
    LEFT JOIN [Inventory].[UnitMeasure] um ON um.Id = p.UnitMeasureId
    LEFT JOIN [Inventory].[ProductUnitConversion] puc
        ON puc.ProductId = r.ProductId
        AND puc.TargetUnitMeasureId = r.UnitMeasureId
        AND puc.IsActive = 1
    WHERE
        r.VariantId = @ResolvedVariantId
        AND p.IsActive = 1
    ORDER BY r.Id;
END;
GO

