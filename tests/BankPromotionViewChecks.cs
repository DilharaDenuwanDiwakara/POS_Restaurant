using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Services;
using PointOfSale.Core.Interfaces.Repositories.Restaurant;
using PointOfSale.Core.Interfaces.Repositories.Sales;
using PointOfSale.Core.Interfaces.Repositories.System;
using PointOfSale.Core.Models.Restaurant;
using PointOfSale.Core.Models.Sales;
using PointOfSale.Core.Models.System;
using PointOfSale.UI.ViewModels.Sales;
using PointOfSale.UI.Views.Sales;
using MenuItem = PointOfSale.Core.Models.Restaurant.MenuItem;

internal static class BankPromotionViewChecks
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    [STAThread]
    private static void Main(string[] args)
    {
        var saved = new FakePromotions();
        var variants = new FakeVariants();
        var dialogs = new FakeDialogs();
        var vm = new BankPromotionViewModel(saved, new FakeBanks(), variants, new FakeCategories(), dialogs);
        vm.Initialization.GetAwaiter().GetResult();
        Check(vm.Banks.Count == 2 && !vm.IsBusy && vm.SelectedTabIndex == 0, "Initial state loads banks and opens master list");
        Check(vm.OngoingPromotions.Count == 1, "Saved promotions load on initialization");
        var existingStart = vm.OngoingPromotions[0].ValidFrom;
        var existingEnd = vm.OngoingPromotions[0].ValidTo;
        vm.OngoingPromotions[0].ValidTo = new DateTime(9999, 12, 31, 23, 59, 59).AddMilliseconds(997);
        vm.OngoingPromotions[0].ValidFrom = new DateTime(1900, 1, 1);
        vm.EditPromotionCommand.Execute(vm.OngoingPromotions[0]);
        Check(vm.ValidFromDate == DateTime.Today && vm.ValidFromTime == "00:00:00", "Legacy 1900 placeholder uses today's date in the draft");
        Check(vm.OngoingPromotions[0].ValidFrom == new DateTime(1900, 1, 1), "Opening the editor must not rewrite saved validity");
        Check(vm.ValidToDate == DateTime.Today && vm.ValidToTime == "23:59:59", "Legacy open-ended Valid To uses today at end of day in the draft");
        Check(vm.OngoingPromotions[0].ValidTo.Year == 9999, "Editing must not mutate the saved end date before Save");
        vm.OngoingPromotions[0].ValidTo = existingEnd;
        vm.OngoingPromotions[0].ValidFrom = existingStart;
        vm.EditPromotionCommand.Execute(vm.OngoingPromotions[0]);
        Check(vm.SelectedPromotion.Id == 77 && vm.SelectedTabIndex == 1 && vm.PromoName == "Existing" && vm.ExcludedVariants.Count == 1, "Edit selects the master and populates the editor");
        Check(vm.ValidFromDate == new DateTime(2026, 10, 28) && vm.ValidFromTime == "09:15:30" && vm.ValidToTime == "22:45:15" && vm.ValidToDate == existingEnd.Date, "Edit splits exact stored timestamps into date and time fields");
        Check(vm.RequiredCardType == "CREDIT", "Editing loads the stored required card type");
        vm.ExcludedVariants.Clear();
        Check(vm.OngoingPromotions[0].ExcludedVariants.Count == 1, "Draft exclusions cannot change master data before saving");
        vm.DiscountPercentage = "15";
        vm.SaveCommand.Execute(null);
        Check(saved.Promotion.Id == 77 && saved.Variants.Count == 0 && vm.SelectedTabIndex == 0 && vm.HasStatusMessage, "Edit updates the existing ID, removes all exclusions, refreshes and navigates");
        Check(saved.Promotion.RequiredCardType == "CREDIT", "Saving promotion edits preserves its card type restriction");
        vm.AddNewPromotionCommand.Execute(null);
        Check(vm.SelectedPromotion == null && vm.SelectedTabIndex == 1 && vm.SelectedBank == null && vm.PromoName == "", "Add New resets the editor and navigates");
        vm.SelectedBank = vm.Banks[0]; vm.PromoName = " Card offer "; vm.DiscountPercentage = "12.5";
        vm.SelectedCategory = vm.Categories[0]; vm.VariantsLoadTask.GetAwaiter().GetResult();
        vm.SelectedVariant = vm.AvailableVariants[0];
        vm.AddExcludedVariantCommand.Execute(null); vm.AddExcludedVariantCommand.Execute(null);
        Check(vm.ExcludedVariants.Count == 1 && !vm.AddExcludedVariantCommand.CanExecute(null), "Duplicate variant exclusions are prevented");
        vm.ValidFromDate = new DateTime(2026, 10, 28); vm.ValidFromTime = "09:15:30";
        vm.ValidToDate = new DateTime(2026, 10, 28);
        foreach (var invalidEnd in new[] { "09:15:30", "09:15:29", "25:00", "nonsense", "9:15" })
        {
            vm.ValidToTime = invalidEnd; vm.SaveCommand.Execute(null);
            Check(saved.Saves == 1 && vm.HasErrors, "Equal, reversed and malformed validity must not save: " + invalidEnd);
        }
        vm.ValidToTime = "09:15:31";
        vm.ValidFromDate = null; vm.SaveCommand.Execute(null);
        Check(saved.Saves == 1 && vm.HasErrors, "Missing date must not save");
        vm.ValidFromDate = new DateTime(1700, 1, 1); vm.SaveCommand.Execute(null);
        Check(saved.Saves == 1 && vm.HasErrors, "Dates outside SQL DATETIME range must not save");
        vm.ValidFromDate = new DateTime(2026, 10, 28);
        var slow = new TaskCompletionSource<IEnumerable<Variant>>();
        variants.Pending = slow;
        vm.SelectedCategory = vm.Categories[1]; var stale = vm.VariantsLoadTask;
        vm.SelectedCategory = vm.Categories[0];
        slow.SetResult(new[] { new Variant { Id = 201, Name = "Stale" } }); stale.GetAwaiter().GetResult();
        Check(vm.AvailableVariants.Count == 2 && vm.AvailableVariants.All(v => v.Id != 201), "Earlier category responses cannot replace current variants");
        variants.Fail = true;
        vm.SelectedCategory = vm.Categories[1];
        Check(vm.HasError && !vm.IsLoadingVariants && vm.AvailableVariants.Count == 0, "Category load failure clears old variants and restores loading state");
        variants.Fail = false;
        vm.SaveCommand.Execute(null);
        Check(saved.Promotion.Id == 0 && saved.Promotion.PromoName == "Card offer" && saved.Promotion.DiscountPercentage == 12.5m && saved.Variants.SequenceEqual(new[] { 101 }), "Insert saves promotion and exact exclusions together without BINs");
        Check(saved.Promotion.ValidFrom == new DateTime(2026, 10, 28, 9, 15, 30) && saved.Promotion.ValidTo == new DateTime(2026, 10, 28, 9, 15, 31), "Saved timestamps combine date and time with second precision");
        Check(vm.SelectedBank == null && vm.ExcludedVariants.Count == 0 && !vm.HasErrors, "Successful save clears the draft");
        vm.AddNewPromotionCommand.Execute(null);
        vm.SelectedBank = vm.Banks[0]; vm.PromoName = "Validation";
        foreach (var invalid in new[] { "invalid", "0", "101", "-1", "1e1", "10.001", "12,50", "??" })
        {
            vm.DiscountPercentage = invalid; vm.SaveCommand.Execute(null);
            Check(saved.Saves == 2 && vm.HasErrors, "Invalid percentage must not save: " + invalid);
        }
        vm.DiscountPercentage = "15"; saved.Fail = true; vm.SaveCommand.Execute(null);
        Check(vm.HasError && vm.PromoName == "Validation" && vm.SelectedTabIndex == 1 && !vm.IsBusy, "Failed save preserves draft and restores commands");
        saved.Fail = false;
        vm.SelectedGlobalBank = vm.Banks[0]; vm.BinsLoadTask.GetAwaiter().GetResult();
        vm.BinPrefix = "12345"; vm.AddBinCommand.Execute(null);
        Check(vm.BinPrefixes.Count == 0 && vm.HasError, "Short BIN rejected");
        vm.BinCardType = "Debit"; vm.BinCardBrand = "Visa";
        vm.BinPrefix = "012345"; vm.AddBinCommand.Execute(null);
        Check(vm.BinPrefixes.Count == 1 && saved.Bins.Single().BankId == 1 && vm.BinPrefix == "", "Add BIN persists against global bank immediately");
        vm.BinPrefix = "012345"; vm.AddBinCommand.Execute(null);
        Check(saved.Bins.Count == 1 && vm.HasError, "Duplicate BIN rejected");
        vm.ClearCommand.Execute(null);
        Check(vm.SelectedGlobalBank.Id == 1 && vm.BinPrefixes.Count == 1, "Clearing promotion leaves global BIN settings intact");
        var pendingBins = new TaskCompletionSource<IEnumerable<BankBINMapping>>();
        saved.PendingBins = pendingBins;
        vm.SelectedGlobalBank = vm.Banks[1]; var staleBins = vm.BinsLoadTask;
        Check(vm.IsLoadingBins && !vm.AddBinCommand.CanExecute(null) && vm.BinPrefixes.Count == 0, "Bank switch clears old BINs and disables Add while loading");
        vm.SelectedGlobalBank = vm.Banks[0];
        pendingBins.SetResult(new[] { new BankBINMapping { BankId = 2, BINPrefix = "999999" } }); staleBins.GetAwaiter().GetResult();
        Check(vm.BinPrefixes.Single().BINPrefix == "012345" && !vm.IsLoadingBins, "Stale BIN response cannot replace current bank");
        var originalBin = vm.BinPrefixes.Single();
        Check(originalBin.CardType == "DEBIT" && originalBin.CardBrand == "VISA", "Adding a BIN includes its type and brand");
        vm.EditBinCommand.Execute(originalBin);
        Check(vm.BinCardType == "DEBIT" && vm.BinCardBrand == "VISA", "BIN editing loads existing metadata");
        Check(vm.IsEditingBin && vm.BinPrefix == "012345" && !vm.CanChangeGlobalBank && vm.BinSaveButtonText == "Save BIN", "BIN edit loads a separate draft and locks bank selection");
        vm.BinPrefix = "001234"; vm.IsBinActive = false;
        Check(originalBin.BINPrefix == "012345" && originalBin.IsActive, "Editing does not mutate saved BIN row");
        vm.CancelBinEditCommand.Execute(null);
        Check(!vm.IsEditingBin && vm.BinPrefix == "" && vm.BinPrefixes.Single().BINPrefix == "012345", "Cancel discards BIN changes");
        vm.EditBinCommand.Execute(originalBin); vm.BinPrefix = "001234"; vm.IsBinActive = false;
        saved.FailBinUpdate = true; vm.SaveBinCommand.Execute(null);
        Check(vm.IsEditingBin && vm.BinPrefix == "001234" && vm.HasError && !vm.IsBusy && vm.BinPrefixes.Single().BINPrefix == "012345", "Failed BIN update keeps draft and saved row intact");
        saved.FailBinUpdate = false;
        saved.PendingBinUpdate = new TaskCompletionSource<bool>();
        vm.SaveBinCommand.Execute(null);
        var updateCount = saved.BinUpdates;
        Check(vm.IsBusy && !vm.EditPromotionCommand.CanExecute(vm.OngoingPromotions[0]) && !vm.EditBinCommand.CanExecute(originalBin) && !vm.CancelBinEditCommand.CanExecute(null), "Row actions disable while saving");
        vm.SaveBinCommand.Execute(null);
        Check(saved.BinUpdates == updateCount, "Async commands reject reentrant execution");
        saved.PendingBinUpdate.SetResult(true);
        Check(!vm.IsBusy && !vm.IsEditingBin && vm.BinPrefixes.Single().Id == originalBin.Id && vm.BinPrefixes.Single().BINPrefix == "001234" && !vm.BinPrefixes.Single().IsActive, "BIN update preserves identity and refreshes number and active state");
        Check(vm.BinPrefixes.Single().CardType == "DEBIT" && vm.BinPrefixes.Single().CardBrand == "VISA", "Updating a BIN number preserves card metadata");
        vm.EditBinCommand.Execute(vm.BinPrefixes.Single());
        vm.SelectedGlobalBank = vm.Banks[1];
        Check(!vm.IsEditingBin && vm.BinPrefix == "", "Changing bank cancels any old BIN draft");
        vm.SelectedGlobalBank = vm.Banks[0];
        vm.SelectedCategory = vm.Categories[1]; var clearedLoad = vm.VariantsLoadTask;
        vm.ClearCommand.Execute(null);
        Check(vm.SelectedCategory == null && vm.AvailableVariants.Count == 0 && !vm.IsLoadingVariants, "Clear invalidates category selection");
        vm.SelectedBank = vm.Banks[0]; vm.PromoName = "Weekend card offer"; vm.DiscountPercentage = "12.5";
        vm.SelectedCategory = vm.Categories[0]; vm.SelectedVariant = vm.AvailableVariants[0]; vm.AddExcludedVariantCommand.Execute(null);
        var app = new Application();
        foreach (var resource in new[] { "Converters", "Themes/Colors", "Themes/Icons", "Components/Text", "Components/Buttons", "Components/Inputs", "Components/Containers", "Components/DataGrids", "Styles/ViewStyles" })
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/PointOfSale.UI;component/Resources/" + resource + ".xaml", UriKind.Relative) });
        var view = new BankPromotionView { DataContext = vm, Width = 1180, Height = 900, Background = Brushes.WhiteSmoke };
        Action layout = () =>
        {
            view.Measure(new Size(1180, 900)); view.Arrange(new Rect(0, 0, 1180, 900)); view.UpdateLayout();
            System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(() => { }));
            view.UpdateLayout();
        };
        vm.SelectedTabIndex = 1; layout();
        var tabs = Descendants(view).OfType<TabControl>().Single();
        Check(tabs.Items.Count == 3 && tabs.SelectedIndex == 1, "Three tabs and two-way navigation binding render");
        Check(Descendants(view).OfType<DatePicker>().Count() == 2, "Validity dates render with centralized DatePicker styling");
        Check(Descendants(view).OfType<ComboBox>().Count(c => c.IsEditable) == 2, "Validity times render as editable time dropdowns");
        Check(Descendants(view).OfType<System.Windows.Controls.Primitives.DatePickerTextBox>().All(t => t.IsReadOnly), "Date inputs use the calendar to avoid stale date values after invalid typed text");
        var percentageInput = Descendants(view).OfType<TextBox>().Single(t => (string)t.Tag == "Percentage");
        var excludedGrid = Descendants(view).OfType<DataGrid>().Single(g => ReferenceEquals(g.ItemsSource, vm.ExcludedVariants));
        Check(excludedGrid.Columns.Single(c => (string)c.Header == "Variant").ActualWidth > 150, "Exclusions use available width");
        var removeButton = Descendants(excludedGrid).OfType<Button>().Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Remove excluded variant");
        Check(removeButton.IsEnabled, "Exclusion Remove must be enabled: parameter=" + removeButton.CommandParameter?.GetType().FullName + "; context=" + removeButton.DataContext?.GetType().FullName + "; busy=" + vm.IsBusy + "; command=" + removeButton.Command?.GetType().FullName + "; CanExecute=" + removeButton.Command?.CanExecute(removeButton.CommandParameter) + "; grid=" + excludedGrid.IsEnabled);
        Action<TextBox> checkInput = input =>
        {
            var paste = new DataObjectPastingEventArgs(new DataObject(DataFormats.UnicodeText, "12ab"), false, DataFormats.UnicodeText);
            input.RaiseEvent(paste); Check(paste.CommandCancelled, "Invalid numeric paste rejected");
        };
        checkInput(percentageInput);
        InvokeButton(removeButton); layout();
        Check(vm.ExcludedVariants.Count == 0, "Rendered Remove button executes its row command");
        vm.AddExcludedVariantCommand.Execute(null); layout();
        var bitmap = new RenderTargetBitmap(1180, 900, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(args[0])) encoder.Save(stream);
        tabs.SelectedIndex = 2; layout();
        Check(vm.SelectedTabIndex == 2, "User tab navigation updates the ViewModel");
        var binInput = Descendants(view).OfType<TextBox>().Single(t => (string)t.Tag == "BIN");
        Check(binInput.MaxLength == 6, "BIN input length"); checkInput(binInput);
        Check(Descendants(view).OfType<DataGrid>().Any(g => ReferenceEquals(g.ItemsSource, vm.BinPrefixes)), "Global tab renders selected bank BINs");
        var editBinButton = Descendants(view).OfType<Button>().Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Edit BIN");
        Check(editBinButton.IsEnabled, "Rendered BIN Edit action is enabled");
        InvokeButton(editBinButton); layout();
        Check(vm.IsEditingBin && vm.BinPrefix == "001234", "Rendered BIN Edit button loads its row");
        var binTypeInput = (ComboBox)view.FindName("BinCardTypeInput");
        var binBrandInput = (TextBox)view.FindName("BinCardBrandInput");
        Check((string)binTypeInput.SelectedItem == "DEBIT" && binBrandInput.Text == "VISA", "BIN editor displays saved type and brand");
        binTypeInput.SelectedItem = "CREDIT";
        binBrandInput.Text = "  mAsTeRcArD  ";
        Check(vm.BinCardType == "CREDIT" && vm.BinCardBrand == "  MASTERCARD  ", "BIN metadata inputs update the ViewModel");
        vm.BinPrefix = "002345";
        var saveBinButton = Descendants(view).OfType<Button>().Single(b => ReferenceEquals(b.Command, vm.SaveBinCommand));
        Check(saveBinButton.IsEnabled, "Rendered BIN Save action is enabled");
        InvokeButton(saveBinButton); layout();
        Check(!vm.IsEditingBin && vm.BinPrefixes.Single().BINPrefix == "002345", "Rendered BIN Save action persists and exits edit mode");
        Check(vm.BinPrefixes.Single().CardType == "CREDIT" && vm.BinPrefixes.Single().CardBrand == "MASTERCARD", "Saving from UI updates BIN type and brand");
        var binList = Descendants(view).OfType<DataGrid>().Single(g => ReferenceEquals(g.ItemsSource, vm.BinPrefixes));
        Check(binList.Columns.Any(c => (string)c.Header == "Card Type") && binList.Columns.Any(c => (string)c.Header == "Card Brand"), "BIN grid displays card metadata columns");
        var globalBitmap = new RenderTargetBitmap(1180, 900, 96, 96, PixelFormats.Pbgra32); globalBitmap.Render(view);
        var globalEncoder = new PngBitmapEncoder(); globalEncoder.Frames.Add(BitmapFrame.Create(globalBitmap));
        using (var stream = File.Create(Path.ChangeExtension(args[0], ".bins.png"))) globalEncoder.Save(stream);
        tabs.SelectedIndex = 0; layout();
        Check(Descendants(view).OfType<DataGrid>().Any(g => ReferenceEquals(g.ItemsSource, vm.OngoingPromotions)), "Master tab renders saved promotions");
        var editButton = Descendants(view).OfType<Button>().Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Edit promotion");
        Check(editButton.IsEnabled, "Promotion Edit must be enabled: parameter=" + editButton.CommandParameter?.GetType().FullName);
        var masterBitmap = new RenderTargetBitmap(1180, 900, 96, 96, PixelFormats.Pbgra32); masterBitmap.Render(view);
        var masterEncoder = new PngBitmapEncoder(); masterEncoder.Frames.Add(BitmapFrame.Create(masterBitmap));
        using (var stream = File.Create(Path.ChangeExtension(args[0], ".master.png"))) masterEncoder.Save(stream);
        InvokeButton(editButton); layout();
        Check(vm.SelectedTabIndex == 1 && vm.SelectedPromotion.Id == 77 && vm.PromoName == "Existing", "Rendered promotion Edit button navigates and populates the draft");
        // Use a real presentation source to verify keyboard focus through composite controls.
        var requiredTypeInput = (ComboBox)view.FindName("RequiredCardTypeInput");
        Check((string)requiredTypeInput.SelectedItem == "CREDIT", "Promotion editor displays required card type");
        requiredTypeInput.SelectedItem = "DEBIT";
        Check(vm.RequiredCardType == "DEBIT", "Required card type selector updates the promotion draft");
        var keyboardWindow = new Window { Content = view, Width = 1200, Height = 940, ShowInTaskbar = false };
        try
        {
            keyboardWindow.Show(); keyboardWindow.Activate(); layout();
            vm.SelectedCategory = vm.Categories[0];
            vm.SelectedVariant = vm.AvailableVariants[1]; layout();
            var keyboardFields = new[] { "BankInput", "PromotionNameInput", "DiscountInput", "RequiredCardTypeInput", "ValidFromDateInput", "ValidFromTimeInput", "ValidToDateInput", "ValidToTimeInput", "PromotionActiveInput", "CategoryInput", "VariantInput", "ExcludeVariantButton" };
            ((Control)view.FindName(keyboardFields[0])).Focus();
            for (var i = 1; i < keyboardFields.Length; i++)
            {
                EnterFocusedInput();
                var next = (Control)view.FindName(keyboardFields[i]);
                Check(next.IsKeyboardFocusWithin, "Enter from " + keyboardFields[i - 1] + " must focus " + keyboardFields[i] + "; actual=" + (Keyboard.FocusedElement as FrameworkElement)?.Name);
            }
            var categoryInput = (ComboBox)view.FindName("CategoryInput");
            categoryInput.Focus(); categoryInput.IsDropDownOpen = true;
            EnterFocusedInput();
            Check(!((Control)view.FindName("VariantInput")).IsKeyboardFocusWithin, "Enter in an open dropdown must not skip selection");
            categoryInput.IsDropDownOpen = false;
            tabs.SelectedIndex = 2; layout();
            var binKeyboardFields = new[] { "GlobalBankInput", "BinPrefixInput", "BinCardTypeInput", "BinCardBrandInput", "BinActiveInput", "SaveBinButton" };
            ((Control)view.FindName(binKeyboardFields[0])).Focus();
            for (var i = 1; i < binKeyboardFields.Length; i++)
            {
                EnterFocusedInput();
                Check(((Control)view.FindName(binKeyboardFields[i])).IsKeyboardFocusWithin, "Enter must focus " + binKeyboardFields[i]);
            }
            var deleteRow = vm.BinPrefixes.Single();
            var deleteButton = Descendants(view).OfType<Button>().Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Delete BIN");
            Check(deleteButton.IsEnabled && !deleteRow.IsActive, "Inactive BIN still exposes an enabled Delete action");
            dialogs.Confirm = false;
            InvokeButton(deleteButton); layout();
            Check(saved.BinDeletes == 0 && vm.BinPrefixes.Count == 1 && !vm.IsBusy, "Cancelling confirmation keeps the BIN and skips persistence");
            dialogs.Confirm = true;
            vm.EditBinCommand.Execute(deleteRow);
            saved.FailBinDelete = true;
            InvokeButton(deleteButton); layout();
            Check(vm.HasError && vm.BinPrefixes.Count == 1 && vm.IsEditingBin && !vm.IsBusy, "Failed deletion retains saved row and edit draft");
            saved.FailBinDelete = false;
            saved.PendingBinDelete = new TaskCompletionSource<bool>();
            InvokeButton(deleteButton); layout();
            Check(vm.IsBusy && !vm.EditBinCommand.CanExecute(deleteRow) && !vm.DeleteBinCommand.CanExecute(deleteRow), "Deletion disables conflicting actions until completion");
            var deletes = saved.BinDeletes;
            vm.DeleteBinCommand.Execute(deleteRow);
            Check(saved.BinDeletes == deletes, "A pending delete cannot execute twice");
            saved.PendingBinDelete.SetResult(true);
            System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(() => { }));
            layout();
            Check(vm.BinPrefixes.Count == 0 && saved.Bins.Count == 0 && !vm.IsEditingBin && vm.BinPrefix == "" && !vm.IsBusy && vm.HasStatusMessage, "Successful deletion removes the BIN and clears its edit draft");
            Check(!vm.DeleteBinCommand.CanExecute(deleteRow), "Deleted rows cannot execute Delete again");
            vm.BinPrefix = "003456"; vm.BinCardType = "Credit"; vm.BinCardBrand = "Visa";
            vm.SaveBinCommand.Execute(null); layout();
            var activeRow = vm.BinPrefixes.Single();
            Check(activeRow.IsActive && vm.DeleteBinCommand.CanExecute(activeRow), "Active BINs can also be deleted");
            Check(!vm.DeleteBinCommand.CanExecute(new BankBINMapping { Id = activeRow.Id, BankId = 2 }), "Delete rejects rows from another bank");
            InvokeButton(Descendants(view).OfType<Button>().Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Delete BIN"));
            layout();
            Check(vm.BinPrefixes.Count == 0, "Rendered Delete button removes an active BIN");
        }
        finally { keyboardWindow.Close(); }
        Console.WriteLine("Bank promotion navigation, insert/update, validation, asynchronous loading and XAML rendering passed. Preview: " + args[0]);
    }

    private static void EnterFocusedInput()
    {
        var input = (UIElement)Keyboard.FocusedElement;
        input.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(input), 0, Key.Enter) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(() => { }));
    }

    private static void InvokeButton(Button button)
    {
        var peer = new System.Windows.Automation.Peers.ButtonAutomationPeer(button);
        var provider = (System.Windows.Automation.Provider.IInvokeProvider)peer.GetPattern(System.Windows.Automation.Peers.PatternInterface.Invoke);
        provider.Invoke();
        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(() => { }));
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private sealed class FakePromotions : IBankPromotionRepository
    {
        public BankPromotion Promotion;
        public List<int> Variants;
        public List<BankBINMapping> Bins = new List<BankBINMapping>();
        public int Saves;
        public bool Fail;
        public TaskCompletionSource<IEnumerable<BankBINMapping>> PendingBins;
        public Task<IEnumerable<BankPromotionDto>> GetAllAsync() => Task.FromResult<IEnumerable<BankPromotionDto>>(new[]
        {
            new BankPromotionDto { ValidFrom = new DateTime(2026, 10, 28, 9, 15, 30), ValidTo = new DateTime(2026, 10, 29, 22, 45, 15), Id = 77, BankId = 1, RequiredCardType = "Credit", BankName = "Sample Bank", PromoName = "Existing", DiscountPercentage = 10,
                ExcludedVariants = new List<Variant> { new Variant { Id = 101, Name = "Existing excluded variant" } } }
        });
        public Task<IEnumerable<BankBINMapping>> GetBinsAsync(int bankId) => bankId == 2 && PendingBins != null ? PendingBins.Task : Task.FromResult<IEnumerable<BankBINMapping>>(Bins.Where(b => b.BankId == bankId).ToList());
        public Task<int> AddBinAsync(BankBINMapping bin) { bin.Id = 10 + Bins.Count; Bins.Add(bin); return Task.FromResult(bin.Id); }
        public bool FailBinUpdate;
        public TaskCompletionSource<bool> PendingBinUpdate;
        public int BinUpdates;
        public async Task UpdateBinAsync(BankBINMapping bin)
        {
            BinUpdates++;
            if (PendingBinUpdate != null) await PendingBinUpdate.Task;
            if (FailBinUpdate) throw new InvalidOperationException("BIN update failed");
            var index = Bins.FindIndex(b => b.Id == bin.Id && b.BankId == bin.BankId);
            if (index < 0) throw new InvalidOperationException("Missing BIN");
            Bins[index] = bin;
        }
        public bool FailBinDelete;
        public TaskCompletionSource<bool> PendingBinDelete;
        public int BinDeletes;
        public async Task DeleteBinAsync(int id, int bankId)
        {
            BinDeletes++;
            if (PendingBinDelete != null) await PendingBinDelete.Task;
            if (FailBinDelete) throw new InvalidOperationException("Delete failed");
            Bins.RemoveAll(b => b.Id == id && b.BankId == bankId);
        }
        public Task<int> SaveAsync(BankPromotion p, IEnumerable<int> variants)
        {
            if (Fail) throw new InvalidOperationException("Save failed");
            Promotion = p; Variants = variants.ToList(); Saves++;
            return Task.FromResult(77);
        }
    }
    private sealed class FakeDialogs : IDialogService
    {
        public bool Confirm;
        public bool ShowConfirmation(string message, string title) => Confirm;
        public void ShowMessage(string message, string title, DialogMessageType type = DialogMessageType.Information) { }
        public bool? ShowDialog<T>(out T vm) where T : class { vm = null; return false; }
        public bool? ShowDialog<T>(Action<T> configure, out T vm) where T : class { vm = null; return false; }
    }
    private sealed class FakeBanks : IBankRepository
    {
        public Task<IEnumerable<Bank>> GetAllAsync() => Task.FromResult<IEnumerable<Bank>>(new[] { new Bank { Id = 1, BankName = "Sample Bank", IsActive = true }, new Bank { Id = 2, IsActive = false } });
        public Task<int> CreateAsync(Bank b) => throw new NotSupportedException();
        public Task UpdateAsync(Bank b) => throw new NotSupportedException();
        public Task DeleteAsync(int id) => throw new NotSupportedException();
    }
    private sealed class FakeVariants : IMenuItemRepository
    {
        public TaskCompletionSource<IEnumerable<Variant>> Pending;
        public bool Fail;
        public Task<IEnumerable<Variant>> GetVariantsByCategoryIdAsync(int categoryId)
        {
            if (Fail) throw new InvalidOperationException("Variant load failed");
            if (categoryId == 2 && Pending != null) return Pending.Task;
            return Task.FromResult<IEnumerable<Variant>>(categoryId == 1 ? new[]
            {
                new Variant { Id = 101, ItemCode = "A001", Name = "CHICKEN FRIED RICE - EXTRA LARGE FAMILY PORTION" },
                new Variant { Id = 102, ItemCode = "B002", Name = "Plain Tea - Small" }
            } : new Variant[0]);
        }
        public Task<IEnumerable<MenuVariantDto>> GetAllVariantsForSalesAsync() => throw new NotSupportedException();
        public Task<int> CreateAsync(MenuItem item, IEnumerable<int> taxes, int user) => throw new NotSupportedException();
        public Task<IEnumerable<MenuItemDto>> GetAllWithDetailsAsync() => throw new NotSupportedException();
        public Task<IEnumerable<VariantPriceDto>> GetVariantsByItemIdAsync(int id) => throw new NotSupportedException();
        public Task UpdateVariantPricesAsync(IEnumerable<VariantPriceDto> variants) => throw new NotSupportedException();
        public Task<MenuItem> GetByIdAsync(int id) => throw new NotSupportedException();
        public Task<IEnumerable<int>> GetSelectedTaxIdsAsync(int id) => throw new NotSupportedException();
        public Task UpdateAsync(MenuItem item, IEnumerable<int> taxes, int user) => throw new NotSupportedException();
        public Task DeleteAsync(int id) => throw new NotSupportedException();
    }
    private sealed class FakeCategories : IMenuCategoryRepository
    {
        public Task<IEnumerable<MenuCategory>> GetAllAsync() => Task.FromResult<IEnumerable<MenuCategory>>(new[]
        {
            new MenuCategory { Id = 1, Name = "Tea", IsActive = true, DisplayOrder = 1 },
            new MenuCategory { Id = 2, Name = "Food", IsActive = true, DisplayOrder = 2 },
            new MenuCategory { Id = 3, Name = "Empty", IsActive = true, DisplayOrder = 3 },
            new MenuCategory { Id = 4, Name = "Inactive", IsActive = false }
        });
        public Task<int> CreateAsync(MenuCategory category) => throw new NotSupportedException();
        public Task UpdateAsync(MenuCategory category) => throw new NotSupportedException();
        public Task UpdateMenuCategoryAsync(MenuCategory category) => throw new NotSupportedException();
        public Task DeleteAsync(int id) => throw new NotSupportedException();
    }
}
