using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Inventory;
using PointOfSale.Core.Interfaces.Services;
using PointOfSale.Core.Models.Inventory;
using PointOfSale.Core.Services;
using PointOfSale.UI.ViewModels.Inventory;
using PointOfSale.UI.Views.Inventory;

class StockAdjustmentLoadingChecks
{
    class Stub<T> : RealProxy
    {
        readonly Func<IMethodCallMessage, object> call;
        public Stub(Func<IMethodCallMessage, object> call) : base(typeof(T)) { this.call = call; }
        public T Value => (T)GetTransparentProxy();
        public override IMessage Invoke(IMessage message)
        {
            var request = (IMethodCallMessage)message;
            try { return new ReturnMessage(call(request), null, 0, request.LogicalCallContext, request); }
            catch (Exception ex) { return new ReturnMessage(ex, request); }
        }
    }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS: " + message); }
    static void Pump() { var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false)); Dispatcher.PushFrame(frame); }
    static void Enter(UIElement element) { element.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(element), 0, Key.Enter) { RoutedEvent = Keyboard.PreviewKeyDownEvent }); Pump(); }
    [STAThread] static void Main()
    {
        var app = new Application();
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        foreach (var resource in new[] { "Converters", "Themes/Colors", "Themes/Icons", "Components/Text", "Components/Buttons", "Components/Inputs", "Components/Containers", "Components/DataGrids", "Styles/MainViewStyle", "Styles/SalesStyles", "Styles/ViewStyles" })
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/PointOfSale.UI;component/Resources/" + resource + ".xaml", UriKind.Relative) });
        var locationRequest = new TaskCompletionSource<IEnumerable<Location>>();
        var productRequest = new TaskCompletionSource<IEnumerable<Product>>();
        var historyRequest = new TaskCompletionSource<List<StockAdjustmentHeaderDto>>();
        var saveRequest = new TaskCompletionSource<long>();
        var locationCalls = 0; var productCalls = 0; var historyCalls = 0;
        var product = new Product { ProductId = 1, ProductName = "ONIONS", IsActive = true };
        var inventory = new Stub<IInventoryRepository>(m => { locationCalls++; return locationRequest.Task; }).Value;
        var products = new Stub<IProductRepository>(m => { productCalls++; return productRequest.Task; }).Value;
        var stock = new Stub<IStockAdjustmentRepository>(m => {
            if (m.MethodName == "GetHistoryAsync") { historyCalls++; return historyRequest.Task; }
            if (m.MethodName == "CreateAsync") return saveRequest.Task;
            if (m.MethodName == "GetAvailableQty") return Task.FromResult<IEnumerable<AvailableQuantityDto>>(new AvailableQuantityDto[0]);
            throw new Exception("Unexpected repository call: " + m.MethodName);
        }).Value;
        var session = new Stub<IUserSessionService>(m => 1).Value;
        var dialogs = new Stub<IDialogService>(m => null).Value;
        var vm = new StockAdjustmentViewModel(products, dialogs, stock, inventory, session);
        Check(locationCalls == 1 && productCalls == 1, "Locations and products start together without waiting for each other");
        Check(vm.IsBusy && vm.IsLoading && !vm.IsOverlayVisible, "Navigation loads data without showing the shell's dark overlay");
        var view = new StockAdjustmentView { DataContext = vm };
        var window = new Window { Content = view, Width = 1400, Height = 800, ShowInTaskbar = false };
        window.Show(); Pump();
        var indicator = (TextBlock)view.FindName("DataLoadingIndicator");
        Check(indicator.Visibility == Visibility.Visible, "The actual view displays a local loading indicator");
        var responsive = false;
        Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => responsive = true)); Pump();
        Check(responsive, "The UI dispatcher remains responsive during delayed data requests");
        locationRequest.SetResult(new[] { new Location { Id = 1, Name = "MAIN_STORE" } }); Pump();
        Check(vm.IsBusy && historyCalls == 0 && !vm.IsOverlayVisible, "A partially loaded screen stays guarded without darkening");
        productRequest.SetResult(new[] { product }); Pump();
        Check(historyCalls == 1 && vm.Products.Count == 1 && vm.Locations.Count == 1 && vm.SelectedLocation.Id == 1,
            "Dependency completion populates the view and starts history once");
        Check(vm.IsBusy && vm.IsLoading && !vm.IsOverlayVisible && !vm.RefreshCommand.CanExecute(null),
            "Initial history loading preserves the busy state and prevents duplicate refreshes");
        historyRequest.SetResult(new List<StockAdjustmentHeaderDto>()); Pump();
        Check(!vm.IsBusy && !vm.IsLoading && !vm.IsOverlayVisible && indicator.Visibility == Visibility.Collapsed,
            "Loading completes without toggling the dark overlay");
        historyRequest = new TaskCompletionSource<List<StockAdjustmentHeaderDto>>();
        vm.SearchCommand.Execute(null); Pump();
        Check(vm.IsBusy && vm.IsLoading && !vm.IsOverlayVisible, "History searches use local loading rather than the dark overlay");
        historyRequest.SetResult(new List<StockAdjustmentHeaderDto>()); Pump();
        Check(!vm.IsBusy && indicator.Visibility == Visibility.Collapsed, "History search restores the ready state");
        vm.RefreshCommand.Execute(null); Pump();
        Check(locationCalls == 2 && productCalls == 2 && !vm.IsBusy && !vm.IsOverlayVisible,
            "Refreshing reloads both dependencies and leaves the screen ready");
        vm.SelectedProduct = product; vm.AdjustmentQuantity = "1"; vm.LineReason = "Stock count";
        vm.AddLineCommand.Execute(null);
        Check(vm.SaveCommand.CanExecute(null), "A valid adjustment can still be saved");
        vm.SaveCommand.Execute(null); Pump();
        Check(vm.IsBusy && vm.IsOverlayVisible && !vm.IsLoading, "Saving retains the protective shell overlay");
        // The fake save remains pending so no report dialog or database write is performed.
        window.Close();
    }
}
