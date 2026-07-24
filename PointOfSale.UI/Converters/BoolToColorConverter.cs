using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PointOfSale.UI.Converters
{
    public class BoolToColorConverter : IValueConverter
    {
        private readonly SolidColorBrush _activeBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F5E9")); // Light Green
        private readonly SolidColorBrush _inactiveBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFEBEE")); // Light Red

        // Optional: Text colors if you want to use this for foregrounds too
        // private readonly SolidColorBrush _activeText = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32")); 
        // private readonly SolidColorBrush _inactiveText = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C62828")); 

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isActive && isActive)
            {
                return _activeBrush;
            }
            return _inactiveBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
