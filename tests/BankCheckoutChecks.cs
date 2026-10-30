using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Windows.Threading;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Sales;
using PointOfSale.Core.Interfaces.Services;
using PointOfSale.Core.Models.Sales;
using PointOfSale.Core.Models.System;
using PointOfSale.Core.Services;
using PointOfSale.UI.Services;
using PointOfSale.UI.ViewModels.Sales;

internal static class BankCheckoutChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    private static bool Can(SalesViewModel vm, string method) => (bool)typeof(SalesViewModel)
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(vm, null);
    private static BankPromoWithExclusionsDto Offer(int id = 1, decimal rate = 10)
    {
        var offer = new BankPromoWithExclusionsDto { BankPromotionId = id, BankId = id, PromoName = "Test bank offer", DiscountPercentage = rate };
        offer.ExcludedVariantIds.Add(102);
        return offer;
    }
    private static SalesViewModel ViewModel(FakeSales sales)
    {
        return new SalesViewModel(sales, null, null, null, null, null, null, null, null, null,
            null, null, null, null, new Reports(), null,
            (CloudStorageService)FormatterServices.GetUninitializedObject(typeof(CloudStorageService)));
    }
    private static void Wait(Task task)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
        var deadline = DateTime.UtcNow.AddSeconds(5);
        timer.Tick += (s, e) => { if (task.IsCompleted || DateTime.UtcNow > deadline) frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
        timer.Stop();
        Check(task.IsCompleted, "Async promotion lookup timed out");
        task.GetAwaiter().GetResult();
    }
    [STAThread]
    private static int Main()
    {
        try { Run(); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void Run()
    {
        var sales = new FakeSales();
        var service = new CheckoutService(sales);
        var cart = new List<SalesLine>
        {
            new SalesLine { ProductId = 101, UnitPrice = 100, Quantity = 1 },
            new SalesLine { ProductId = 102, UnitPrice = 200, Quantity = 1 },
            new SalesLine { ProductId = 103, UnitPrice = 100, Quantity = 1, ManualDiscount = 20 },
            new SalesLine { ProductId = 104, UnitPrice = 50, Quantity = 1, BaseDiscountPerUnit = 5 },
            new SalesLine { ProductId = 105, UnitPrice = 50, Quantity = 1 }
        };
        cart[4].SetPromoDiscount(5);
        var result = service.ApplyBankPromotionAsync("012345", cart).GetAwaiter().GetResult();
        Check(result.EligibleSubtotal == 100 && result.DiscountAmount == 10 && result.PromoType == "BANK" && result.PromoBankId == 1,
            "Bank eligibility must exclude base/manual/promo discounts and excluded variants");
        Check(cart[2].ManualDiscount == 20 && cart[3].BaseDiscountPerUnit == 5 && cart[4].PromoDiscount == 5,
            "Applying a bank promotion must preserve existing line discounts");
        foreach (var bin in new[] { null, "", "12345", "1234567", "12a456", "１２３４５６" })
        {
            try { service.ApplyBankPromotionAsync(bin, cart).GetAwaiter().GetResult(); throw new Exception("Invalid BIN accepted"); }
            catch (ArgumentException) { }
        }
        Check(CheckoutService.CalculateBankPromotion(Offer(1, 5), new[] { new SalesLine { UnitPrice = 0.10m, Quantity = 1 } }).DiscountAmount == 0.01m,
            "Half-cent discounts must round away from zero, matching SQL ROUND");

        var vm = ViewModel(sales);
        foreach (var line in cart.Take(3)) vm.CartItems.Add(line);
        vm.SelectedPaymentMethod = "CARD";
        sales.Calls = 0;
        vm.ReferenceNumber = "01234";
        Check(sales.Calls == 0, "Five reference digits must not start evaluation");
        vm.ReferenceNumber = "012345";
        Wait(vm.BankPromotionEvaluation);
        Check(sales.Calls == 1 && vm.PromoType == "BANK" && vm.PromoBankId == 1 && vm.BankDiscountAmount == 10,
            "Exactly six digits must apply the promotion to the invoice: " + vm.BankPromotionMessage);
        Check(vm.SubTotal == 380 && vm.NetAmount == 370, "Invoice must subtract bank savings once");
        vm.LineDiscountPercent = 10;
        Check(vm.LineDiscountPercent == 0 && vm.BillDiscount == 10, "Cash discount must not stack on bank discount");
        vm.IsPaymentInputVisible = true;
        vm.SelectedPaymentMethod = "CASH";
        vm.CurrentPaymentAmount = vm.NetAmount;
        Check(!vm.HasBankPromotion && vm.NetAmount == 380, "Switching away from card clears its reference and restores the normal total");
        vm.PaymentTerminals.Add(new PaymentTerminal { Id = 1, TerminalName = "Test terminal" });
        vm.SelectedPaymentMethod = "CARD";
        vm.ReferenceNumber = "012345";
        Wait(vm.BankPromotionEvaluation);
        vm.CurrentPaymentAmount = vm.NetAmount - 1;
        Check(!Can(vm, "CanAddPayment"), "Partial card payments must be rejected");
        vm.CurrentPaymentAmount = vm.NetAmount;
        Check(Can(vm, "CanAddPayment"), "Full card payment must be accepted");
        vm.AddPaymentCommand.Execute(null);
        Check(vm.AppliedPayments.Count == 1 && Can(vm, "CanSaveSale"), "One full card payment enables saving");
        Check(vm.ReferenceNumber == string.Empty && vm.HasBankPromotion && vm.AppliedPayments[0].ReferenceNumber == "012345",
            "Clearing entry after ADD must preserve the bank promotion and exact six-digit payment reference");
        Wait((Task)typeof(SalesViewModel).GetMethod("RevalidateBankPromotionForSaveAsync", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(vm, null));
        vm.IsPaymentInputVisible = true;
        vm.CurrentPaymentAmount = 1;
        Check(!Can(vm, "CanAddPayment"), "A second payment line must be blocked");
        cart[0].Quantity = 2;
        Check(vm.BankDiscountAmount == 20 && !Can(vm, "CanSaveSale"), "Changing quantity recalculates eligibility and invalidates stale payment totals");
        vm.ReferenceNumber = "01234";
        Check(!vm.HasBankPromotion && vm.AppliedPayments.Count == 0 && vm.PromoBankId == null && vm.BillDiscount == 0 && vm.NetAmount == 480,
            "Shortening the reference must clear the old bank/payment and restore the normal bill total");
        vm.LineDiscountPercent = 5;
        vm.ReferenceNumber = "012345";
        Wait(vm.BankPromotionEvaluation);
        Check(!vm.HasBankPromotion && !Can(vm, "CanAddPayment"), "Bank evaluation with a cash discount must fail and block payment until resolved");
        vm.LineDiscountPercent = 0;
        vm.ReferenceNumber = "";
        vm.ReferenceNumber = "012345";
        Wait(vm.BankPromotionEvaluation);
        Check(vm.HasBankPromotion, "Evaluation can be retried after removing the conflicting discount");
        vm.SelectedCustomer = new Customer { Id = 0, CustomerName = "CBS FINANCE - MAHARAGAMA" };
        vm.CancelInvoiceCommand.Execute(null);
        Check(vm.SelectedCustomer == null && vm.CustomerSearchText == string.Empty && vm.ReferenceNumber == string.Empty &&
            vm.PromoBankId == null && !vm.HasBankPromotion && vm.CartItems.Count == 0, "Reset clears customer and bank promotion state");

        var held = new RecalledSaleDto { SalesId = 42, InvoiceNumber = "HELD-42", TotalAmount = 100, Discount = 10,
            PromoType = "BANK", PromoBankId = 1, BankPromotionId = 1, BankDiscountAmount = 10 };
        held.Lines.Add(new RecalledSaleLineDto { ProductId = 101, ProductName = "Tea", Quantity = 1, UnitPrice = 100 });
        typeof(SalesViewModel).GetMethod("LoadRecalledSaleIntoCart", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(vm, new object[] { held });
        Wait((Task)typeof(SalesViewModel).GetMethod("RestoreBankPromotionAsync", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(vm, new object[] { held }));
        Check(vm.HasBankPromotion && vm.LineDiscountPercent == 0 && vm.BillDiscount == 10 && vm.NetAmount == 90,
            "Recall must restore bank metadata instead of converting bank savings into a cash discount");
        sales.Current = null;
        typeof(SalesViewModel).GetMethod("LoadRecalledSaleIntoCart", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(vm, new object[] { held });
        Wait((Task)typeof(SalesViewModel).GetMethod("RestoreBankPromotionAsync", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(vm, new object[] { held }));
        Check(vm.HasBankPromotion && !Can(vm, "CanSaveSale") && !Can(vm, "CanAddPayment"),
            "An expired held promotion must retain restrictions and block checkout until cleared");
        vm.ReferenceNumber = "01234";
        Check(!vm.HasBankPromotion && vm.BillDiscount == 0 && vm.LineDiscountPercent == 0,
            "Clearing an expired held bank promotion must not leave a cash discount behind");

        var raceSales = new FakeSales { Pending = new Dictionary<string, TaskCompletionSource<BankPromoWithExclusionsDto>>() };
        raceSales.Pending.Add("012345", new TaskCompletionSource<BankPromoWithExclusionsDto>());
        raceSales.Pending.Add("234567", new TaskCompletionSource<BankPromoWithExclusionsDto>());
        var race = ViewModel(raceSales);
        race.CartItems.Add(new SalesLine { ProductId = 101, Quantity = 1, UnitPrice = 100 });
        race.SelectedPaymentMethod = "CARD";
        race.ReferenceNumber = "012345";
        var older = race.BankPromotionEvaluation;
        Check(race.IsBankPromotionEvaluating && !Can(race, "CanSaveSale"), "Pending lookup must block checkout");
        race.ReferenceNumber = "234567";
        raceSales.Pending["234567"].SetResult(Offer(2, 20));
        Wait(race.BankPromotionEvaluation);
        raceSales.Pending["012345"].SetResult(Offer(1, 10));
        Wait(older);
        Check(race.PromoBankId == 2 && race.BankDiscountAmount == 20, "A stale BIN response must never overwrite the latest selection");
        Console.WriteLine("Checkout service and WPF ViewModel: eligibility, rounding, mutual exclusion, payment restrictions, reset and async race checks passed.");
    }
    private sealed class Reports : IReportService
    {
        public void PrintSalesInvoice(long id) { }
        public void PrintSettlementReceipt(long id) { }
        public void PrintSalesReport(string type, int user, int? branch, DateTime from, DateTime to, string path) { }
    }
    private sealed class FakeSales : ISalesRepository
    {
        public int Calls;
        public BankPromoWithExclusionsDto Current = Offer();
        public Dictionary<string, TaskCompletionSource<BankPromoWithExclusionsDto>> Pending;
        public Task<BankPromoWithExclusionsDto> GetBankPromoWithExclusionsAsync(string bin)
        {
            Calls++;
            return Pending != null ? Pending[bin].Task : Task.FromResult(Current);
        }
        public Task<long> CreateAsync(Sale sale) => throw new NotImplementedException();
        public Task<int> HoldSaleAsync(HoldSaleRequestDto sale) => throw new NotImplementedException();
        public Task<bool> FinalizeSaleAsync(FinalizeSaleRequestDto sale) => throw new NotImplementedException();
        public Task<List<SalesListDto>> GetUnpaidSalesAsync(int branch) => throw new NotImplementedException();
        public Task<RecalledSaleDto> GetSaleForRecallAsync(long id) => throw new NotImplementedException();
        public DataTable GetInvoiceData(long id) => throw new NotImplementedException();
        public DataTable GetSettlementReceiptData(long id) => throw new NotImplementedException();
        public Task<List<SalesListDto>> GetSalesListAsync(DateTime from, DateTime to, int? branch, string type) => throw new NotImplementedException();
        public Task<List<SalesLineItemDto>> GetSalesLineItemsAsync(long id) => throw new NotImplementedException();
    }
}
