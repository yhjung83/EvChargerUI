using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace EvChargerUI.Commons.Converters
{
    internal class ProgressToHeightConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 2 ||
                !(values[0] is int progress) ||
                !(values[1] is double containerHeight))
                return 0;

            // 0~100 비율로 계산하여 높이 반환
            return (progress / 100.0) * containerHeight;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
