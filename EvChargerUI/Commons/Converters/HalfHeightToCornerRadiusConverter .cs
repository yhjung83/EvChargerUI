using System.Globalization;
using System.Windows.Data;
using System.Windows;
using System;

namespace EvChargerUI.Commons.Converters
{
    public class HalfHeightToCornerRadiusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double height)
                return new CornerRadius(height / 2);
            return new CornerRadius(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}