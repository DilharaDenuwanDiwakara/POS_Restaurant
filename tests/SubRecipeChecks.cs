using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Threading;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Inventory;
using PointOfSale.Core.Interfaces.Services;
using PointOfSale.Core.Models.Inventory;
using PointOfSale.Core.Services;
using PointOfSale.UI.ViewModels.Inventory;
using PointOfSale.UI.Views.Inventory;

class SubRecipeChecks
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
    static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
    static Button RemoveButton(SubRecipeView view, SubRecipeLineDto line)
    {
        view.UpdateLayout(); Pump();
        var button = Descendants<Button>(view).Single(b => ReferenceEquals(b.DataContext, line));
        Check(button.Command != null && ReferenceEquals(button.CommandParameter, line), "Remove button binds to the command and its own row");
        Check(button.IsEnabled, "Actual remove button is enabled");
        return button;
    }
    static void Click(Button button)
    {
        var peer = new ButtonAutomationPeer(button);
        ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
        Pump();
    }
    [STAThread] static void Main()
    {
        var prep = new Product { ProductId = 1, ProductName = "ONION SAMBOL", IsActive = true, UnitMeasureId = 1 };
        var ingredient = new Product { ProductId = 2, ProductName = "RED ONIONS", IsActive = true, UnitMeasureId = 1, StandardCost = 100 };
        var tomato = new Product { ProductId = 3, ProductName = "TOMATO", IsActive = true, UnitMeasureId = 1, StandardCost = 50 };
        IList<SubRecipeLineDto> saved = null;
        var products = new Stub<IProductRepository>(m => Task.FromResult<IEnumerable<Product>>(new[] { prep, ingredient, tomato })).Value;
        var units = new Stub<IUnitMeasureRepository>(m => Task.FromResult<IEnumerable<UnitMeasure>>(new[] { new UnitMeasure { UnitMeasureId = 1, UnitMeasureName = "KG" } })).Value;
        var repo = new Stub<ISubRecipeRepository>(m => {
            if (m.MethodName == "SaveAsync") { saved = ((IEnumerable<SubRecipeLineDto>)m.Args[1]).ToList(); return Task.CompletedTask; }
            if (m.MethodName == "IsUsedAsMenuIngredientAsync") return Task.FromResult(true);
            return Task.FromResult<IList<SubRecipeLineDto>>(new List<SubRecipeLineDto> { new SubRecipeLineDto { ProductId = 2, UnitMeasureId = 1, Quantity = 1 } });
        }).Value;
        var dialogs = new Stub<IDialogService>(m => null).Value;
        var vm = new SubRecipeViewModel(new SubRecipeService(repo), products, units, dialogs);
        var app = new Application();
        foreach (var resource in new[] { "Converters", "Themes/Colors", "Themes/Icons", "Components/Text", "Components/Buttons", "Components/Inputs", "Components/Containers", "Components/DataGrids" })
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/PointOfSale.UI;component/Resources/" + resource + ".xaml", UriKind.Relative) });
        var view = new SubRecipeView { DataContext = vm };
        var window = new Window { Content = view, Width = 1200, Height = 700, ShowInTaskbar = false };
        window.Show(); window.Activate(); Pump();
        var prepBox = (ComboBox)view.FindName("PrepItemComboBox");
        prepBox.ApplyTemplate();
        var editor = (TextBox)prepBox.Template.FindName("PART_EditableTextBox", prepBox);
        editor.Focus(); editor.Text = "sAmB"; Pump();
        Check(prepBox.Items.Count == 1 && prepBox.Items[0] == prep, "Prep search finds a case-insensitive middle-name match");
        Enter(prepBox);
        Check(vm.SelectedPrepItem == prep, "Enter commits the filtered prep item");
        var ingredientBox = (ComboBox)view.FindName("IngredientComboBox");
        Check(ingredientBox.IsKeyboardFocusWithin, "Prep selection focuses Ingredient");
        Check(vm.RemoveIngredientCommand.CanExecute(vm.RecipeLines[0]), "Loaded ingredients can be removed when prep is used by a menu");
        RemoveButton(view, vm.RecipeLines[0]);
        vm.SelectedIngredient = ingredient; Pump();
        ingredientBox.Focus(); Enter(ingredientBox);
        var unitBox = (ComboBox)view.FindName("UnitMeasureComboBox");
        Check(unitBox.IsKeyboardFocusWithin, "Ingredient Enter focuses unit measure");
        Enter(unitBox);
        var qty = (TextBox)view.FindName("QuantityTextBox");
        Check(qty.IsKeyboardFocusWithin, "Unit Enter focuses quantity");
        qty.Text = "2"; Enter(qty);
        Check(vm.RecipeLines.Single().Quantity == 3 && ingredientBox.IsKeyboardFocusWithin, "Quantity Enter adds once and returns focus to Ingredient");
        qty.Focus(); qty.Text = "0"; Enter(qty);
        Check(vm.RecipeLines.Single().Quantity == 3 && qty.IsKeyboardFocusWithin, "Invalid quantity adds nothing and retains focus");
        vm.SelectedIngredient = tomato; Pump();
        qty.Focus(); qty.Text = "0.25"; Enter(qty);
        var newLine = vm.RecipeLines.Single(line => line.ProductId == tomato.ProductId);
        vm.IsBusy = true; Pump();
        var newButton = Descendants<Button>(view).Single(b => ReferenceEquals(b.DataContext, newLine));
        Check(!newButton.IsEnabled, "Remove buttons remain disabled during loading or saving");
        vm.IsBusy = false; Pump();
        Click(RemoveButton(view, newLine));
        Check(vm.RecipeLines.Count == 1 && vm.RecipeLines[0].ProductId == ingredient.ProductId,
            "Clicking the new row delete button removes only the new ingredient");
        Click(RemoveButton(view, vm.RecipeLines[0]));
        Check(vm.RecipeLines.Count == 0 && vm.TotalRecipeCost == 0 && vm.SaveSubRecipeCommand.CanExecute(null), "Removing final row clears cost and allows saving");
        vm.SaveSubRecipeCommand.Execute(null);
        Check(saved != null && saved.Count == 0, "Empty recipe reaches persistence after final deletion");
        window.Close();
    }
}
