ALTER PROCEDURE [Restaurant].[rptGetMenuProfitability]
    @CategoryId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    WITH CategoryTree AS
    (
        SELECT
            mc.[Id]
        FROM [Restaurant].[MenuCategory] mc
        WHERE @CategoryId IS NOT NULL
          AND mc.[Id] = @CategoryId

        UNION ALL

        SELECT
            child.[Id]
        FROM [Restaurant].[MenuCategory] child
        INNER JOIN CategoryTree parent ON parent.[Id] = child.[ParentId]
    ),
    RecipeCosting AS
    (
        SELECT
            r.[VariantId],
            COUNT(r.[Id]) AS TotalIngredients,
            SUM(
                [Inventory].[fnConvertQuantity](r.ProductId, r.UnitMeasureId, p.UnitMeasureId, r.QuantityRequired)
                * (p.StandardCost / NULLIF((100.0 - ISNULL(p.WastagePercentage, 0.0)) / 100.0, 0))
            ) AS CalculatedBOMCost
        FROM [Inventory].[Recipe] r
        INNER JOIN [Inventory].[Product] p ON p.[Id] = r.[ProductId]
        WHERE p.[IsActive] = 1
        GROUP BY r.[VariantId]
    )
    SELECT
        ISNULL(mc.[Name], 'Uncategorized') AS CategoryName,
        m.[Name] AS MenuItemName,
        v.[ItemCode] AS [ItemCode],
        v.[Name] AS VariantName,
        ISNULL(rc.CalculatedBOMCost, 0) AS TotalBOMCost,
        ISNULL(v.[FinalAmount], 0) AS SellingPrice,

        (ISNULL(v.[FinalAmount], 0) - ISNULL(rc.CalculatedBOMCost, 0)) AS GrossProfit,

        CASE
            WHEN ISNULL(v.[FinalAmount], 0) > 0
            THEN (ISNULL(rc.CalculatedBOMCost, 0) / v.[FinalAmount]) * 100.0
            ELSE 0
        END AS FoodCostPercentage,

        CASE
            WHEN rc.VariantId IS NULL OR rc.TotalIngredients = 0 THEN 'Missing Recipe'
            WHEN ISNULL(rc.CalculatedBOMCost, 0) = 0 THEN 'Missing Cost'
            WHEN (ISNULL(rc.CalculatedBOMCost, 0) / NULLIF(v.[FinalAmount], 0)) * 100.0 > 35.0 THEN 'High Cost'
            ELSE 'Healthy'
        END AS BOMStatus
    FROM [Restaurant].[MenuItem] m
    INNER JOIN [Restaurant].[Variant] v ON v.[MenuItemId] = m.[Id]
    LEFT JOIN [Restaurant].[MenuCategory] mc ON mc.[Id] = m.[MenuCategoryId]
    LEFT JOIN RecipeCosting rc ON rc.[VariantId] = v.[Id]
    WHERE m.[IsActive] = 1
      AND (
            @CategoryId IS NULL
            OR m.[MenuCategoryId] IN (SELECT [Id] FROM CategoryTree)
          )
    ORDER BY FoodCostPercentage DESC
    OPTION (MAXRECURSION 100);
END;
