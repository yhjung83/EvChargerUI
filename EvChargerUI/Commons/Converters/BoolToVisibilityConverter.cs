using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows;

namespace EvChargerUI.Commons.Converters
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public bool Inverse { get; set; } = false;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isVisible = (bool) value;
            if (Inverse) isVisible = !isVisible;

            return isVisible? Visibility.Visible : Visibility.Collapsed;

        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool result = (Visibility)value == Visibility.Visible;
            if (Inverse) result = !result;

            return result;
        }
    }
}
