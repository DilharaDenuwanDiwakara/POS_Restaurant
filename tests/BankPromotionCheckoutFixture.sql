CREATE SCHEMA [Sales];
GO
CREATE SCHEMA [System];
GO
CREATE SCHEMA [Restaurant];
GO
CREATE SCHEMA [Inventory];
GO
CREATE SCHEMA [Accounts];
GO
CREATE SCHEMA [Logging];
GO
CREATE PROCEDURE [Logging].[uspLogError] @n int,@p nvarchar(128),@l int,@m nvarchar(max) AS RETURN;
GO
CREATE PROCEDURE [Sales].[uspFinalizeSale] AS RETURN;
GO
CREATE TYPE [Sales].[PaymentListType] AS TABLE
(PaymentTerminalId int NULL,PaymentMethod varchar(20),Amount decimal(18,2),ReferenceNumber nvarchar(100));
CREATE TABLE [Sales].[Sales] (Id bigint PRIMARY KEY,BranchId int,OrderId int,InvoiceNumber nvarchar(50),
    SubTotal decimal(18,2),DiscountAmount decimal(18,2),TaxAmount decimal(18,2),ServiceCharge decimal(18,2),CustomerId int,
    PaymentStatus nvarchar(20),AmountPaid decimal(18,2),BalanceAmount decimal(18,2),CashGiven decimal(18,2));
CREATE TABLE [Sales].[SalesLine] (SalesId bigint,ProductId int,Quantity decimal(18,3),UnitPrice decimal(18,2),DiscountAmount decimal(18,2),TaxAmount decimal(18,2));
CREATE TABLE [Sales].[Payment] (SalesId bigint,PaymentTerminalId int,PaymentMethod varchar(20),Amount decimal(18,2),ReferenceNumber nvarchar(100),CreatedBy int);
CREATE TABLE [Sales].[PaymentTerminal] (Id int,LinkedAccountId int);
CREATE TABLE [System].[Bank] (Id int PRIMARY KEY,BankName nvarchar(200));
CREATE TABLE [System].[AccountMapping] (MappingKey varchar(100),AccountId int);
CREATE TABLE [System].[TaxConfiguration] (TaxCode varchar(20),AccountCode varchar(20),IsActive bit);
CREATE TABLE [Restaurant].[Variant] (Id int PRIMARY KEY,MenuItemId int);
CREATE TABLE [Restaurant].[MenuItem] (Id int,Name nvarchar(100),MenuCategoryId int,TargetStationId int);
CREATE TABLE [Restaurant].[MenuCategory] (Id int,Name nvarchar(100),ParentCategoryId int,IncomeAccountCode varchar(20));
CREATE TABLE [Restaurant].[Station] (Id int,Name nvarchar(100));
CREATE TABLE [Restaurant].[Order] (Id int,Status varchar(20),IsPaid bit,OpenAccountId int,TableId int);
CREATE TABLE [Restaurant].[OrderItem] (Id int,OrderId int,Status varchar(20));
CREATE TABLE [Restaurant].[OrderItemStatusLog] (OrderItemId int,PreviousStatus varchar(20),NewStatus varchar(20),Reason nvarchar(100),ChangedBy int,ChangedAt datetime);
CREATE TABLE [Restaurant].[OpenAccount] (Id int,IsOpen bit,ClosedAt datetime);
CREATE TABLE [Restaurant].[RestaurantTable] (Id int,CurrentStatus varchar(20));
CREATE TABLE [Inventory].[WastageReason] (Id int IDENTITY,Name nvarchar(100),IsActive bit);
CREATE TABLE [Inventory].[Location] (Id int,Name nvarchar(100),BranchId int);
CREATE TABLE [Inventory].[Recipe] (VariantId int,ProductId int,QuantityRequired decimal(18,3),UnitMeasureId int);
CREATE TABLE [Inventory].[Product] (Id int,UnitMeasureId int,WastagePercentage decimal(18,2),CategoryId int);
CREATE TABLE [Inventory].[ProductUnitConversion] (ProductId int,TargetUnitMeasureId int,ConversionRate decimal(18,6));
CREATE TABLE [Inventory].[GlobalUnitConversion] (FromUnitMeasureId int,ToUnitMeasureId int,Multiplier decimal(18,6));
CREATE TABLE [Inventory].[Wastage] (Id int IDENTITY,BranchId int,LocationId int,WastageNumber nvarchar(100),WastageDate datetime,Note nvarchar(200),Status varchar(20),CreatedBy int);
CREATE TABLE [Inventory].[WastageLine] (WastageId int,ProductId int,BatchId bigint,WastageReasonId int,Quantity decimal(18,3),UnitCost decimal(18,2),UnitMeasureId int);
CREATE TABLE [Inventory].[LocationStock] (LocationId int,ProductId int,BatchId bigint,AvailableQuantity decimal(18,3));
CREATE TABLE [Inventory].[ProductBatch] (Id bigint,ProductId int,CostPrice decimal(18,2),ReceivedDate datetime);
CREATE TABLE [Inventory].[StockTransaction] (BranchId int,LocationId int,ProductId int,BatchId bigint,TransactionType varchar(20),ReferenceId bigint,DocumentNumber nvarchar(100),Quantity decimal(18,3),UnitCost decimal(18,2),CreatedBy int);
CREATE TABLE [Inventory].[Category] (Id int,Name nvarchar(100),ParentCategoryId int,CostOfSalesAccountCode varchar(20),StockAccountCode varchar(20));
CREATE TABLE [Accounts].[Account] (Id int PRIMARY KEY,Code varchar(20),IsActive bit);
CREATE TABLE [Accounts].[FiscalPeriod] (Id int,StartDate datetime,EndDate datetime,Status varchar(20));
CREATE TABLE [Accounts].[JournalEntry] (Id int IDENTITY,PeriodId int,EntryDate datetime,EntryNumber nvarchar(50),SourceType varchar(30),SourceId bigint,Reference nvarchar(50),Memo nvarchar(200),Status varchar(20),CreatedBy int,CreatedAt datetime,PostedAt datetime,PostedBy int,TotalDebit decimal(18,2),TotalCredit decimal(18,2));
CREATE TABLE [Accounts].[JournalEntryLine] (JournalEntryId int,AccountId int,EntityId int,DebitAmount decimal(18,2),CreditAmount decimal(18,2),Memo nvarchar(200));
INSERT INTO [System].[Bank] VALUES(1,'Test Bank'),(2,'Other Bank');
INSERT INTO [Restaurant].[Variant] VALUES(101,1),(102,1),(103,1);
INSERT INTO [Restaurant].[MenuItem] VALUES(1,'Test item',1,1);
INSERT INTO [Restaurant].[MenuCategory] VALUES(1,'Test category',NULL,'4101');
INSERT INTO [Restaurant].[Station] VALUES(1,'Kitchen');
INSERT INTO [Inventory].[Location] VALUES(1,'Kitchen',1);
INSERT INTO [Inventory].[Product] VALUES(101,1,0,1),(102,1,0,1),(103,1,0,1);
INSERT INTO [Inventory].[ProductBatch] VALUES(1,101,10,GETDATE()),(2,102,10,GETDATE()),(3,103,10,GETDATE());
INSERT INTO [Inventory].[LocationStock] VALUES(1,101,1,100),(1,102,2,100),(1,103,3,100);
INSERT INTO [Inventory].[Category] VALUES(1,'Test category',NULL,'5100','1200');
INSERT INTO [Accounts].[Account] VALUES(1,'1000',1),(2,'1100',1),(3,'4101',1),(4,'1200',1),(5,'4105',1),(6,'5200',1),(7,'5100',1);
INSERT INTO [System].[AccountMapping] VALUES('CASH_ACCOUNT',1),('CARD_ACCOUNT',2),('CASH_DISCOUNT_EXPENSE',5),('BANK_PROMO_EXPENSE',6);
INSERT INTO [Sales].[PaymentTerminal] VALUES(1,2);
INSERT INTO [Sales].[Sales] VALUES(1,1,NULL,'TEST-001',380,10,0,0,NULL,'UNPAID',0,380,0);
INSERT INTO [Sales].[SalesLine] VALUES(1,101,1,100,0,0),(1,102,1,200,0,0),(1,103,1,100,20,0);
GO
-- Contract stubs for repository persistence tests. Production hold/recall procedures are unchanged.
CREATE TYPE Sales.SalesLineType AS TABLE
(ProductId int,Quantity decimal(18,3),UnitPrice decimal(18,2),DiscountAmount decimal(18,2),TaxAmount decimal(18,2));
GO
CREATE PROCEDURE Sales.uspHoldSale
    @BranchId int,@ShiftId int=NULL,@CustomerId int=NULL,@TotalAmount decimal(18,2),@Discount decimal(18,2),
    @TaxAmount decimal(18,2),@ServiceChargeAmount decimal(18,2),@IsTaxInvoice bit,@TaxInvoiceNumber nvarchar(50)=NULL,
    @CreatedBy int,@OrderId bigint=NULL,@CurrentSalesId bigint=NULL,@SalesLines Sales.SalesLineType READONLY,@SalesId bigint OUTPUT
AS
BEGIN
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    IF @CurrentSalesId IS NULL
    BEGIN
        SELECT @SalesId=ISNULL(MAX(Id),0)+1 FROM Sales.Sales;
        INSERT INTO Sales.Sales(Id,BranchId,InvoiceNumber,SubTotal,DiscountAmount,TaxAmount,ServiceCharge,CustomerId,PaymentStatus)
        VALUES(@SalesId,@BranchId,'TEST-HOLD',@TotalAmount,@Discount,@TaxAmount,@ServiceChargeAmount,@CustomerId,'UNPAID');
    END
    ELSE
    BEGIN
        SET @SalesId=@CurrentSalesId;
        UPDATE Sales.Sales SET SubTotal=@TotalAmount,DiscountAmount=@Discount WHERE Id=@SalesId;
        DELETE FROM Sales.SalesLine WHERE SalesId=@SalesId;
    END;
    INSERT INTO Sales.SalesLine SELECT @SalesId,ProductId,Quantity,UnitPrice,DiscountAmount,TaxAmount FROM @SalesLines;
    COMMIT TRANSACTION;
END;
GO
CREATE PROCEDURE Sales.uspRecallHeldSale @SalesId bigint AS
BEGIN
    SELECT Id AS SalesId,InvoiceNumber,CustomerId,SubTotal AS TotalAmount,DiscountAmount AS Discount,
        TaxAmount,ServiceCharge AS ServiceChargeAmount,CAST(OrderId AS bigint) AS OrderId,CAST(NULL AS int) AS ShiftId
    FROM Sales.Sales WHERE Id=@SalesId;
    SELECT ProductId,'Test item' AS ProductName,Quantity,UnitPrice,DiscountAmount,TaxAmount FROM Sales.SalesLine WHERE SalesId=@SalesId;
END;
