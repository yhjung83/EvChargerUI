using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace EvChargerUI.Commons.Controls
{
    public class WatermarkComboBox : ComboBox
    {
        static WatermarkComboBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(WatermarkComboBox),
                new FrameworkPropertyMetadata(typeof(WatermarkComboBox)));
        }

        public static readonly DependencyProperty WatermarkProperty =
            DependencyProperty.Register("Watermark", typeof(string), typeof(WatermarkComboBox), new PropertyMetadata(string.Empty));

        public string Watermark
        {
            get => (string)GetValue(WatermarkProperty);
            set => SetValue(WatermarkProperty, value);
        }
    }
}
