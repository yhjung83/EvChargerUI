using System.Globalization;
using System.Windows;
using System;
using System.Windows.Data;

namespace EvChargerUI.Commons.Converters
{
    public class NullToVisibilityConverter : IValueConverter
    {
        public bool Inverse { get; set; } = false;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isVisible = value == null;
            if (Inverse) isVisible = !isVisible;

            return isVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}