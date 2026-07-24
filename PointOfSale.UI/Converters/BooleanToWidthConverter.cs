using System;
using System.Globalization;
using System.Windows.Data;

namespace PointOfSale.UI.Converters
{
    public class BooleanToWidthConverter : IValueConverter
    {
        public double CollapsedWidth { get; set; } = 60;
        public double ExpandedWidth { get; set; } = 250;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isExpanded)
            {
                return isExpanded ? ExpandedWidth : CollapsedWidth;
            }
            return CollapsedWidth;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
