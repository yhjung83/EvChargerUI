using System;
using System.Globalization;
using System.Windows.Data;
using EvChargerUI.Commons.Enum;

namespace EvChargerUI.Commons.Converters
{
    public class EnumToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;


            return value.GetHashCode() >= System.Enum.Parse(value.GetType(), parameter.ToString()).GetHashCode();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ((bool)value)
                return System.Enum.Parse(targetType, parameter.ToString());
            return Binding.DoNothing;
        }
    }
}
