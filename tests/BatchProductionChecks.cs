using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
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

class BatchProductionChecks
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
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        var product = new Product { ProductId = 1, ProductName = "ONION SAMBOL", IsActive = true, UnitMeasureId = 1 };
        var processCalls = 0;
        var confirmationCalls = 0;
        var confirm = true;
        var failProcessing = false;
        var serviceResult = true;
        string confirmationText = null;
        BatchProductionRequest request = null;
        var service = new Stub<IBatchProductionService>(m => {
            if (m.MethodName == "ProcessBatchAsync")
            {
                Check(confirmationCalls > 0 && confirm, "Confirmation is accepted before calling the production service");
                processCalls++; request = (BatchProductionRequest)m.Args[0];
                if (failProcessing) throw new InvalidOperationException("Test failure");
                return Task.FromResult(serviceResult);
            }
            return Task.FromResult<IList<BatchProductionHistoryDto>>(new List<BatchProductionHistoryDto>());
        }).Value;
        var inventory = new Stub<IInventoryRepository>(m => Task.FromResult<IEnumerable<Location>>(new[] { new Location { Id = 1, Name = "KITCHEN" } })).Value;
        var products = new Stub<IProductRepository>(m => Task.FromResult<IEnumerable<Product>>(new[] { product })).Value;
        var stock = new Stub<IStockAdjustmentRepository>(m => Task.FromResult<IEnumerable<AvailableQuantityDto>>(new AvailableQuantityDto[0])).Value;
        var session = new Stub<IUserSessionService>(m => 1).Value;
        var dialogs = new Stub<IDialogService>(m => {
            if (m.MethodName == "ShowConfirmation")
            {
                confirmationCalls++;
                confirmationText = (string)m.Args[0];
                return confirm;
            }
            return null;
        }).Value;
        var vm = new BatchProductionViewModel(service, inventory, products, stock, session, dialogs);
        var app = new Application();
        foreach (var resource in new[] { "Converters", "Themes/Colors", "Themes/Icons", "Components/Text", "Components/Buttons", "Components/Inputs", "Components/Containers", "Components/DataGrids" })
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/PointOfSale.UI;component/Resources/" + resource + ".xaml", UriKind.Relative) });
        var view = new BatchProductionView { DataContext = vm };
        var window = new Window { Content = view, Width = 1200, Height = 700, ShowInTaskbar = false };
        window.Show(); window.Activate(); Pump();
        var location = (ComboBox)view.FindName("LocationComboBox");
        var item = (ComboBox)view.FindName("ProductComboBox");
        var qty = (TextBox)view.FindName("YieldQuantityTextBox");
        var button = (Button)view.FindName("ProcessBatchButton");
        location.Focus(); location.IsDropDownOpen = true; Enter(location);
        Check(item.IsKeyboardFocusWithin && !location.IsDropDownOpen, "Location Enter commits selection and focuses Item");
        vm.SelectedProduct = product; Pump();
        item.Focus(); item.IsDropDownOpen = true; Enter(item);
        Check(qty.IsKeyboardFocusWithin && !item.IsDropDownOpen, "Item Enter commits selection and focuses Yield Quantity");
        foreach (var invalid in new[] { "", "abc", "0", "-2", "1.2345", "1,2", "1e3", "NaN", "1000000000000000", "999999999999999999999999999999999999999999999" })
        {
            qty.Focus(); qty.Text = invalid; Enter(qty);
            Check(qty.IsKeyboardFocusWithin && Validation.GetHasError(qty) && !button.IsEnabled && !string.IsNullOrEmpty(vm.ProducedQuantityError),
                "Invalid quantity stays focused, shows an error and disables processing: " + invalid);
        }
        qty.Text = "1.250"; Enter(qty);
        Check(button.IsKeyboardFocused && !Validation.GetHasError(qty) && button.IsEnabled && processCalls == 0,
            "Valid decimal Enter focuses Process Batch without executing it");
        confirm = false;
        vm.ProcessBatchCommand.Execute(null); Pump();
        Check(processCalls == 0 && vm.SelectedProduct == product && vm.ProducedQuantity == "1.250" && !vm.IsBusy,
            "Declining confirmation keeps the form and does not save");
        Check(confirmationText.Contains("ONION SAMBOL") && confirmationText.Contains("KITCHEN") && confirmationText.Contains("1.250"),
            "Confirmation displays the product, location and yield quantity");
        confirm = true;
        failProcessing = true;
        vm.ProcessBatchCommand.Execute(null); Pump();
        Check(processCalls == 1 && vm.SelectedProduct == product && vm.ProducedQuantity == "1.250" && !vm.IsBusy,
            "Failed processing retains the form for retry");
        failProcessing = false;
        serviceResult = false;
        vm.ProcessBatchCommand.Execute(null); Pump();
        Check(processCalls == 2 && vm.SelectedProduct == product && vm.ProducedQuantity == "1.250",
            "An unsuccessful service result does not clear the form");
        serviceResult = true;
        vm.ProcessBatchCommand.Execute(null); Pump();
        Check(processCalls == 3 && request.ProducedQty == 1.250m && request.LocationId == 1 && request.OutputProductId == 1,
            "Validated quantity and selected IDs reach the production service");
        Check(vm.SelectedProduct == null && vm.SelectedProductId == 0 && vm.ProducedQuantity == null &&
              vm.SelectedLocationId == 1 && vm.TargetUomName == string.Empty && vm.SelectedProductAvailableQuantity == 0,
            "Successful processing resets production inputs to their defaults");
        Check(item.SelectedItem == null && item.Text == string.Empty && qty.Text == string.Empty &&
              !Validation.GetHasError(item) && !Validation.GetHasError(qty) && string.IsNullOrEmpty(vm.ProducedQuantityError) &&
              !button.IsEnabled && location.IsKeyboardFocusWithin,
            "Reset clears visible inputs and errors, disables processing and focuses Location");
        vm.SelectedProduct = product;
        vm.ProducedQuantity = "999999999999999.999";
        Check(vm.ProcessBatchCommand.CanExecute(null), "SQL decimal(18,3) maximum is accepted");
        vm.ProducedQuantity = "2.5"; vm.SelectedLocationId = 999;
        Check(!vm.ProcessBatchCommand.CanExecute(null), "A location outside the loaded list cannot be processed");
        vm.SelectedLocationId = 1;
        item.Focus(); item.Text = "does not exist"; Pump();
        Check(!button.IsEnabled, "Typing unknown item text immediately disables processing");
        Enter(item);
        Check(item.IsKeyboardFocusWithin && Validation.GetHasError(item) && !button.IsEnabled,
            "Unknown item text cannot advance or process a batch");
        vm.SelectedProduct = product;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        vm.ProducedQuantity = "1,125";
        Check(vm.ProcessBatchCommand.CanExecute(null), "Current-culture decimal separator is accepted");
        vm.ProducedQuantity = "1.125";
        Check(!vm.ProcessBatchCommand.CanExecute(null), "A grouping separator cannot be mistaken for a decimal");
        window.Close();
    }
}
