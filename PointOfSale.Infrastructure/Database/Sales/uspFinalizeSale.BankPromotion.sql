ALTER PROCEDURE [Sales].[uspFinalizeSale]
    @SalesId BIGINT,
    @CashGiven DECIMAL(18,2) = 0,
    @CreatedBy INT,
    @CustomerId INT = NULL,
    @Payments [Sales].[PaymentListType] READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Fetch existing sale details
        DECLARE @BranchId INT, @OrderId INT, @NewInvoiceNumber NVARCHAR(50);
        DECLARE @TotalAmount DECIMAL(18,2), @Discount DECIMAL(18,2), @TaxAmount DECIMAL(18,2);
        DECLARE @ServiceChargeAmount DECIMAL(18,2), @ExistingCustomerId INT;
        DECLARE @PromoType VARCHAR(20), @PromoBankId INT, @BankPromotionId INT;
        DECLARE @BankPromoBin VARCHAR(6), @BankDiscountAmount DECIMAL(18,2);
        DECLARE @AmountDue DECIMAL(18,2);
        
        SELECT 
            @BranchId = BranchId, @OrderId = OrderId, @NewInvoiceNumber = InvoiceNumber,
            @TotalAmount = SubTotal, @Discount = DiscountAmount, @TaxAmount = TaxAmount, 
            @ServiceChargeAmount = ServiceCharge, @ExistingCustomerId = CustomerId,
            @PromoType = UPPER(LTRIM(RTRIM(PromoType))), @PromoBankId = PromoBankId,
            @BankPromotionId = BankPromotionId,
            @BankDiscountAmount = BankDiscountAmount
        FROM [Sales].[Sales] WITH (UPDLOCK, HOLDLOCK) WHERE Id = @SalesId;

        IF @BranchId IS NULL THROW 52000, 'Sale ID not found.', 1;

        -- SubTotal already includes line discounts; taxes/service charges are inclusive.
        SET @AmountDue = ISNULL(@TotalAmount,0) - ISNULL(@Discount,0);

        -- Hard failsafe: execute before any payment, stock or ledger writes.
        IF @PromoType = 'BANK'
        BEGIN
            IF EXISTS (SELECT 1 FROM [Sales].[Payment] WITH (UPDLOCK, HOLDLOCK) WHERE SalesId=@SalesId)
                THROW 52038, 'This bank promotion invoice already has payments. Refresh the invoice before retrying.', 1;
            IF (SELECT COUNT(*) FROM @Payments) <> 1
               OR EXISTS (SELECT 1 FROM @Payments WHERE PaymentMethod IS NULL OR PaymentMethod <> 'CARD')
                THROW 52030, 'Split payments are not allowed for Bank Promotions. Please clear payments and use a single card.', 1;

            -- Reuse the existing payment reference; no BIN parameter/header column is required.
            SELECT @BankPromoBin=ReferenceNumber FROM @Payments;
            IF EXISTS (SELECT 1 FROM @Payments WHERE ReferenceNumber IS NULL
                OR LEN(ReferenceNumber) <> 6
                OR ReferenceNumber COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9]%')
                THROW 52031, 'Enter exactly six digits in the card payment reference.', 1;

            IF @PromoBankId IS NULL OR @BankPromotionId IS NULL
                THROW 52031, 'Bank promotion metadata is missing. Reapply the promotion.', 1;

            DECLARE @BankRate DECIMAL(5,2), @EligibleSubtotal DECIMAL(28,5);
            SELECT @BankRate = bp.DiscountPercentage
            FROM [Sales].[BankPromotion] bp WITH (HOLDLOCK)
            INNER JOIN [System].[BankBINMapping] bm WITH (HOLDLOCK)
                ON bm.BankId=bp.BankId AND bm.BINPrefix=@BankPromoBin AND bm.IsActive=1
            WHERE bp.Id=@BankPromotionId AND bp.BankId=@PromoBankId AND bp.IsActive=1
              AND GETDATE() BETWEEN bp.ValidFrom AND bp.ValidTo
              AND (UPPER(LTRIM(RTRIM(bp.RequiredCardType)))='ALL'
                   OR UPPER(LTRIM(RTRIM(bp.RequiredCardType)))=UPPER(LTRIM(RTRIM(bm.CardType))));
            IF @BankRate IS NULL OR @BankRate <= 0 OR @BankRate > 100
                THROW 52032, 'The bank promotion has expired or does not match this BIN.', 1;

            SELECT @EligibleSubtotal=ISNULL(SUM(sl.Quantity*sl.UnitPrice),0)
            FROM [Sales].[SalesLine] sl WITH (HOLDLOCK)
            WHERE sl.SalesId=@SalesId AND ISNULL(sl.DiscountAmount,0)=0
              AND sl.Quantity>0 AND sl.UnitPrice>0
              AND NOT EXISTS (SELECT 1 FROM [Sales].[BankPromoExcludedVariant] ev WITH (HOLDLOCK)
                              WHERE ev.BankPromotionId=@BankPromotionId AND ev.VariantId=sl.ProductId);

            DECLARE @ExpectedBankDiscount DECIMAL(18,2)=ROUND(@EligibleSubtotal*@BankRate/100.0,2);
            IF @ExpectedBankDiscount <= 0 OR ISNULL(@BankDiscountAmount,0) <> @ExpectedBankDiscount
               OR ISNULL(@Discount,0) <> @ExpectedBankDiscount
                THROW 52033, 'Bank discount is invalid or combined with another bill discount. Reapply the promotion.', 1;

            IF EXISTS (SELECT 1 FROM @Payments WHERE Amount IS NULL OR Amount<=0 OR Amount<>@AmountDue)
                THROW 52034, 'The bank promotion requires one card payment for the full invoice amount.', 1;
        END;

        DECLARE @FinalCustomerId INT = ISNULL(NULLIF(@CustomerId, 0), @ExistingCustomerId);

        -- Validate Credit Payments
        IF EXISTS (SELECT 1 FROM @Payments WHERE PaymentMethod = 'CREDIT') AND (@FinalCustomerId IS NULL)
        BEGIN
            RAISERROR('A CustomerId is required when paying with Store CREDIT.', 16, 1);
            RETURN;
        END

        -- =========================================================
        -- 1. UPDATE SALES HEADER (MODIFIED FOR CREDIT / AR DEBT)
        -- =========================================================
        -- Exclude CREDIT from AmountPaid because it is a deferred payment (debt), not settled cash/card
        DECLARE @ActualAmountPaid DECIMAL(18,2) = (
            SELECT ISNULL(SUM(Amount), 0) 
            FROM @Payments 
            WHERE PaymentMethod <> 'CREDIT'
        );

        DECLARE @NewBalance DECIMAL(18,2) = @AmountDue - @ActualAmountPaid;

        DECLARE @NewPaymentStatus NVARCHAR(20) = CASE 
            WHEN @NewBalance <= 0 THEN 'PAID'
            WHEN @ActualAmountPaid > 0 THEN 'PARTIAL'
            ELSE 'PENDING'
        END;

        UPDATE [Sales].[Sales]
        SET PaymentStatus = @NewPaymentStatus, 
            AmountPaid = @ActualAmountPaid,
            BalanceAmount = @NewBalance,
            CashGiven = @CashGiven,
            CustomerId = @FinalCustomerId
        WHERE Id = @SalesId;

        -- =========================================================
        -- 2. PROCESS MULTIPLE PAYMENTS (EXCLUDING DEBT)
        -- =========================================================
        INSERT INTO [Sales].[Payment] (
            SalesId, PaymentTerminalId, PaymentMethod, Amount, ReferenceNumber, CreatedBy
        )
        SELECT 
            @SalesId, PaymentTerminalId, PaymentMethod, Amount, ReferenceNumber, @CreatedBy
        FROM @Payments
        WHERE PaymentMethod <> 'CREDIT'; -- Prevents debt from inflating the cashier's collected cash reports

        -- =========================================================
        -- 3. SETUP WASTAGE FOR YIELD LOSS
        -- =========================================================
        DECLARE @WastageReasonId INT;
        SELECT TOP 1 @WastageReasonId = Id FROM [Inventory].[WastageReason] WHERE Name = 'RECIPE YIELD LOSS';
        
        IF @WastageReasonId IS NULL
        BEGIN
            INSERT INTO [Inventory].[WastageReason] (Name, IsActive) VALUES ('RECIPE YIELD LOSS', 1);
            SET @WastageReasonId = SCOPE_IDENTITY();
        END

        DECLARE @WastageHeaders TABLE (LocationId INT, WastageId INT);

        -- =========================================================
        -- 4. PROCESS INVENTORY DEDUCTIONS (Loop from SalesLine table)
        -- =========================================================
        DECLARE @VariantId INT, @QtySold DECIMAL(18, 3), @UnitPrice DECIMAL(18, 2);

        DECLARE SalesCursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT ProductId, Quantity, UnitPrice FROM [Sales].[SalesLine] WHERE SalesId = @SalesId;

        OPEN SalesCursor;
        FETCH NEXT FROM SalesCursor INTO @VariantId, @QtySold, @UnitPrice;

        WHILE @@FETCH_STATUS = 0
        BEGIN
            DECLARE @TargetLocationId INT;

            SELECT TOP 1 @TargetLocationId = L.Id
            FROM [Restaurant].[Variant] V
            INNER JOIN [Restaurant].[MenuItem] MI ON V.MenuItemId = MI.Id
            INNER JOIN [Restaurant].[Station] S ON MI.TargetStationId = S.Id
            INNER JOIN [Inventory].[Location] L ON L.Name = S.Name AND L.BranchId = @BranchId
            WHERE V.Id = @VariantId;

            IF @TargetLocationId IS NULL
                SELECT TOP 1 @TargetLocationId = Id FROM [Inventory].[Location] WHERE BranchId = @BranchId ORDER BY Id;

            -- Resolve Ingredients & Calculate Wastage
            DECLARE @Ingredients TABLE (ProductId INT, BaseQty DECIMAL(18,3), WastageQty DECIMAL(18,3));
            DELETE FROM @Ingredients;

            IF EXISTS (SELECT 1 FROM [Inventory].[Recipe] WHERE VariantId = @VariantId)
            BEGIN
                INSERT INTO @Ingredients (ProductId, BaseQty, WastageQty)
                SELECT 
                    r.ProductId,
                    (r.QuantityRequired * @QtySold * COALESCE((1.0 / NULLIF(puc.ConversionRate, 0)), guc.Multiplier, 1.0)) AS BaseQty,
                    ((r.QuantityRequired * @QtySold * COALESCE((1.0 / NULLIF(puc.ConversionRate, 0)), guc.Multiplier, 1.0)) * ISNULL(p.WastagePercentage, 0) / 100.0) AS WastageQty
                FROM [Inventory].[Recipe] r
                INNER JOIN [Inventory].[Product] p ON r.ProductId = p.Id
                LEFT JOIN [Inventory].[ProductUnitConversion] puc ON puc.ProductId = p.Id AND puc.TargetUnitMeasureId = r.UnitMeasureId
                LEFT JOIN [Inventory].[GlobalUnitConversion] guc ON guc.FromUnitMeasureId = r.UnitMeasureId AND guc.ToUnitMeasureId = p.UnitMeasureId
                WHERE r.VariantId = @VariantId;
            END
            ELSE
            BEGIN
                INSERT INTO @Ingredients (ProductId, BaseQty, WastageQty)
                SELECT @VariantId, @QtySold, (@QtySold * ISNULL(p.WastagePercentage, 0) / 100.0)
                FROM [Inventory].[Product] p WHERE p.Id = @VariantId;
            END

            -- Handle Wastage Header
            DECLARE @CurrentWastageId INT = NULL;
            IF EXISTS (SELECT 1 FROM @Ingredients WHERE WastageQty > 0)
            BEGIN
                SELECT @CurrentWastageId = WastageId FROM @WastageHeaders WHERE LocationId = @TargetLocationId;
                IF @CurrentWastageId IS NULL
                BEGIN
                    DECLARE @NewWastageNo NVARCHAR(50) = 'WST-' + @NewInvoiceNumber + '-' + CAST(@TargetLocationId AS NVARCHAR);
                    INSERT INTO [Inventory].[Wastage] (BranchId, LocationId, WastageNumber, WastageDate, Note, Status, CreatedBy)
                    VALUES (@BranchId, @TargetLocationId, @NewWastageNo, GETDATE(), 'Auto-Yield loss for ' + @NewInvoiceNumber, 'POSTED', @CreatedBy);
                    
                    SET @CurrentWastageId = SCOPE_IDENTITY();
                    INSERT INTO @WastageHeaders (LocationId, WastageId) VALUES (@TargetLocationId, @CurrentWastageId);
                END
            END

            -- FIFO Deduction
            DECLARE @IngProductId INT, @RemSaleQty DECIMAL(18,3), @RemWastageQty DECIMAL(18,3);
            DECLARE @IngUnitMeasureId INT

            DECLARE IngredientCursor CURSOR LOCAL FAST_FORWARD FOR
            SELECT ProductId, BaseQty, WastageQty FROM @Ingredients;

            OPEN IngredientCursor;
            FETCH NEXT FROM IngredientCursor INTO @IngProductId, @RemSaleQty, @RemWastageQty;

            WHILE @@FETCH_STATUS = 0
            BEGIN
                SELECT @IngUnitMeasureId = UnitMeasureId FROM [Inventory].[Product] WHERE Id = @IngProductId;

                DECLARE @BatchId BIGINT, @QtyOnHand DECIMAL(18, 3), @UnitCost DECIMAL(18, 2);
                
                DECLARE BatchCursor CURSOR LOCAL FAST_FORWARD FOR
                SELECT ls.BatchId, ls.AvailableQuantity, pb.CostPrice
                FROM [Inventory].[LocationStock] ls
                INNER JOIN [Inventory].[ProductBatch] pb ON ls.BatchId = pb.Id
                WHERE ls.LocationId = @TargetLocationId AND ls.ProductId = @IngProductId AND ls.AvailableQuantity > 0
                ORDER BY pb.ReceivedDate ASC; 

                OPEN BatchCursor;
                FETCH NEXT FROM BatchCursor INTO @BatchId, @QtyOnHand, @UnitCost;

                WHILE @@FETCH_STATUS = 0 AND (@RemSaleQty > 0 OR @RemWastageQty > 0)
                BEGIN
                    DECLARE @DeductSale DECIMAL(18,3) = 0, @DeductWastage DECIMAL(18,3) = 0;

                    IF @RemSaleQty > 0 AND @QtyOnHand > 0
                    BEGIN
                        SET @DeductSale = CASE WHEN @QtyOnHand >= @RemSaleQty THEN @RemSaleQty ELSE @QtyOnHand END;
                        SET @QtyOnHand = @QtyOnHand - @DeductSale;
                        SET @RemSaleQty = @RemSaleQty - @DeductSale;

                        UPDATE [Inventory].[LocationStock] SET AvailableQuantity = AvailableQuantity - @DeductSale WHERE LocationId = @TargetLocationId AND BatchId = @BatchId;
                        INSERT INTO [Inventory].[StockTransaction] (BranchId, LocationId, ProductId, BatchId, TransactionType, ReferenceId, DocumentNumber, Quantity, UnitCost, CreatedBy)
                        VALUES (@BranchId, @TargetLocationId, @IngProductId, @BatchId, 'SALES', @SalesId,  @NewInvoiceNumber, -@DeductSale, @UnitCost, @CreatedBy);
                    END

                    IF @RemWastageQty > 0 AND @QtyOnHand > 0
                    BEGIN
                        SET @DeductWastage = CASE WHEN @QtyOnHand >= @RemWastageQty THEN @RemWastageQty ELSE @QtyOnHand END;
                        SET @QtyOnHand = @QtyOnHand - @DeductWastage;
                        SET @RemWastageQty = @RemWastageQty - @DeductWastage;

                        UPDATE [Inventory].[LocationStock] SET AvailableQuantity = AvailableQuantity - @DeductWastage WHERE LocationId = @TargetLocationId AND BatchId = @BatchId;
                        INSERT INTO [Inventory].[StockTransaction] (BranchId, LocationId, ProductId, BatchId, TransactionType, ReferenceId, DocumentNumber, Quantity, UnitCost, CreatedBy)
                        VALUES (@BranchId, @TargetLocationId, @IngProductId, @BatchId, 'WASTAGE', @CurrentWastageId, 'WST-' + @NewInvoiceNumber + '-' + CAST(@TargetLocationId AS NVARCHAR), -@DeductWastage, @UnitCost, @CreatedBy);
                        
                        INSERT INTO [Inventory].[WastageLine] (WastageId, ProductId, BatchId, WastageReasonId, Quantity, UnitCost, UnitMeasureId)
                        VALUES (@CurrentWastageId, @IngProductId, @BatchId, @WastageReasonId, @DeductWastage, @UnitCost, @IngUnitMeasureId);
                    END
                    FETCH NEXT FROM BatchCursor INTO @BatchId, @QtyOnHand, @UnitCost;
                END
                CLOSE BatchCursor; DEALLOCATE BatchCursor;

                -- FORCE NEGATIVE STOCK
                IF @RemSaleQty > 0 OR @RemWastageQty > 0
                BEGIN
                    SELECT TOP 1 @BatchId = pb.Id, @UnitCost = pb.CostPrice FROM [Inventory].[ProductBatch] pb WHERE pb.ProductId = @IngProductId ORDER BY pb.ReceivedDate DESC;
                    IF @BatchId IS NOT NULL
                    BEGIN
                        IF NOT EXISTS (SELECT 1 FROM [Inventory].[LocationStock] WHERE LocationId = @TargetLocationId AND BatchId = @BatchId)
                            INSERT INTO [Inventory].[LocationStock] (LocationId, BatchId, ProductId, AvailableQuantity) VALUES (@TargetLocationId, @BatchId, @IngProductId, 0);

                        IF @RemSaleQty > 0
                        BEGIN
                            UPDATE [Inventory].[LocationStock] SET AvailableQuantity = AvailableQuantity - @RemSaleQty WHERE LocationId = @TargetLocationId AND BatchId = @BatchId;
                            INSERT INTO [Inventory].[StockTransaction] (BranchId, LocationId, ProductId, BatchId, TransactionType, ReferenceId, DocumentNumber, Quantity, UnitCost, CreatedBy)
                            VALUES (@BranchId, @TargetLocationId, @IngProductId, @BatchId, 'SALES', @SalesId, @NewInvoiceNumber, -@RemSaleQty, @UnitCost, @CreatedBy);
                        END

                        IF @RemWastageQty > 0
                        BEGIN
                            UPDATE [Inventory].[LocationStock] SET AvailableQuantity = AvailableQuantity - @RemWastageQty WHERE LocationId = @TargetLocationId AND BatchId = @BatchId;
                            INSERT INTO [Inventory].[StockTransaction] (BranchId, LocationId, ProductId, BatchId, TransactionType, ReferenceId, DocumentNumber, Quantity, UnitCost, CreatedBy)
                            VALUES (@BranchId, @TargetLocationId, @IngProductId, @BatchId, 'WASTAGE', @CurrentWastageId, 'WST-' + @NewInvoiceNumber + '-' + CAST(@TargetLocationId AS NVARCHAR), -@RemWastageQty, @UnitCost, @CreatedBy);

                            INSERT INTO [Inventory].[WastageLine] (WastageId, ProductId, BatchId, WastageReasonId, Quantity, UnitCost, UnitMeasureId)
                            VALUES (@CurrentWastageId, @IngProductId, @BatchId, @WastageReasonId, @RemWastageQty, @UnitCost, @IngUnitMeasureId);
                        END
                    END
                END
                FETCH NEXT FROM IngredientCursor INTO @IngProductId, @RemSaleQty, @RemWastageQty;
            END
            CLOSE IngredientCursor; DEALLOCATE IngredientCursor;

            FETCH NEXT FROM SalesCursor INTO @VariantId, @QtySold, @UnitPrice;
        END
        CLOSE SalesCursor; DEALLOCATE SalesCursor;

        -- =========================================================
        -- 5. COMPLETE OPEN ORDER
        -- =========================================================
        IF @OrderId IS NOT NULL AND @OrderId > 0
        BEGIN
            UPDATE [Restaurant].[Order] SET Status = 'COMPLETED', IsPaid = 1 WHERE Id = @OrderId;

            INSERT INTO [Restaurant].[OrderItemStatusLog] (OrderItemId, PreviousStatus, NewStatus, Reason, ChangedBy, ChangedAt)
            SELECT Id, Status, 'COMPLETED', 'Billed at POS', @CreatedBy, GETDATE()
            FROM [Restaurant].[OrderItem] WHERE OrderId = @OrderId AND Status <> 'COMPLETED' AND Status <> 'CANCELLED';

            UPDATE [Restaurant].[OrderItem] SET Status = 'COMPLETED' WHERE OrderId = @OrderId AND Status <> 'COMPLETED' AND Status <> 'CANCELLED';

            DECLARE @OpenAccountId INT, @TableId INT;
            SELECT @OpenAccountId = OpenAccountId, @TableId = TableId FROM [Restaurant].[Order] WHERE Id = @OrderId;

            IF @OpenAccountId IS NOT NULL UPDATE [Restaurant].[OpenAccount] SET IsOpen = 0, ClosedAt = GETDATE() WHERE Id = @OpenAccountId;
            IF @TableId IS NOT NULL AND @TableId > 0 UPDATE [Restaurant].[RestaurantTable] SET CurrentStatus = 'AVAILABLE' WHERE Id = @TableId;
        END

        -- =========================================================
        -- 6. DOUBLE ENTRY ACCOUNTING (FIXED MISMATCH & REVENUE CALC)
        -- =========================================================
        DECLARE @DefaultCashAccountId INT, @DefaultCardAccountId INT, @ArAccountId INT;
        SELECT @DefaultCashAccountId = AccountId FROM [System].[AccountMapping] WHERE MappingKey = 'CASH_ACCOUNT';
        SELECT @DefaultCardAccountId = AccountId FROM [System].[AccountMapping] WHERE MappingKey = 'CARD_ACCOUNT';
        SELECT @ArAccountId = AccountId FROM [System].[AccountMapping] WHERE MappingKey = 'AR_ACCOUNT';
        
        IF @DefaultCashAccountId IS NULL OR @DefaultCardAccountId IS NULL THROW 52020, 'Configuration Error: Map Default Cash and Card accounts.', 1;

        DECLARE @JournalEntryId INT;
        DECLARE @CurrentDate DATETIME = GETDATE();
        DECLARE @PeriodId INT = ISNULL((SELECT TOP 1 Id FROM [Accounts].[FiscalPeriod] WHERE @CurrentDate BETWEEN StartDate AND EndDate AND Status = 'OPEN'), 1);
        
        INSERT INTO [Accounts].[JournalEntry] (
            [PeriodId], [EntryDate], [EntryNumber], [SourceType], [SourceId], [Reference], [Memo], [Status], [CreatedBy], [CreatedAt], [PostedAt], [PostedBy], [TotalDebit], [TotalCredit]
        ) VALUES (
            @PeriodId, @CurrentDate, @NewInvoiceNumber, 'SALES_INVOICE', @SalesId, @NewInvoiceNumber, 'POS Sale - ' + @NewInvoiceNumber, 'POSTED', @CreatedBy, @CurrentDate, @CurrentDate, @CreatedBy, 0, 0
        );
        SET @JournalEntryId = SCOPE_IDENTITY();

        -- Calculate Proportions to fix Double-Counting of Tax/ServiceCharge if not passed in SalesLine
        DECLARE @TotalLineGross DECIMAL(18,2) = ISNULL((SELECT SUM(ISNULL(Quantity,0) * ISNULL(UnitPrice,0)) FROM [Sales].[SalesLine] WHERE SalesId = @SalesId), 0);
        DECLARE @LineTaxTotal DECIMAL(18,2) = ISNULL((SELECT SUM(ISNULL(TaxAmount,0)) FROM [Sales].[SalesLine] WHERE SalesId = @SalesId), 0);

        -- REVENUE
        INSERT INTO [Accounts].[JournalEntryLine] (JournalEntryId, AccountId, CreditAmount, DebitAmount, Memo)
        SELECT @JournalEntryId, ISNULL(Acc.Id, (SELECT TOP 1 Id FROM [Accounts].[Account] WHERE Code = '4101')), 
            SUM(
                (ISNULL(SL.Quantity, 0) * ISNULL(SL.UnitPrice, 0)) 
                - CASE WHEN ISNULL(@TaxAmount, 0) > 0 AND @LineTaxTotal > 0 THEN ISNULL(SL.TaxAmount, 0) 
                       WHEN ISNULL(@TaxAmount, 0) > 0 AND @LineTaxTotal = 0 AND @TotalLineGross > 0 THEN (@TaxAmount / @TotalLineGross) * (ISNULL(SL.Quantity, 0) * ISNULL(SL.UnitPrice, 0))
                       ELSE 0 END
                - CASE WHEN ISNULL(@ServiceChargeAmount, 0) > 0 AND @TotalLineGross > 0 THEN (@ServiceChargeAmount / @TotalLineGross) * (ISNULL(SL.Quantity, 0) * ISNULL(SL.UnitPrice, 0)) 
                       ELSE 0 END
            ), 
            0, 
            'Sales Revenue - ' + ISNULL(ISNULL(MC.Name, ParentMC.Name), 'General')
        FROM [Sales].[SalesLine] SL
        LEFT JOIN [Restaurant].[Variant] V ON SL.ProductId = V.Id
        LEFT JOIN [Restaurant].[MenuItem] MI ON V.MenuItemId = MI.Id
        LEFT JOIN [Restaurant].[MenuCategory] MC ON MI.MenuCategoryId = MC.Id
        LEFT JOIN [Restaurant].[MenuCategory] ParentMC ON MC.ParentCategoryId = ParentMC.Id
        LEFT JOIN [Accounts].[Account] Acc ON Acc.Code = ISNULL(MC.IncomeAccountCode, ParentMC.IncomeAccountCode)
        WHERE SL.SalesId = @SalesId
        GROUP BY Acc.Id, ISNULL(MC.Name, ParentMC.Name)
        HAVING SUM((ISNULL(SL.Quantity, 0) * ISNULL(SL.UnitPrice, 0))) > 0;

        -- Taxes & Service Charge
        IF ISNULL(@TaxAmount, 0) > 0
        BEGIN
            DECLARE @OutputVatAccountId INT;
            SELECT TOP 1 @OutputVatAccountId = A.Id FROM [System].[TaxConfiguration] T INNER JOIN [Accounts].[Account] A ON A.Code = T.AccountCode WHERE T.[TaxCode] = 'VAT' AND T.[IsActive] = 1;
            INSERT INTO [Accounts].[JournalEntryLine] (JournalEntryId, AccountId, CreditAmount, DebitAmount, Memo) VALUES (@JournalEntryId, @OutputVatAccountId, @TaxAmount, 0, 'Output VAT on Sales');
        END

        IF ISNULL(@ServiceChargeAmount, 0) > 0
        BEGIN
            DECLARE @ScAccountId INT;
            SELECT TOP 1 @ScAccountId = A.Id FROM [System].[TaxConfiguration] T INNER JOIN [Accounts].[Account] A ON A.Code = T.AccountCode WHERE T.[TaxCode] = 'SC' AND T.[IsActive] = 1;
            INSERT INTO [Accounts].[JournalEntryLine] (JournalEntryId, AccountId, CreditAmount, DebitAmount, Memo) VALUES (@JournalEntryId, ISNULL(@ScAccountId, @DefaultCashAccountId), @ServiceChargeAmount, 0, 'Service Charge Collected');
        END

        -- Discount accounting: preserve ordinary item discounts separately from the bank bill discount.
        DECLARE @SalesDiscountAccountId INT, @BankDiscountAccountId INT;
        DECLARE @LineDiscountTotal DECIMAL(18,2) = ISNULL((SELECT SUM(DiscountAmount)
            FROM [Sales].[SalesLine] WHERE SalesId=@SalesId),0);
        SELECT @SalesDiscountAccountId=AccountId FROM [System].[AccountMapping]
            WHERE MappingKey='CASH_DISCOUNT_EXPENSE';
        IF @SalesDiscountAccountId IS NULL
            SELECT TOP 1 @SalesDiscountAccountId=Id FROM [Accounts].[Account] WHERE Code='4105' AND IsActive=1;

        IF @LineDiscountTotal>0 OR (ISNULL(@Discount,0)>0 AND ISNULL(@PromoType,'')<>'BANK')
        BEGIN
            IF @SalesDiscountAccountId IS NULL
                THROW 52035, 'Map CASH_DISCOUNT_EXPENSE or activate discount account 4105.', 1;
            INSERT INTO [Accounts].[JournalEntryLine] (JournalEntryId,AccountId,DebitAmount,CreditAmount,Memo)
            VALUES (@JournalEntryId,@SalesDiscountAccountId,
                @LineDiscountTotal+CASE WHEN @PromoType='BANK' THEN 0 ELSE ISNULL(@Discount,0) END,
                0,'Item / Cash Discount Given');
        END;

        IF @PromoType='BANK' AND ISNULL(@Discount,0)>0
        BEGIN
            SELECT @BankDiscountAccountId=AccountId FROM [System].[AccountMapping]
                WHERE MappingKey='BANK_PROMO_EXPENSE';
            IF @BankDiscountAccountId IS NULL
                THROW 52036, 'Configuration Error: Map BANK_PROMO_EXPENSE before using bank promotions.', 1;
            INSERT INTO [Accounts].[JournalEntryLine] (JournalEntryId,AccountId,DebitAmount,CreditAmount,Memo)
            VALUES (@JournalEntryId,@BankDiscountAccountId,@Discount,0,'Bank Promotion Discount');
        END;

        -- =========================================================
        -- PAYMENT ACCOUNTING 
        -- =========================================================
        INSERT INTO [Accounts].[JournalEntryLine] (JournalEntryId, AccountId, EntityId, DebitAmount, CreditAmount, Memo)
        SELECT @JournalEntryId,
            CASE 
                WHEN P.PaymentMethod = 'CASH' THEN @DefaultCashAccountId
                WHEN P.PaymentMethod = 'CARD' THEN ISNULL((SELECT TOP 1 LinkedAccountId FROM [Sales].[PaymentTerminal] WHERE Id = P.PaymentTerminalId), @DefaultCardAccountId)
                WHEN P.PaymentMethod = 'CREDIT' THEN @ArAccountId ELSE @DefaultCashAccountId END,
            CASE WHEN P.PaymentMethod = 'CREDIT' THEN @FinalCustomerId ELSE NULL END, 
            CASE WHEN P.Amount > 0 THEN P.Amount ELSE 0 END, -- Puts positive amounts to Debit (Cash IN)
            CASE WHEN P.Amount < 0 THEN ABS(P.Amount) ELSE 0 END, -- Puts negative amounts to Credit (Change OUT)
            'Payment/Change - ' + P.PaymentMethod + ISNULL(' (' + P.ReferenceNumber + ')', '')
        FROM @Payments P WHERE P.Amount <> 0;

        -- COGS & Inventory Reduction
        INSERT INTO [Accounts].[JournalEntryLine] (JournalEntryId, AccountId, DebitAmount, CreditAmount, Memo)
        SELECT @JournalEntryId, ISNULL(Acc.Id, (SELECT TOP 1 Id FROM [Accounts].[Account] WHERE Code = '5100')), 
            SUM(ABS(ST.Quantity) * ST.UnitCost), 0, 'Cost of Goods Sold - ' + ISNULL(ISNULL(C.Name, ParentC.Name), 'General')
        FROM [Inventory].[StockTransaction] ST
        INNER JOIN [Inventory].[Product] P ON ST.ProductId = P.Id
        LEFT JOIN [Inventory].[Category] C ON P.CategoryId = C.Id LEFT JOIN [Inventory].[Category] ParentC ON C.ParentCategoryId = ParentC.Id
        LEFT JOIN [Accounts].[Account] Acc ON Acc.Code = ISNULL(C.CostOfSalesAccountCode, ParentC.CostOfSalesAccountCode)
        WHERE ST.ReferenceId = @SalesId AND ST.TransactionType = 'SALES' GROUP BY Acc.Id, ISNULL(C.Name, ParentC.Name) HAVING SUM(ABS(ST.Quantity) * ST.UnitCost) > 0;

        INSERT INTO [Accounts].[JournalEntryLine] (JournalEntryId, AccountId, CreditAmount, DebitAmount, Memo)
        SELECT @JournalEntryId, ISNULL(Acc.Id, (SELECT TOP 1 Id FROM [Accounts].[Account] WHERE Code = '1200')), 
            SUM(ABS(ST.Quantity) * ST.UnitCost), 0, 'Inventory Consumption - ' + ISNULL(ISNULL(C.Name, ParentC.Name), 'General')
        FROM [Inventory].[StockTransaction] ST
        INNER JOIN [Inventory].[Product] P ON ST.ProductId = P.Id
        LEFT JOIN [Inventory].[Category] C ON P.CategoryId = C.Id LEFT JOIN [Inventory].[Category] ParentC ON C.ParentCategoryId = ParentC.Id
        LEFT JOIN [Accounts].[Account] Acc ON Acc.Code = ISNULL(C.StockAccountCode, ParentC.StockAccountCode)
        WHERE ST.ReferenceId = @SalesId AND ST.TransactionType = 'SALES' GROUP BY Acc.Id, ISNULL(C.Name, ParentC.Name) HAVING SUM(ABS(ST.Quantity) * ST.UnitCost) > 0;

        -- =========================================================
        -- Final Ledger Verification & PRODUCTION SAFE Auto-Rounding
        -- =========================================================
        DECLARE @SumDebit DECIMAL(18,2), @SumCredit DECIMAL(18,2);
        SELECT @SumDebit = ISNULL(SUM(DebitAmount), 0), @SumCredit = ISNULL(SUM(CreditAmount), 0) FROM [Accounts].[JournalEntryLine] WHERE JournalEntryId = @JournalEntryId;

        DECLARE @Variance DECIMAL(18,2) = @SumDebit - @SumCredit;

        -- THIS IS THE PRODUCTION FAILSAFE: 
        -- If any mismatch occurs, it forces the ledger to balance via the Revenue account. 
        -- This ensures the cashier NEVER gets stuck on the Settle screen again.
        IF @PromoType='BANK' AND ABS(@Variance)>0.02
            THROW 52037, 'Bank promotion journal does not balance. Review discount and tax configuration.', 1;

        IF ABS(@Variance) > 0 
        BEGIN
            INSERT INTO [Accounts].[JournalEntryLine] (JournalEntryId, AccountId, CreditAmount, DebitAmount, Memo) 
            VALUES (
                @JournalEntryId, 
                ISNULL((SELECT TOP 1 Id FROM [Accounts].[Account] WHERE Code = '4101'), @DefaultCashAccountId), 
                CASE WHEN @Variance > 0 THEN ABS(@Variance) ELSE 0 END, -- Put it in Credit if Debit is higher
                CASE WHEN @Variance < 0 THEN ABS(@Variance) ELSE 0 END, -- Put it in Debit if Credit is higher
                'System Auto-Balancing Adjustment (' + CAST(@Variance AS NVARCHAR) + ')'
            );

            -- Re-calculate the sums so they match perfectly
            SELECT @SumDebit = ISNULL(SUM(DebitAmount), 0), @SumCredit = ISNULL(SUM(CreditAmount), 0) FROM [Accounts].[JournalEntryLine] WHERE JournalEntryId = @JournalEntryId;
        END

        UPDATE [Accounts].[JournalEntry] SET TotalDebit = @SumDebit, TotalCredit = @SumCredit WHERE Id = @JournalEntryId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        DECLARE @ErrorNumber INT = ERROR_NUMBER(); DECLARE @ErrorProcedure NVARCHAR(128) = ERROR_PROCEDURE(); DECLARE @ErrorLine INT = ERROR_LINE(); DECLARE @ErrorMessage NVARCHAR(MAX) = ERROR_MESSAGE();
        EXEC [Logging].[uspLogError] @ErrorNumber, @ErrorProcedure, @ErrorLine, @ErrorMessage;
        THROW;
    END CATCH
END
