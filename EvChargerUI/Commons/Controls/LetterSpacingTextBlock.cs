using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;

namespace EvChargerUI.Commons.Controls
{
    public class LetterSpacingTextBlock : TextBlock
    {
        public static readonly DependencyProperty LetterSpacingProperty =
            DependencyProperty.Register(
                nameof(LetterSpacing),
                typeof(double),
                typeof(LetterSpacingTextBlock),
                new PropertyMetadata(0.0, OnLetterSpacingChanged));

        public double LetterSpacing
        {
            get { return (double)GetValue(LetterSpacingProperty); }
            set { SetValue(LetterSpacingProperty, value); }
        }

        private static void OnLetterSpacingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var textBlock = d as LetterSpacingTextBlock;
            if (textBlock != null)
            {
                textBlock.UpdateLetterSpacing();
            }
        }

        private void UpdateLetterSpacing()
        {
            if (string.IsNullOrEmpty(this.Text))
                return;

            var effects = new TextEffectCollection();
            double spacing = LetterSpacing;

            for (int i = 1; i < this.Text.Length; i++) // 첫 글자는 이동하지 않음
            {
                var effect = new TextEffect
                {
                    PositionStart = i,
                    PositionCount = 1,
                    Transform = new TranslateTransform(spacing * i, 0)
                };
                effects.Add(effect);
            }

            this.TextEffects = effects;
        }


    }
}