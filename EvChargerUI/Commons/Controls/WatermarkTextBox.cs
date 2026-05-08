using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EvChargerUI.Commons.Controls
{
    public class WatermarkTextBox : TextBox
    {
        static WatermarkTextBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(WatermarkTextBox),
                new FrameworkPropertyMetadata(typeof(WatermarkTextBox)));
        }

        public static readonly DependencyProperty WatermarkProperty =
            DependencyProperty.Register("Watermark", typeof(string), typeof(WatermarkTextBox), new PropertyMetadata(string.Empty));

        public string Watermark
        {
            get => (string)GetValue(WatermarkProperty);
            set => SetValue(WatermarkProperty, value);
        }

        public static readonly DependencyProperty LetterSpacingProperty =
            DependencyProperty.Register("LetterSpacing", typeof(double), typeof(WatermarkTextBox), new PropertyMetadata(0.0));

        public double LetterSpacing
        {
            get => (double)GetValue(LetterSpacingProperty);
            set => SetValue(LetterSpacingProperty, value);
        }

        public static readonly DependencyProperty WatermarkForegroundProperty =
            DependencyProperty.Register("WatermarkForeground", typeof(Brush), typeof(WatermarkTextBox), new PropertyMetadata(Brushes.Gray));

        public Brush WatermarkForeground
        {
            get => (Brush)GetValue(WatermarkForegroundProperty);
            set => SetValue(WatermarkForegroundProperty, value);
        }
    }
}