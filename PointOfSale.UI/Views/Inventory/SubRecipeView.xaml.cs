using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PointOfSale.UI.Views.Inventory
{
    public partial class SubRecipeView : UserControl
    {
        public SubRecipeView()
        {
            InitializeComponent();
        }

        private void MoveFocusOnEnter(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            if (sender is ComboBox comboBox)
            {
                if (comboBox.IsDropDownOpen)
                {
                    return;
                }

                comboBox.GetBindingExpression(ComboBox.TextProperty)?.UpdateSource();
                comboBox.GetBindingExpression(ComboBox.SelectedItemProperty)?.UpdateSource();
                comboBox.GetBindingExpression(ComboBox.SelectedValueProperty)?.UpdateSource();
            }
            else if (sender is TextBox textBox)
            {
                textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            }

            if (sender is FrameworkElement element && Validation.GetHasError(element))
            {
                e.Handled = true;
                return;
            }

            if (ReferenceEquals(sender, AddIngredientButton))
            {
                IngredientComboBox.Focus();
                e.Handled = true;
                return;
            }

            if (Keyboard.FocusedElement is UIElement currentFocus)
            {
                currentFocus.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }

            e.Handled = true;
        }

        private void AddIngredientButton_Click(object sender, RoutedEventArgs e)
        {
            IngredientComboBox.Focus();
        }
    }
}
