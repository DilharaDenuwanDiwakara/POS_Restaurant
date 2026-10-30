-- Run BankPromotionSchema.sql first, then this script, then uspFinalizeSale.BankPromotion.sql.
-- Existing PromoType and PromoBankId values are preserved.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF COL_LENGTH(N'Sales.Sales', N'PromoType') IS NULL
    ALTER TABLE [Sales].[Sales] ADD PromoType VARCHAR(20) NULL;
IF COL_LENGTH(N'Sales.Sales', N'PromoBankId') IS NULL
    ALTER TABLE [Sales].[Sales] ADD PromoBankId INT NULL;
IF COL_LENGTH(N'Sales.Sales', N'BankPromotionId') IS NULL
    ALTER TABLE [Sales].[Sales] ADD BankPromotionId INT NULL;
IF COL_LENGTH(N'Sales.Sales', N'BankDiscountAmount') IS NULL
    ALTER TABLE [Sales].[Sales] ADD BankDiscountAmount DECIMAL(18,2) NOT NULL
        CONSTRAINT DF_Sales_BankDiscountAmount DEFAULT(0) WITH VALUES;
COMMIT TRANSACTION;

-- Configure BANK_PROMO_EXPENSE in System.AccountMapping using an approved expense account.
-- CASH_DISCOUNT_EXPENSE is optional: the existing active account 4105 is its fallback.
-- No account codes or account mappings are invented by this migration.
