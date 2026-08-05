CREATE OR ALTER PROCEDURE [Inventory].[sp_InsertProductUnitConversion]
    @ProductId INT,
    @TargetUnitMeasureId INT,
    @ConversionRate DECIMAL(18, 4),
    @IsMultiply BIT,
    @CreatedBy INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [Inventory].[ProductUnitConversion]
    (
        [ProductId],
        [TargetUnitMeasureId],
        [ConversionRate],
        [IsActive],
        [CreatedBy],
        [CreatedAt],
        [IsMultiply]
    )
    VALUES
    (
        @ProductId,
        @TargetUnitMeasureId,
        @ConversionRate,
        1,
        @CreatedBy,
        GETDATE(),
        @IsMultiply
    );
END;
GO
