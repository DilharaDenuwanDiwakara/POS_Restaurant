-- Run against the POS database before using GetBankPromoWithExclusionsAsync.
-- Existing [System].[Bank] and [Restaurant].[Variant] tables are required.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'[Sales].[BankPromotion]', N'U') IS NULL
BEGIN
    CREATE TABLE [Sales].[BankPromotion]
    (
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_BankPromotion] PRIMARY KEY,
        [BankId] INT NOT NULL,
        [PromoName] NVARCHAR(200) NOT NULL,
        [DiscountPercentage] DECIMAL(5,2) NOT NULL,
        [IsActive] BIT NOT NULL CONSTRAINT [DF_BankPromotion_IsActive] DEFAULT (1),
        CONSTRAINT [FK_BankPromotion_Bank] FOREIGN KEY ([BankId]) REFERENCES [System].[Bank] ([Id]),
        CONSTRAINT [CK_BankPromotion_DiscountPercentage] CHECK ([DiscountPercentage] > 0 AND [DiscountPercentage] <= 100),
        CONSTRAINT [CK_BankPromotion_PromoName] CHECK (LEN(LTRIM(RTRIM([PromoName]))) > 0)
    );
    CREATE INDEX [IX_BankPromotion_BankId] ON [Sales].[BankPromotion] ([BankId]);
END;

-- Previously unscheduled offers start today; existing explicit schedules remain unchanged.
-- NOT NULL and a database constraint protect all callers, including direct SQL.
IF COL_LENGTH(N'Sales.BankPromotion', N'ValidFrom') IS NULL
    ALTER TABLE [Sales].[BankPromotion] ADD [ValidFrom] DATETIME NOT NULL
        CONSTRAINT [DF_BankPromotion_ValidFrom] DEFAULT (CONVERT(DATE, GETDATE())) WITH VALUES;
-- Upgrade the old default for future inserts without rewriting saved promotion dates.
IF EXISTS (SELECT 1 FROM sys.default_constraints
           WHERE parent_object_id = OBJECT_ID(N'[Sales].[BankPromotion]')
             AND name = N'DF_BankPromotion_ValidFrom' AND definition LIKE '%19000101%')
BEGIN
    ALTER TABLE [Sales].[BankPromotion] DROP CONSTRAINT [DF_BankPromotion_ValidFrom];
    ALTER TABLE [Sales].[BankPromotion] ADD CONSTRAINT [DF_BankPromotion_ValidFrom]
        DEFAULT (CONVERT(DATE, GETDATE())) FOR [ValidFrom];
END;
IF COL_LENGTH(N'Sales.BankPromotion', N'ValidTo') IS NULL
    ALTER TABLE [Sales].[BankPromotion] ADD [ValidTo] DATETIME NOT NULL
        CONSTRAINT [DF_BankPromotion_ValidTo] DEFAULT (DATEADD(SECOND, 86399, CONVERT(DATETIME, CONVERT(DATE, GETDATE())))) WITH VALUES;
-- Future inserts end today; existing saved end dates are not rewritten.
IF EXISTS (SELECT 1 FROM sys.default_constraints
           WHERE parent_object_id = OBJECT_ID(N'[Sales].[BankPromotion]')
             AND name = N'DF_BankPromotion_ValidTo' AND definition LIKE '%9999%')
BEGIN
    ALTER TABLE [Sales].[BankPromotion] DROP CONSTRAINT [DF_BankPromotion_ValidTo];
    ALTER TABLE [Sales].[BankPromotion] ADD CONSTRAINT [DF_BankPromotion_ValidTo]
        DEFAULT (DATEADD(SECOND, 86399, CONVERT(DATETIME, CONVERT(DATE, GETDATE())))) FOR [ValidTo];
END;
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'[Sales].[BankPromotion]')
               AND name = N'CK_BankPromotion_Validity')
    EXEC(N'ALTER TABLE [Sales].[BankPromotion] WITH CHECK ADD CONSTRAINT [CK_BankPromotion_Validity]
           CHECK ([ValidTo] > [ValidFrom]);');

IF OBJECT_ID(N'[System].[BankBINMapping]', N'U') IS NULL
BEGIN
    CREATE TABLE [System].[BankBINMapping]
    (
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_BankBINMapping] PRIMARY KEY,
        [BankId] INT NOT NULL,
        [BINPrefix] VARCHAR(6) NOT NULL,
        [IsActive] BIT NOT NULL CONSTRAINT [DF_BankBINMapping_IsActive] DEFAULT (1),
        CONSTRAINT [FK_BankBINMapping_Bank] FOREIGN KEY ([BankId]) REFERENCES [System].[Bank] ([Id]),
        CONSTRAINT [CK_BankBINMapping_BINPrefix] CHECK
            (DATALENGTH([BINPrefix]) = 6 AND [BINPrefix] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9]%')
    );
    -- A BIN identifies one active bank globally.
    CREATE UNIQUE INDEX [UX_BankBINMapping_ActiveBINPrefix]
        ON [System].[BankBINMapping] ([BINPrefix]) WHERE [IsActive] = 1;
    CREATE UNIQUE INDEX [UX_BankBINMapping_BankId_BINPrefix]
        ON [System].[BankBINMapping] ([BankId], [BINPrefix]);
END;

-- Upgrade the former promotion-owned BIN table without losing existing assignments.
-- Dynamic SQL avoids compiling references to columns before they are added/dropped.
IF COL_LENGTH(N'System.BankBINMapping', N'BankPromotionId') IS NOT NULL
BEGIN
    EXEC(N'ALTER TABLE [System].[BankBINMapping] ADD [BankId] INT NULL;');
    EXEC(N'UPDATE bm SET BankId = p.BankId FROM [System].[BankBINMapping] bm
           JOIN [Sales].[BankPromotion] p ON p.Id = bm.BankPromotionId;');
    ALTER TABLE [System].[BankBINMapping] DROP CONSTRAINT [FK_BankBINMapping_BankPromotion];
    DROP INDEX [IX_BankBINMapping_BankPromotionId] ON [System].[BankBINMapping];
    EXEC(N'ALTER TABLE [System].[BankBINMapping] DROP COLUMN [BankPromotionId];');
    EXEC(N'ALTER TABLE [System].[BankBINMapping] ALTER COLUMN [BankId] INT NOT NULL;');
    EXEC(N';WITH duplicates AS
        (SELECT *, ROW_NUMBER() OVER (PARTITION BY BankId, BINPrefix ORDER BY IsActive DESC, Id) AS rn
         FROM [System].[BankBINMapping]) DELETE FROM duplicates WHERE rn > 1;');
    EXEC(N'ALTER TABLE [System].[BankBINMapping] ADD CONSTRAINT [FK_BankBINMapping_Bank]
           FOREIGN KEY ([BankId]) REFERENCES [System].[Bank] ([Id]);');
    EXEC(N'CREATE UNIQUE INDEX [UX_BankBINMapping_BankId_BINPrefix]
           ON [System].[BankBINMapping] ([BankId], [BINPrefix]);');
END;

-- Card metadata is nullable for existing, unclassified BINs. Such BINs only match All offers.
IF COL_LENGTH(N'System.BankBINMapping', N'CardType') IS NULL
    ALTER TABLE [System].[BankBINMapping] ADD [CardType] VARCHAR(20) NULL;
IF COL_LENGTH(N'System.BankBINMapping', N'CardBrand') IS NULL
    ALTER TABLE [System].[BankBINMapping] ADD [CardBrand] VARCHAR(30) NULL;
IF COL_LENGTH(N'Sales.BankPromotion', N'RequiredCardType') IS NULL
    ALTER TABLE [Sales].[BankPromotion] ADD [RequiredCardType] VARCHAR(20) NOT NULL
        CONSTRAINT [DF_BankPromotion_RequiredCardType] DEFAULT ('All') WITH VALUES;

IF OBJECT_ID(N'[Sales].[BankPromoExcludedVariant]', N'U') IS NULL
BEGIN
    CREATE TABLE [Sales].[BankPromoExcludedVariant]
    (
        [BankPromotionId] INT NOT NULL,
        [VariantId] INT NOT NULL,
        CONSTRAINT [PK_BankPromoExcludedVariant] PRIMARY KEY ([BankPromotionId], [VariantId]),
        CONSTRAINT [FK_BankPromoExcludedVariant_BankPromotion] FOREIGN KEY ([BankPromotionId]) REFERENCES [Sales].[BankPromotion] ([Id]),
        CONSTRAINT [FK_BankPromoExcludedVariant_Variant] FOREIGN KEY ([VariantId]) REFERENCES [Restaurant].[Variant] ([Id])
    );
    CREATE INDEX [IX_BankPromoExcludedVariant_VariantId]
        ON [Sales].[BankPromoExcludedVariant] ([VariantId]);
END;

COMMIT TRANSACTION;
