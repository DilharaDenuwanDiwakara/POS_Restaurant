using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PointOfSale.UI.Converters
{
    public class ColorShadeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SolidColorBrush brush)
            {
                // Parse the parameter (e.g., "0.2" for light, "-0.2" for dark)
                if (double.TryParse(parameter?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double factor))
                {
                    Color color = brush.Color;
                    return new SolidColorBrush(ChangeColorBrightness(color, factor));
                }
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private Color ChangeColorBrightness(Color color, double correctionFactor)
        {
            float red = (float)color.R;
            float green = (float)color.G;
            float blue = (float)color.B;

            if (correctionFactor < 0) // Darken
            {
                correctionFactor = 1 + correctionFactor;
                red *= (float)correctionFactor;
                green *= (float)correctionFactor;
                blue *= (float)correctionFactor;
            }
            else // Lighten
            {
                red = (255 - red) * (float)correctionFactor + red;
                green = (255 - green) * (float)correctionFactor + green;
                blue = (255 - blue) * (float)correctionFactor + blue;
            }

            return Color.FromArgb(color.A, (byte)red, (byte)green, (byte)blue);
        }
    }
}
