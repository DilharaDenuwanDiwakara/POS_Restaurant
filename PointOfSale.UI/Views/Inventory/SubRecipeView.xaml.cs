using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using PointOfSale.Core.Models.Inventory;
using PointOfSale.UI.ViewModels.Inventory;

namespace PointOfSale.UI.Views.Inventory
{
    public partial class SubRecipeView : UserControl
    {
        private bool _updatingPrepSearch;
        private bool _focusIngredientAfterLoad;

        public SubRecipeView()
        {
            InitializeComponent();
            IngredientComboBox.IsEnabledChanged += (_, e) =>
            {
                if ((bool)e.NewValue && _focusIngredientAfterLoad)
                {
                    _focusIngredientAfterLoad = false;
                    Dispatcher.BeginInvoke(new Action(FocusIngredient));
                }
            };
            PrepItemComboBox.AddHandler(TextBox.TextChangedEvent, new TextChangedEventHandler(PrepItemSearchChanged));
        }

        private void PrepItemSearchChanged(object sender, TextChangedEventArgs e)
        {
            if (_updatingPrepSearch || !(e.OriginalSource is TextBox editor) || !editor.IsKeyboardFocused)
            {
                return;
            }

            var view = ((CollectionViewSource)Resources["PrepItemsView"]).View;
            if (view == null)
            {
                return;
            }

            _updatingPrepSearch = true;
            try
            {
                var text = editor.Text;
                if (PrepItemComboBox.SelectedItem is Product selected && selected.ProductName == text)
                {
                    view.Filter = null;
                    return;
                }

                var caret = editor.SelectionStart;
                view.Filter = item => item is Product product &&
                    (product.ProductName ?? string.Empty).IndexOf(text.Trim(), StringComparison.CurrentCultureIgnoreCase) >= 0;
                PrepItemComboBox.IsDropDownOpen = true;
                editor.Text = text;
                editor.SelectionStart = Math.Min(caret, text.Length);
                editor.SelectionLength = 0;
            }
            finally
            {
                _updatingPrepSearch = false;
            }
        }

        private void MoveFocusOnEnter(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            e.Handled = true;
            if (sender is ComboBox comboBox)
            {
                if (ReferenceEquals(comboBox, PrepItemComboBox))
                {
                    _focusIngredientAfterLoad = true;
                }
                if (ReferenceEquals(comboBox, PrepItemComboBox) && comboBox.SelectedItem == null)
                {
                    comboBox.SelectedItem = comboBox.Items.Cast<Product>().FirstOrDefault();
                    if (comboBox.SelectedItem == null)
                    {
                        _focusIngredientAfterLoad = false;
                        return;
                    }
                }

                comboBox.IsDropDownOpen = false;
                comboBox.GetBindingExpression(ComboBox.SelectedItemProperty)?.UpdateSource();
                comboBox.GetBindingExpression(ComboBox.SelectedValueProperty)?.UpdateSource();
            }
            else if (sender is TextBox textBox)
            {
                textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            }

            if (sender is FrameworkElement element && Validation.GetHasError(element))
            {
                return;
            }

            if (ReferenceEquals(sender, QuantityTextBox))
            {
                if (DataContext is SubRecipeViewModel model && model.AddIngredientCommand.CanExecute(null))
                {
                    var quantityBeforeAdd = model.QuantityInput;
                    model.AddIngredientCommand.Execute(null);
                    // Successful additions clear the quantity; validation failures retain focus.
                    if (!string.IsNullOrEmpty(quantityBeforeAdd) && string.IsNullOrEmpty(model.QuantityInput))
                    {
                        FocusIngredient();
                    }
                }
                return;
            }

            if (ReferenceEquals(sender, PrepItemComboBox))
            {
                if (IngredientComboBox.IsEnabled)
                {
                    _focusIngredientAfterLoad = false;
                    FocusIngredient();
                }
            }
            else if (ReferenceEquals(sender, IngredientComboBox))
            {
                UnitMeasureComboBox.Focus();
            }
            else if (ReferenceEquals(sender, UnitMeasureComboBox))
            {
                QuantityTextBox.Focus();
            }
            else if (Keyboard.FocusedElement is UIElement currentFocus)
            {
                currentFocus.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }
        }

        private void FocusIngredient()
        {
            IngredientComboBox.Focus();
            IngredientComboBox.ApplyTemplate();
            if (IngredientComboBox.Template.FindName("PART_EditableTextBox", IngredientComboBox) is TextBox editor)
            {
                editor.Focus();
                editor.SelectAll();
            }
        }

        private void RemoveIngredientButton_Loaded(object sender, RoutedEventArgs e)
        {
            // Row templates can attach their command while the recipe is still loading.
            // Recheck once the button and its row parameter are fully connected.
            if (sender is Button button && button.Command is PointOfSale.UI.Commands.RelayCommand command)
            {
                command.RaiseCanExecuteChanged();
            }
        }

        private void AddIngredientButton_Click(object sender, RoutedEventArgs e)
        {
            FocusIngredient();
        }
    }
}
