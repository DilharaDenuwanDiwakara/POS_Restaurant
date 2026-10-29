using System;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using PointOfSale.Core.Models.Sales;
using PointOfSale.Core.Models.System;
using PointOfSale.Core.Services;
using PointOfSale.Infrastructure.Repositories.System;
using PointOfSale.Infrastructure;
using PointOfSale.Infrastructure.Repositories.Sales;
using PointOfSale.Infrastructure.Repositories.Restaurant;

internal static class BankPromotionChecks
{
    private static void Execute(SqlConnection connection, string sql)
    {
        using (var command = new SqlCommand(sql, connection)) command.ExecuteNonQuery();
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void RejectSql(SqlConnection connection, string sql)
    {
        try { Execute(connection, sql); }
        catch (SqlException ex) when (ex.Number == 547 || ex.Number == 2601 || ex.Number == 2627) { return; }
        throw new Exception("Expected a constraint violation: " + sql);
    }

    private static int Main(string[] args)
    {
        var database = "BankPromoChecks_" + Guid.NewGuid().ToString("N");
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = "master", IntegratedSecurity = true
        };
        using (var master = new SqlConnection(builder.ConnectionString))
        {
            master.Open();
            Execute(master, "CREATE DATABASE [" + database + "];");
            try
            {
                builder.InitialCatalog = database;
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                config.AppSettings.Settings.Add("ActiveConnection", "BankPromotionChecks");
                config.ConnectionStrings.ConnectionStrings.Add(new ConnectionStringSettings("BankPromotionChecks", builder.ConnectionString));
                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
                ConfigurationManager.RefreshSection("connectionStrings");

                using (var connection = new SqlConnection(builder.ConnectionString))
                {
                    connection.Open();
                    Execute(connection, "CREATE SCHEMA [System];");
                    Execute(connection, "CREATE SCHEMA [Restaurant];");
                    Execute(connection, "CREATE SCHEMA [Sales];");
                    Execute(connection, @"
CREATE TABLE [System].[Bank] (Id int PRIMARY KEY, BankName nvarchar(200));
CREATE TABLE [Restaurant].[MenuItem] (Id int PRIMARY KEY, Name nvarchar(200), MenuCategoryId int, IsActive bit);
CREATE TABLE [Restaurant].[Variant] (Id int PRIMARY KEY, ItemCode varchar(20), Name nvarchar(200), Price decimal(18,2), MenuItemId int);
INSERT INTO [System].[Bank] VALUES (1,N'First Bank'), (2,N'Second Bank'), (3,N'Third Bank');
INSERT INTO [Restaurant].[MenuItem] VALUES (1,N'Tea',1,1), (2,N'Rice',2,1), (3,N'Inactive',1,0);
INSERT INTO [Restaurant].[Variant] VALUES (101,'A001',N'Large',100,1), (102,'A002',N'Small',50,1), (201,'B001',N'STANDARD',200,2), (301,'C001',N'STANDARD',100,3);
-- Reproduce the former schema to exercise an actual upgrade.
CREATE TABLE [Sales].[BankPromotion] (Id int IDENTITY PRIMARY KEY, BankId int, PromoName nvarchar(200), DiscountPercentage decimal(5,2), IsActive bit);
INSERT INTO [Sales].[BankPromotion] VALUES (1,N'Legacy',20,1), (1,N'Old inactive',10,0);
CREATE TABLE [System].[BankBINMapping] (Id int IDENTITY PRIMARY KEY, BankPromotionId int NOT NULL, BINPrefix varchar(6) NOT NULL, IsActive bit NOT NULL DEFAULT 1,
CONSTRAINT FK_BankBINMapping_BankPromotion FOREIGN KEY (BankPromotionId) REFERENCES [Sales].[BankPromotion](Id),
CONSTRAINT CK_BankBINMapping_BINPrefix CHECK (DATALENGTH(BINPrefix)=6 AND BINPrefix COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9]%'));
CREATE UNIQUE INDEX UX_BankBINMapping_ActiveBINPrefix ON [System].[BankBINMapping](BINPrefix) WHERE IsActive=1;
CREATE INDEX IX_BankBINMapping_BankPromotionId ON [System].[BankBINMapping](BankPromotionId);
INSERT INTO [System].[BankBINMapping](BankPromotionId,BINPrefix,IsActive) VALUES (1,'012345',1),(2,'012345',0),(2,'234567',0);");
                    var migration = File.ReadAllText(args[0]);
                    Execute(connection, migration);
                    Execute(connection, migration);
                    var repository = new SalesRepository(new DatabaseConnection());
                    var admin = new BankPromotionRepository(new DatabaseConnection());
                    var menuItems = new MenuItemRepository(new DatabaseConnection());
                    Assert(menuItems.GetVariantsByCategoryIdAsync(1).GetAwaiter().GetResult().Select(v => v.Id).SequenceEqual(new[] { 101, 102 }), "Category query filters inactive items");
                    var migrated = admin.GetBinsAsync(1).GetAwaiter().GetResult().ToList();
                    Assert(migrated.Count == 2 && migrated.Single(b => b.BINPrefix == "012345").IsActive, "Migration preserves bank ownership and active mapping while consolidating duplicates");
                    Assert(repository.GetBankPromoWithExclusionsAsync("012345").GetAwaiter().GetResult().PromoName == "Legacy", "Checkout resolves migrated bank BIN");
                    Assert(repository.GetBankPromoWithExclusionsAsync("234567").GetAwaiter().GetResult() == null, "Inactive BIN does not apply");
                    foreach (var prefix in new[] { null, "", "12345", "1234567", "12a456", " 12345", "??????" })
                    {
                        try { repository.GetBankPromoWithExclusionsAsync(prefix).GetAwaiter().GetResult(); }
                        catch (ArgumentException) { continue; }
                        throw new Exception("Expected invalid BIN rejection");
                    }
                    var draft = new BankPromotion { ValidFrom = new DateTime(2000, 1, 1), ValidTo = new DateTime(2099, 12, 31), BankId = 2, PromoName = "New offer", DiscountPercentage = 25m };
                    var id = admin.SaveAsync(draft, new[] { 101, 201 }).GetAwaiter().GetResult();
                    Assert(!admin.GetBinsAsync(2).GetAwaiter().GetResult().Any(), "Promotion creation requires no BIN and cannot modify global settings");
                    admin.AddBinAsync(new BankBINMapping { BankId = 2, BINPrefix = "567890", CardType = "  cReDiT  ", CardBrand = "  vIsA  " }).GetAwaiter().GetResult();
                    var editableBin = admin.GetBinsAsync(2).GetAwaiter().GetResult().Single();
                    Assert(editableBin.CardType == "CREDIT" && editableBin.CardBrand == "VISA", "BIN insert and read preserve type and brand");
                    Execute(connection, @"IF EXISTS (SELECT 1 FROM [System].[BankBINMapping] WHERE Id=" + editableBin.Id + @"
                        AND (CardType COLLATE Latin1_General_100_BIN2 <> 'CREDIT' OR CardBrand COLLATE Latin1_General_100_BIN2 <> 'VISA'))
                        THROW 50000, 'Insert must store trimmed uppercase BIN metadata', 1;");
                    var originalId = editableBin.Id;
                    editableBin.BINPrefix = "001234";
                    admin.UpdateBinAsync(editableBin).GetAwaiter().GetResult();
                    var updatedBin = admin.GetBinsAsync(2).GetAwaiter().GetResult().Single();
                    Assert(updatedBin.Id == originalId && updatedBin.BINPrefix == "001234", "BIN number update persists with leading zeros and existing identity");
                    Assert(repository.GetBankPromoWithExclusionsAsync("567890").GetAwaiter().GetResult() == null && repository.GetBankPromoWithExclusionsAsync("001234").GetAwaiter().GetResult().BankPromotionId == id, "Checkout uses the new BIN immediately");
                    editableBin.BINPrefix = "012345";
                    try { admin.UpdateBinAsync(editableBin).GetAwaiter().GetResult(); throw new Exception("Expected duplicate BIN update failure"); }
                    catch (InvalidOperationException) { }
                    Assert(admin.GetBinsAsync(2).GetAwaiter().GetResult().Single().BINPrefix == "001234", "Conflicting update preserves the saved BIN");
                    editableBin.BINPrefix = "001234"; editableBin.BankId = 3;
                    try { admin.UpdateBinAsync(editableBin).GetAwaiter().GetResult(); throw new Exception("Expected cross-bank update failure"); }
                    catch (InvalidOperationException) { }
                    Assert(admin.GetBinsAsync(2).GetAwaiter().GetResult().Single().BankId == 2, "BIN update cannot change a different bank's mapping");
                    editableBin.BankId = 2; editableBin.IsActive = false;
                    admin.UpdateBinAsync(editableBin).GetAwaiter().GetResult();
                    Assert(repository.GetBankPromoWithExclusionsAsync("001234").GetAwaiter().GetResult() == null, "Deactivated BIN does not apply");
                    editableBin.BINPrefix = "567890"; editableBin.IsActive = true;
                    admin.UpdateBinAsync(editableBin).GetAwaiter().GetResult();
                    var saved = repository.GetBankPromoWithExclusionsAsync("567890").GetAwaiter().GetResult();
                    Assert(saved.BankPromotionId == id && saved.ExcludedVariantIds.SequenceEqual(new[] { 101, 201 }), "Aggregate insert and global BIN lookup");
                    var checkout = new BankPromotionCheckoutService(repository);
                    var binsRepository = new BINMappingRepository(new DatabaseConnection());
                    Assert(checkout.GetDiscountPercentageAsync("5678 9012 3456 7890").GetAwaiter().GetResult() == 25m, "Checkout extracts the six-digit prefix from a formatted card number");
                    foreach (var invalidCard in new[] { null, "", "56789", "567890abc", "??????", "56789012345678901234" })
                        Assert(checkout.GetDiscountPercentageAsync(invalidCard).GetAwaiter().GetResult() == 0m, "Incomplete or malformed card input returns zero");
                    draft.Id = id; draft.RequiredCardType = "  cReDiT  ";
                    admin.SaveAsync(draft, new[] { 101, 201 }).GetAwaiter().GetResult();
                    Assert(admin.GetAllAsync().GetAwaiter().GetResult().Single(p => p.Id == id).RequiredCardType == "CREDIT", "Promotion update and read preserve required card type");
                    Assert(checkout.GetDiscountPercentageAsync("567890").GetAwaiter().GetResult() == 25m, "Credit BIN matches Credit offer");
                    editableBin.CardType = " debit "; editableBin.CardBrand = " Mastercard ";
                    binsRepository.UpdateAsync(editableBin).GetAwaiter().GetResult();
                    var metadata = binsRepository.GetByBankIdAsync(2).GetAwaiter().GetResult().Single();
                    Execute(connection, @"IF EXISTS (SELECT 1 FROM [System].[BankBINMapping] WHERE Id=" + editableBin.Id + @"
                        AND (CardType COLLATE Latin1_General_100_BIN2 <> 'DEBIT' OR CardBrand COLLATE Latin1_General_100_BIN2 <> 'MASTERCARD'))
                        THROW 50000, 'Update must store trimmed uppercase BIN metadata', 1;");
                    Execute(connection, @"IF EXISTS (SELECT 1 FROM [Sales].[BankPromotion] WHERE Id=" + id + @"
                        AND RequiredCardType COLLATE Latin1_General_100_BIN2 <> 'CREDIT')
                        THROW 50000, 'Promotion must store trimmed uppercase required card type', 1;");
                    Assert(metadata.CardType == "DEBIT" && metadata.CardBrand == "MASTERCARD", "BIN update normalizes type and persists brand");
                    Assert(checkout.GetDiscountPercentageAsync("567890").GetAwaiter().GetResult() == 0m, "Debit BIN cannot use Credit offer");
                    var debitOffer = new BankPromotion { BankId = 2, PromoName = "Debit offer", DiscountPercentage = 15m, RequiredCardType = "Debit", ValidFrom = draft.ValidFrom, ValidTo = draft.ValidTo };
                    debitOffer.Id = admin.SaveAsync(debitOffer, new int[0]).GetAwaiter().GetResult();
                    Assert(checkout.GetDiscountPercentageAsync("567890").GetAwaiter().GetResult() == 15m, "Card type filtering selects the eligible offer among Credit and Debit promotions");
                    editableBin.CardType = "Credit";
                    binsRepository.UpdateAsync(editableBin).GetAwaiter().GetResult();
                    var creditOffer = checkout.GetApplicablePromotionAsync("567890").GetAwaiter().GetResult();
                    Assert(creditOffer.RequiredCardType == "CREDIT" && creditOffer.CardType == "CREDIT" && creditOffer.CardBrand == "MASTERCARD" && creditOffer.ExcludedVariantIds.SequenceEqual(new[] { 101, 201 }), "Checkout preserves metadata and exact exclusions for the matching offer");
                    editableBin.CardType = null;
                    binsRepository.UpdateAsync(editableBin).GetAwaiter().GetResult();
                    Assert(checkout.GetDiscountPercentageAsync("567890").GetAwaiter().GetResult() == 0m, "Unclassified BIN must not match Credit or Debit offers");
                    debitOffer.IsActive = false;
                    admin.SaveAsync(debitOffer, new int[0]).GetAwaiter().GetResult();
                    draft.RequiredCardType = "All";
                    admin.SaveAsync(draft, new[] { 101, 201 }).GetAwaiter().GetResult();
                    Assert(checkout.GetDiscountPercentageAsync("567890").GetAwaiter().GetResult() == 25m, "All offer accepts unclassified BIN");
                    editableBin.CardType = "Prepaid";
                    try { binsRepository.UpdateAsync(editableBin).GetAwaiter().GetResult(); throw new Exception("Expected invalid card type failure"); }
                    catch (ArgumentException) { }
                    editableBin.CardType = "Debit"; editableBin.CardBrand = new string('x', 31);
                    try { binsRepository.UpdateAsync(editableBin).GetAwaiter().GetResult(); throw new Exception("Expected oversized brand failure"); }
                    catch (ArgumentException) { }
                    draft.RequiredCardType = "Invalid";
                    try { admin.SaveAsync(draft, new int[0]).GetAwaiter().GetResult(); throw new Exception("Expected invalid promotion type failure"); }
                    catch (ArgumentException) { }
                    draft.RequiredCardType = "All";
                    Execute(connection, "UPDATE [Sales].[BankPromotion] SET ValidFrom=DATEADD(minute,10,GETDATE()), ValidTo=DATEADD(minute,20,GETDATE()) WHERE Id=" + id);
                    Assert(repository.GetBankPromoWithExclusionsAsync("567890").GetAwaiter().GetResult() == null, "Future promotion is ineligible even on the same date");
                    Execute(connection, "UPDATE [Sales].[BankPromotion] SET ValidFrom=DATEADD(minute,-20,GETDATE()), ValidTo=DATEADD(minute,-10,GETDATE()) WHERE Id=" + id);
                    Assert(repository.GetBankPromoWithExclusionsAsync("567890").GetAwaiter().GetResult() == null, "Expired promotion is ineligible even on the same date");
                    Execute(connection, "UPDATE [Sales].[BankPromotion] SET ValidFrom=DATEADD(minute,-10,GETDATE()), ValidTo=DATEADD(minute,10,GETDATE()) WHERE Id=" + id);
                    Assert(repository.GetBankPromoWithExclusionsAsync("567890").GetAwaiter().GetResult().BankPromotionId == id, "Current timestamp inside the validity window applies");
                    Execute(connection, "UPDATE [Sales].[BankPromotion] SET IsActive=0 WHERE Id=" + id);
                    Assert(repository.GetBankPromoWithExclusionsAsync("567890").GetAwaiter().GetResult() == null, "Manual disable overrides a currently valid schedule");
                    RejectSql(connection, "UPDATE [Sales].[BankPromotion] SET ValidTo=ValidFrom WHERE Id=" + id);
                    RejectSql(connection, "UPDATE [Sales].[BankPromotion] SET ValidTo=DATEADD(second,-1,ValidFrom) WHERE Id=" + id);
                    Execute(connection, "UPDATE [Sales].[BankPromotion] SET IsActive=1, ValidFrom='20000101', ValidTo='20991231' WHERE Id=" + id);
                    var list = admin.GetAllAsync().GetAwaiter().GetResult().Single(p => p.Id == id);
                    Assert(list.ValidFrom == draft.ValidFrom && list.ValidTo == draft.ValidTo, "Validity timestamps round-trip through repository reads");
                    Assert(list.BankName == "Second Bank" && list.ExcludedVariants.Select(v => v.Name).SequenceEqual(new[] { "Tea - Large", "Rice" }), "Edit data includes exact variants and display names across categories");
                    var invalidSchedule = new BankPromotion { BankId = 2, PromoName = "Invalid schedule", DiscountPercentage = 10m, ValidFrom = new DateTime(2026, 1, 1), ValidTo = new DateTime(2026, 1, 1) };
                    try { admin.SaveAsync(invalidSchedule, new int[0]).GetAwaiter().GetResult(); throw new Exception("Expected invalid interval rejection"); }
                    catch (ArgumentException) { }
                    draft.Id = id; draft.DiscountPercentage = 30m;
                    admin.SaveAsync(draft, new int[0]).GetAwaiter().GetResult();
                    saved = repository.GetBankPromoWithExclusionsAsync("567890").GetAwaiter().GetResult();
                    Assert(saved.BankPromotionId == id && saved.DiscountPercentage == 30m && saved.ExcludedVariantIds.Count == 0, "Update replaces all exclusions without inserting another promotion");
                    Assert(admin.GetBinsAsync(2).GetAwaiter().GetResult().Single().BINPrefix == "567890", "Promotion update leaves global BIN intact");
                    admin.SaveAsync(draft, new[] { 102 }).GetAwaiter().GetResult();
                    draft.DiscountPercentage = 40m;
                    try { admin.SaveAsync(draft, new[] { 999 }).GetAwaiter().GetResult(); throw new Exception("Expected missing variant failure"); }
                    catch (SqlException) { }
                    var rolledBack = repository.GetBankPromoWithExclusionsAsync("567890").GetAwaiter().GetResult();
                    Assert(rolledBack.DiscountPercentage == 30m && rolledBack.ExcludedVariantIds.SequenceEqual(new[] { 102 }), "Invalid exclusion rolls back parent and deleted children");
                    var count = admin.GetAllAsync().GetAwaiter().GetResult().Count();
                    try { admin.SaveAsync(new BankPromotion { ValidFrom = new DateTime(2000, 1, 1), ValidTo = new DateTime(2099, 12, 31), BankId = 3, PromoName = "Invalid insert", DiscountPercentage = 10m }, new[] { 999 }).GetAwaiter().GetResult(); throw new Exception("Expected invalid insert failure"); }
                    catch (SqlException) { }
                    Assert(admin.GetAllAsync().GetAwaiter().GetResult().Count() == count, "Invalid exclusions roll back new promotion insert");
                    var competingId = admin.SaveAsync(new BankPromotion { ValidFrom = new DateTime(2000, 1, 1), ValidTo = new DateTime(2099, 12, 31), BankId = 2, PromoName = "Competing", DiscountPercentage = 10m }, new int[0]).GetAwaiter().GetResult();
                    try { repository.GetBankPromoWithExclusionsAsync("567890").GetAwaiter().GetResult(); throw new Exception("Expected ambiguous promotion failure"); }
                    catch (InvalidOperationException) { }
                    admin.SaveAsync(new BankPromotion { ValidFrom = new DateTime(2000, 1, 1), ValidTo = new DateTime(2099, 12, 31), Id = competingId, BankId = 2, PromoName = "Competing", DiscountPercentage = 10m, IsActive = false }, new int[0]).GetAwaiter().GetResult();
                    try { admin.AddBinAsync(new BankBINMapping { BankId = 3, BINPrefix = "567890" }).GetAwaiter().GetResult(); throw new Exception("Expected conflicting BIN failure"); }
                    catch (InvalidOperationException) { }
                    Assert(!admin.GetBinsAsync(3).GetAwaiter().GetResult().Any(), "Conflicting BIN must not be saved");
                    draft.IsActive = false;
                    admin.SaveAsync(draft, new int[0]).GetAwaiter().GetResult();
                    Assert(repository.GetBankPromoWithExclusionsAsync("567890").GetAwaiter().GetResult() == null, "Inactive promotion does not apply");
                    draft.Id = 999;
                    try { admin.SaveAsync(draft, new int[0]).GetAwaiter().GetResult(); throw new Exception("Expected missing promotion failure"); }
                    catch (InvalidOperationException) { }
                    // Exercise the fresh-install branch as well as upgrading an existing table.
                    Execute(connection, "DROP TABLE [System].[BankBINMapping];");
                    Execute(connection, migration);
                    admin.AddBinAsync(new BankBINMapping { BankId = 2, BINPrefix = "567890" }).GetAwaiter().GetResult();
                    var deletablePromotion = new BankPromotion { BankId = 3, PromoName = "Delete test", DiscountPercentage = 12m, ValidFrom = new DateTime(2000, 1, 1), ValidTo = new DateTime(2099, 12, 31) };
                    var retainedPromotionId = admin.SaveAsync(deletablePromotion, new int[0]).GetAwaiter().GetResult();
                    var deleteId = binsRepository.InsertAsync(new BankBINMapping { BankId = 3, BINPrefix = "456789", CardType = "Credit", CardBrand = "Visa" }).GetAwaiter().GetResult();
                    Assert(checkout.GetDiscountPercentageAsync("456789").GetAwaiter().GetResult() == 12m, "BIN is usable before deletion");
                    try { admin.DeleteBinAsync(deleteId, 2).GetAwaiter().GetResult(); throw new Exception("Expected wrong-bank deletion failure"); }
                    catch (InvalidOperationException) { }
                    Assert(binsRepository.GetByBankIdAsync(3).GetAwaiter().GetResult().Count() == 1, "Wrong-bank deletion cannot remove another bank's BIN");
                    admin.DeleteBinAsync(deleteId, 3).GetAwaiter().GetResult();
                    Assert(!binsRepository.GetByBankIdAsync(3).GetAwaiter().GetResult().Any() && checkout.GetDiscountPercentageAsync("456789").GetAwaiter().GetResult() == 0m, "Deleted BIN is removed from database and checkout eligibility");
                    Assert(admin.GetAllAsync().GetAwaiter().GetResult().Any(p => p.Id == retainedPromotionId), "Deleting a global BIN does not delete promotions");
                    try { binsRepository.DeleteAsync(deleteId, 3).GetAwaiter().GetResult(); throw new Exception("Expected missing-BIN deletion failure"); }
                    catch (InvalidOperationException) { }
                    var inactiveDeleteId = binsRepository.InsertAsync(new BankBINMapping { BankId = 3, BINPrefix = "456789", IsActive = false }).GetAwaiter().GetResult();
                    binsRepository.DeleteAsync(inactiveDeleteId, 3).GetAwaiter().GetResult();
                    Assert(!binsRepository.GetByBankIdAsync(3).GetAwaiter().GetResult().Any(), "Inactive BINs can be deleted from database");
                    RejectSql(connection, "INSERT INTO [System].[BankBINMapping](BankId,BINPrefix) VALUES (2,'12345');");
                    RejectSql(connection, "INSERT INTO [Sales].[BankPromoExcludedVariant] VALUES (1,999);");

                }
                Console.WriteLine("Bank promotion schema, constraints, input validation and repository integration passed.");
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
            finally
            {
                SqlConnection.ClearAllPools();
                Execute(master, "ALTER DATABASE [" + database + "] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [" + database + "];");
            }
        }
    }
}
