using System;
using System.Configuration;
using System.Data.SqlClient;
using PointOfSale.Core.DTOs;
using PointOfSale.Infrastructure;
using PointOfSale.Infrastructure.Repositories.Sales;

internal static class BankCheckoutPersistenceChecks
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static int Main(string[] args)
    {
        try { Run(args[0]); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void Run(string connectionString)
    {
        var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
        config.AppSettings.Settings.Add("ActiveConnection", "CheckoutTest");
        config.ConnectionStrings.ConnectionStrings.Add(new ConnectionStringSettings("CheckoutTest", connectionString));
        config.Save(ConfigurationSaveMode.Modified);
        ConfigurationManager.RefreshSection("appSettings");
        ConfigurationManager.RefreshSection("connectionStrings");
        var repository = new SalesRepository(new DatabaseConnection());
        var request = new HoldSaleRequestDto
        {
            BranchId = 1, CreatedBy = 1, TotalAmount = 100, Discount = 10,
            PromoType = "BANK", PromoBankId = 1, BankPromotionId = 1, BankDiscountAmount = 10
        };
        request.Lines.Add(new SaleLineRequestDto { ProductId = 101, Quantity = 1, UnitPrice = 100 });
        var id = repository.HoldSaleAsync(request).GetAwaiter().GetResult();
        var recalled = repository.GetSaleForRecallAsync(id).GetAwaiter().GetResult();
        Check(recalled.PromoType == "BANK" && recalled.PromoBankId == 1 && recalled.BankPromotionId == 1 &&
            recalled.BankDiscountAmount == 10 && recalled.Lines.Count == 1,
            "Hold/recall must preserve bank restrictions and savings");

        using (var connection = new SqlConnection(connectionString))
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "ALTER TABLE Sales.Sales ADD CONSTRAINT TestRejectBank CHECK(PromoBankId<>999999);";
                command.ExecuteNonQuery();
                request.SalesId = id;
                request.PromoBankId = 999999;
                request.TotalAmount = 500;
                try { repository.HoldSaleAsync(request).GetAwaiter().GetResult(); throw new Exception("Expected metadata rejection"); }
                catch (SqlException ex) when (ex.Number == 547) { }
                recalled = repository.GetSaleForRecallAsync(id).GetAwaiter().GetResult();
                Check(recalled.TotalAmount == 100, "Metadata failure must roll back the held header and lines");
                request.PromoType = null;
                request.PromoBankId = null;
                request.BankPromotionId = null;
                request.BankDiscountAmount = 0;
                request.Discount = 0;
                repository.HoldSaleAsync(request).GetAwaiter().GetResult();
                recalled = repository.GetSaleForRecallAsync(id).GetAwaiter().GetResult();
                Check(recalled.PromoType == null && recalled.PromoBankId == null && recalled.BankDiscountAmount == 0,
                    "Removing a bank promotion must clear its saved metadata");
            }
        }
        request.SalesId = null;
        request.TotalAmount = 100;
        request.Discount = 10;
        request.PromoType = "BANK";
        request.PromoBankId = 1;
        request.BankPromotionId = 1;
        request.BankDiscountAmount = 10;
        id = repository.HoldSaleAsync(request).GetAwaiter().GetResult();
        repository.FinalizeSaleAsync(new FinalizeSaleRequestDto
        {
            SalesId = id, CreatedBy = 1,
            Payments = new System.Collections.Generic.List<SalePaymentRequestDto>
            {
                new SalePaymentRequestDto { PaymentMethod = "CARD", PaymentTerminalId = 1, Amount = 90, ReferenceNumber = "012345" }
            }
        }).GetAwaiter().GetResult();
        using (var connection = new SqlConnection(connectionString))
        using (var command = connection.CreateCommand())
        {
            connection.Open();
            command.CommandText = "SELECT ReferenceNumber FROM Sales.Payment WHERE SalesId=@Id";
            command.Parameters.AddWithValue("@Id", id);
            Check((string)command.ExecuteScalar() == "012345", "The repository must persist all six reference digits, including the leading zero, via the existing payment TVP");
        }
        Console.WriteLine("Repository checks: hold/recall, atomic rollback, promotion removal and six-digit payment reference database round trip passed.");
    }
}
