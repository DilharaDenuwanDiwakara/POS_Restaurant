using System;
using System.Globalization;
using System.Windows.Data;

namespace PointOfSale.UI.Converters
{
    public class PinMaskConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var pin = value as string;
            return string.IsNullOrEmpty(pin) ? string.Empty : "••••";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
