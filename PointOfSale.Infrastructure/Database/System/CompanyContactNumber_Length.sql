IF COL_LENGTH('System.Company', 'ContactNumber') IS NOT NULL
BEGIN
    ALTER TABLE [System].[Company]
    ALTER COLUMN [ContactNumber] NVARCHAR(100) NULL;
END;
GO

IF COL_LENGTH('System.Branch', 'ContactNumber') IS NOT NULL
BEGIN
    ALTER TABLE [System].[Branch]
    ALTER COLUMN [ContactNumber] NVARCHAR(100) NULL;
END;
GO
