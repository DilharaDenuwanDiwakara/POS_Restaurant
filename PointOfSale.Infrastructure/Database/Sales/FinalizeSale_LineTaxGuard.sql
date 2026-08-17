SET NOCOUNT ON;

DECLARE @sql NVARCHAR(MAX);

SELECT @sql = sm.definition
FROM sys.sql_modules sm
JOIN sys.objects o ON sm.object_id = o.object_id
JOIN sys.schemas s ON o.schema_id = s.schema_id
WHERE s.name = 'Sales'
  AND o.name = 'uspFinalizeSale';

IF @sql IS NULL
    THROW 52000, 'Sales.uspFinalizeSale was not found.', 1;

SET @sql = REPLACE(@sql, 'CREATE   PROCEDURE [Sales].[uspFinalizeSale]', 'ALTER PROCEDURE [Sales].[uspFinalizeSale]');
SET @sql = REPLACE(@sql, 'CREATE  PROCEDURE [Sales].[uspFinalizeSale]', 'ALTER PROCEDURE [Sales].[uspFinalizeSale]');
SET @sql = REPLACE(@sql, 'CREATE PROCEDURE [Sales].[uspFinalizeSale]', 'ALTER PROCEDURE [Sales].[uspFinalizeSale]');
SET @sql = REPLACE(
    @sql,
    'SUM((SL.Quantity * SL.UnitPrice) - SL.DiscountAmount - SL.TaxAmount)',
    'SUM((SL.Quantity * SL.UnitPrice) - SL.DiscountAmount - CASE WHEN @TaxAmount > 0 THEN SL.TaxAmount ELSE 0 END)');

IF @sql NOT LIKE '%CASE WHEN @TaxAmount > 0 THEN SL.TaxAmount ELSE 0 END%'
    THROW 52001, 'Patch marker was not applied to Sales.uspFinalizeSale.', 1;

EXEC sp_executesql @sql;
