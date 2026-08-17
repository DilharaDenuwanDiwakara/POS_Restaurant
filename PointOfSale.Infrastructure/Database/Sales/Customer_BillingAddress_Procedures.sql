ALTER PROCEDURE [Sales].[uspInsertCustomer]
    @CustomerName NVARCHAR(100),
    @ContactNumber NVARCHAR(15),
    @BillingAddress NVARCHAR(500) = NULL,
    @IsTaxRegistered BIT,
    @TaxRegistrationNumber NVARCHAR(50) = NULL,
    @CreditLimit DECIMAL(18, 2),
    @IsActive BIT,
    @CreatedBy INT,
    @CustomerId INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [Sales].[Customer]
    (
        [Name],
        [ContactNumber],
        [BillingAddress],
        [IsTaxRegistered],
        [TaxRegistrationNumber],
        [CreditLimit],
        [Balance],
        [IsActive],
        [LoyaltyPoints],
        [CreatedBy],
        [CreatedAt]
    )
    VALUES
    (
        @CustomerName,
        @ContactNumber,
        NULLIF(LTRIM(RTRIM(@BillingAddress)), ''),
        @IsTaxRegistered,
        NULLIF(LTRIM(RTRIM(@TaxRegistrationNumber)), ''),
        @CreditLimit,
        0,
        @IsActive,
        0,
        @CreatedBy,
        GETDATE()
    );

    SET @CustomerId = SCOPE_IDENTITY();
END;
GO

ALTER PROCEDURE [Sales].[uspUpdateCustomer]
    @CustomerId INT,
    @CustomerName NVARCHAR(100),
    @ContactNumber NVARCHAR(15),
    @BillingAddress NVARCHAR(500) = NULL,
    @IsTaxRegistered BIT,
    @TaxRegistrationNumber NVARCHAR(50) = NULL,
    @CreditLimit DECIMAL(18, 2),
    @IsActive BIT,
    @CreatedBy INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [Sales].[Customer]
    SET
        [Name] = @CustomerName,
        [ContactNumber] = @ContactNumber,
        [BillingAddress] = NULLIF(LTRIM(RTRIM(@BillingAddress)), ''),
        [IsTaxRegistered] = @IsTaxRegistered,
        [TaxRegistrationNumber] = NULLIF(LTRIM(RTRIM(@TaxRegistrationNumber)), ''),
        [CreditLimit] = @CreditLimit,
        [IsActive] = @IsActive,
        [UpdatedBy] = @CreatedBy,
        [UpdatedAt] = GETDATE()
    WHERE [Id] = @CustomerId;
END;
GO

ALTER PROCEDURE [Sales].[uspGetAllCustomer]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        [Id],
        [Name],
        [ContactNumber],
        [BillingAddress],
        [IsTaxRegistered],
        [TaxRegistrationNumber],
        [CreditLimit],
        [Balance],
        [LoyaltyPoints],
        [IsActive]
    FROM [Sales].[Customer]
    ORDER BY [Name];
END;
GO

ALTER PROCEDURE [Sales].[uspGetCustomerById]
    @CustomerId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        [Id],
        [Name],
        [ContactNumber],
        [BillingAddress],
        [IsTaxRegistered],
        [TaxRegistrationNumber],
        [CreditLimit],
        [Balance],
        [LoyaltyPoints],
        [IsActive]
    FROM [Sales].[Customer]
    WHERE [Id] = @CustomerId;
END;
GO
