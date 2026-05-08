using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EvChargerUI.Commons.Enum;
using System.Windows.Media.Imaging;
using System.Windows.Data;

namespace EvChargerUI.Commons.Converters
{
    public class ChargeSequenceToArrowImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ChargeSequence sequence && parameter is string)
            {
                var paramEnum = System.Enum.Parse(typeof(ChargeSequence),(string) parameter);

                var imagePath = sequence.CompareTo(paramEnum) >= 0
                    ? "/Images/arrow.png"
                    : "/Images/arrow_disable.png";

                return new BitmapImage(new Uri(imagePath, UriKind.Relative));
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
