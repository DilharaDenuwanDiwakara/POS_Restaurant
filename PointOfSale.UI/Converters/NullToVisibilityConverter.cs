using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PointOfSale.UI.Converters
{
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Check if the value is "Empty" (Null or Empty String)
            bool isEmpty = value == null || (value is string s && string.IsNullOrWhiteSpace(s));

            // Check if we want to Invert the logic (pass "Inverse" as parameter)
            bool isInverse = parameter != null && parameter.ToString().Equals("Inverse", StringComparison.OrdinalIgnoreCase);

            if (isInverse)
            {
                // Inverse: Show if Null/Empty (Used for "No Image" text)
                return isEmpty ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                // Standard: Hide if Null/Empty (Used for actual Controls)
                return isEmpty ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
