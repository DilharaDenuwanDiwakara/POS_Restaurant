using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PointOfSale.UI.Converters
{
    public class YieldPercentageBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || value == System.DBNull.Value)
            {
                return Brushes.Red;
            }

            decimal yieldPercentage;
            if (!decimal.TryParse(value.ToString(), NumberStyles.Any, culture, out yieldPercentage))
            {
                return Brushes.Red;
            }

            return yieldPercentage >= 85.00m ? Brushes.Green : Brushes.Red;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
