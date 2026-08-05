IF COL_LENGTH('Inventory.ProductUnitConversion', 'IsMultiply') IS NULL
BEGIN
    ALTER TABLE [Inventory].[ProductUnitConversion]
    ADD [IsMultiply] BIT NOT NULL
        CONSTRAINT [DF_ProductUnitConversion_IsMultiply] DEFAULT (1);
END;
GO
