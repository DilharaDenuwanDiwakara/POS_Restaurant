using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using PointOfSale.UI.ViewModels.Inventory;

namespace PointOfSale.UI.Converters
{
    public class BatchContextToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is BatchSelectionContext context) || parameter == null)
            {
                // Default visibility to Visible if context or parameter is missing
                return Visibility.Visible;
            }

            string columnName = parameter.ToString();

            switch (columnName)
            {
                case "Unit Cost":
                    // Unit Cost is visible ONLY for SupplierReturn.
                    return context == BatchSelectionContext.SupplierReturn
                           ? Visibility.Visible
                           : Visibility.Hidden;

                case "Selling Price":
                    // Selling Price is visible for BOTH Sales and SupplierReturn.
                    return Visibility.Visible;

                default:
                    // Any other column (if added later) defaults to Visible.
                    return Visibility.Visible;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
