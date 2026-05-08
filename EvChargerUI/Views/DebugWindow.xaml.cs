using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace EvChargerUI.Views
{
    /// <summary>
    /// DebugWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class DebugWindow : Window
    {
        private const int MAX_LINES = 10000; // 최대 라인 수 제한
        private bool _autoScrollEnabled = true;
        private bool _isClosed = false;

        public bool IsClosed => _isClosed;

        public DebugWindow()
        {
            InitializeComponent();
            
            // 초기 투명도 설정 (InitializeComponent 이후에 설정)
            this.Opacity = 0.9;
            if (OpacitySlider != null)
            {
                OpacitySlider.Value = 0.9;
            }
            if (OpacityValueText != null)
            {
                UpdateOpacityText(0.9);
            }
        }
        
        private void UpdateOpacityText(double value)
        {
            if (OpacityValueText != null)
            {
                OpacityValueText.Text = $"{(int)(value * 100)}%";
            }
        }
        
        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (OpacitySlider != null)
            {
                // Window의 Opacity를 조절하여 실제 투명도 적용
                this.Opacity = OpacitySlider.Value;
                UpdateOpacityText(OpacitySlider.Value);
            }
        }

        /// <summary>
        /// 디버그 메시지 추가
        /// </summary>
        public void AppendMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            try
            {
                // UI 스레드에서 실행
                Dispatcher.Invoke(() =>
                {
                    DebugTextBox.AppendText(message);
                    
                    // 라인 수 제한 (메모리 관리)
                    var lines = DebugTextBox.Text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length > MAX_LINES)
                    {
                        var keepLines = string.Join(Environment.NewLine, lines.Skip(lines.Length - MAX_LINES / 2));
                        DebugTextBox.Text = keepLines + Environment.NewLine;
                    }
                    
                    // 자동 스크롤
                    if (_autoScrollEnabled)
                    {
                        DebugTextBox.ScrollToEnd();
                    }
                });
            }
            catch (Exception ex)
            {
                // 에러 발생 시 무시 (디버그 윈도우 자체의 에러는 로그하지 않음)
                System.Diagnostics.Debug.WriteLine($"DebugWindow.AppendMessage error: {ex.Message}");
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            DebugTextBox.Clear();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        
        private void Header_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // 헤더를 드래그하여 창 이동
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                this.DragMove();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _isClosed = true;
            base.OnClosed(e);
        }
    }
}

