using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using PointOfSale.UI.ViewModels.Restaurant;

namespace PointOfSale.UI.Views.Restaurant
{
    public partial class MenuItemView : UserControl
    {
        private MenuItemViewModel _viewModel;

        public MenuItemView()
        {
            InitializeComponent();
            DataContextChanged += MenuItemView_DataContextChanged;
        }

        private void MenuItemView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.RequestRecipeProductFocus -= OnRequestRecipeProductFocus;
                _viewModel.RequestPricingTabFocus -= OnRequestPricingTabFocus;
            }

            _viewModel = e.NewValue as MenuItemViewModel;

            if (_viewModel != null)
            {
                _viewModel.RequestRecipeProductFocus += OnRequestRecipeProductFocus;
                _viewModel.RequestPricingTabFocus += OnRequestPricingTabFocus;
            }
        }

        private void OnRequestRecipeProductFocus()
        {
            Dispatcher.BeginInvoke(new Action(() => FocusRecipeProductComboBox(resetText: true)), DispatcherPriority.Input);
        }

        private void OnRequestPricingTabFocus()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                MenuItemTabs.SelectedItem = PricingTabItem;
                TxtVariantName.Focus();
            }), DispatcherPriority.Input);
        }

        private void FocusRecipeProductComboBox(bool resetText)
        {
            if (resetText)
            {
                CmbRecipeProduct.SelectedIndex = -1;
                CmbRecipeProduct.Text = string.Empty;
            }

            CmbRecipeProduct.Focus();
            CmbRecipeProduct.IsDropDownOpen = true;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // KEYBOARD NAVIGATION STATE MACHINE
        // ─────────────────────────────────────────────────────────────────────────

        // Single handler wired to PreviewKeyDown on every control in the flow.
        private void MoveFocusOnEnter(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            // When a ComboBox dropdown is open, Enter selects the current item first.
            // Navigation waits for the next Enter press (dropdown will be closed by then).
            if (sender is ComboBox cmb && cmb.IsDropDownOpen) return;

            // Flush pending TwoWay binding updates so the ViewModel sees the latest value,
            // then abort navigation if the current field has a validation error.
            CommitBindings(sender);
            if (sender is FrameworkElement fe && Validation.GetHasError(fe))
            {
                e.Handled = true;
                return;
            }

            UIElement next = GetNextFocusTarget(sender as FrameworkElement);

            if (next != null)
                FocusElement(next);
            else
                // Fallback for controls not in the state machine (e.g. BOM tab fields).
                (Keyboard.FocusedElement as UIElement)?.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));

            // Prevent TextBox from inserting a newline and Button from treating
            // Enter as a click (which would double-fire the command).
            e.Handled = true;
        }

        // Explicit, named focus chain — each control maps to exactly one successor.
        // Returning null tells the caller to fall back to standard tab-order traversal.
        private UIElement GetNextFocusTarget(FrameworkElement current)
        {
            if (current == null) return null;

            if (ReferenceEquals(current, TxtItemName)) return CmbCategory;
            if (ReferenceEquals(current, CmbCategory)) return CmbTargetStation;
            if (ReferenceEquals(current, CmbTargetStation)) return TxtDescription;
            if (ReferenceEquals(current, TxtDescription)) return ChkNoVariant;

            // Conditional branch: skip Name + Portion fields when "No Variant" is active.
            if (ReferenceEquals(current, ChkNoVariant))
                return ChkNoVariant.IsChecked == true
                    ? (UIElement)TxtVariantPrice
                    : (UIElement)TxtVariantName;

            if (ReferenceEquals(current, TxtVariantName)) return TxtPortionSize;
            if (ReferenceEquals(current, TxtPortionSize)) return TxtVariantPrice;
            if (ReferenceEquals(current, TxtVariantPrice)) return BtnAddVariant;

            return null;
        }

        private void MenuItemTabs_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Tab || (Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
                return;

            if (!(sender is TabControl tabControl) || tabControl.Items.Count == 0)
                return;

            var step = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift ? -1 : 1;
            var selectedIndex = tabControl.SelectedIndex < 0 ? 0 : tabControl.SelectedIndex;
            tabControl.SelectedIndex = (selectedIndex + step + tabControl.Items.Count) % tabControl.Items.Count;

            e.Handled = true;

            if (ReferenceEquals(tabControl.SelectedItem, BomTabItem))
            {
                Dispatcher.BeginInvoke(new Action(() => FocusRecipeProductComboBox(resetText: false)), DispatcherPriority.Input);
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // ADD VARIANT BUTTON — loop-back after add
        // ─────────────────────────────────────────────────────────────────────────

        // Enter key on the focused button: execute the command once, then loop back.
        // e.Handled = true prevents ButtonBase from also treating Enter as a Click,
        // which would execute the command a second time.
        private void BtnAddVariant_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            e.Handled = true;

            if (sender is Button btn && btn.Command?.CanExecute(btn.CommandParameter) == true)
                btn.Command.Execute(btn.CommandParameter);

            ReturnFocusToVariantName();
        }

        // Mouse click: the Command binding already executed the add — only return focus.
        private void BtnAddVariant_Click(object sender, RoutedEventArgs e)
        {
            ReturnFocusToVariantName();
        }

        private void ReturnFocusToVariantName()
        {
            // DispatcherPriority.Input ensures the DataGrid row has been added and the
            // ViewModel's NewVariantName has been cleared before we restore focus.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                TxtVariantName.Focus();
                TxtVariantName.SelectAll();
            }), DispatcherPriority.Input);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // CHECKBOX CHECKED HANDLER
        // ─────────────────────────────────────────────────────────────────────────

        // Fires when the user checks the box via mouse click or Space key.
        // The Enter-key path is handled by GetNextFocusTarget above.
        private void NoVariant_Checked(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                TxtVariantPrice.Focus();
                TxtVariantPrice.SelectAll();
            }), DispatcherPriority.Input);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────────────────────────────────

        private static void CommitBindings(object sender)
        {
            if (sender is TextBox txt)
            {
                txt.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            }
            else if (sender is ComboBox cmb)
            {
                // Update whichever binding style the ComboBox uses.
                cmb.GetBindingExpression(ComboBox.SelectedValueProperty)?.UpdateSource();
                cmb.GetBindingExpression(ComboBox.SelectedItemProperty)?.UpdateSource();
            }
        }

        private static void FocusElement(UIElement element)
        {
            element.Focus();
            if (element is TextBox tb) tb.SelectAll();
        }

        // ─────────────────────────────────────────────────────────────────────────
        // NUMERIC / SELECT-ALL HELPERS (unchanged)
        // ─────────────────────────────────────────────────────────────────────────

        private void SelectAllText_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox txt)
                Dispatcher.BeginInvoke(new Action(() => txt.SelectAll()));
        }

        private void QtyTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is TextBox textBox)
                textBox.SelectAll();
        }

        private void QtyTextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox textBox && !textBox.IsKeyboardFocusWithin)
            {
                e.Handled = true;
                textBox.Focus();
            }
        }

        private void QtyTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (Regex.IsMatch(e.Text, "[^0-9.]+"))
            {
                e.Handled = true;
                return;
            }

            if (e.Text == "." && sender is TextBox tb)
            {
                if (tb.Text.Contains(".") && !tb.SelectedText.Contains("."))
                    e.Handled = true;
            }
        }
    }
}
