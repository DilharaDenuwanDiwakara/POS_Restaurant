using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PointOfSale.UI.Converters
{
    public class LevelToMarginConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int level = (int)value;
            // Indent 15 pixels per level
            return new Thickness(level * 15, 0, 0, 0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
