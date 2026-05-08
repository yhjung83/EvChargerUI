using System;
using System.Globalization;
using System.Windows.Data;

namespace EvChargerUI.Commons.Converters
{
    public class EnumEqualsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            return value.Equals(System.Enum.Parse(value.GetType(), parameter.ToString()));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

