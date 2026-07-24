using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PointOfSale.UI.Converters
{
    public class MarginColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush HighMarginBrush =
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32"));
        private static readonly SolidColorBrush MediumMarginBrush =
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF6C00"));
        private static readonly SolidColorBrush LowMarginBrush =
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C62828"));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            decimal marginPercent;

            switch (value)
            {
                case decimal dec:
                    marginPercent = dec;
                    break;
                case double dbl:
                    marginPercent = (decimal)dbl;
                    break;
                default:
                    return LowMarginBrush;
            }

            if (marginPercent >= 60m) return HighMarginBrush;
            if (marginPercent >= 25m) return MediumMarginBrush;
            return LowMarginBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
