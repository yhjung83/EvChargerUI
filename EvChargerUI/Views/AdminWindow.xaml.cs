using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using EvChargerUI.ViewModels;
using EvChargerUI.Services;

namespace EvChargerUI.Views
{
    /// <summary>
    /// SettingWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class AdminWindow : Window
    {
        public AdminViewModel ViewModel => DataContext as AdminViewModel;
        private SystemSettingsService _systemSettingsService;

        public AdminWindow()
        {
            InitializeComponent();

            this.DataContext = new AdminViewModel();
            _systemSettingsService = new SystemSettingsService();

            // Loaded 이벤트 핸들러 등록
            this.Loaded += AdminWindow_Loaded;
            // Closed 이벤트 핸들러 등록
            this.Closed += AdminWindow_Closed;
        }

        /// <summary>
        /// AdminWindow가 로드되었을 때 작업표시줄 활성화
        /// </summary>
        private void AdminWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                bool taskbarEnabled = _systemSettingsService.SetTaskbarEnabled(true);
                var app = (App)Application.Current;
                app.AppLogger.Info($"[AdminWindow] Taskbar enabled when admin window opened: {taskbarEnabled}");
            }
            catch (Exception ex)
            {
                var app = (App)Application.Current;
                app.AppLogger.Error($"[AdminWindow] Failed to enable taskbar on window load: {ex.Message}");
            }
        }

        /// <summary>
        /// AdminWindow가 닫혔을 때 작업표시줄 비활성화
        /// </summary>
        private void AdminWindow_Closed(object sender, EventArgs e)
        {
            try
            {
                var app = (App)Application.Current;
                if (app != null && app.MainView != null)
                {
                    // 관리자 모드 나가기(메인 UI 전환) 시에는 작업표시줄 활성 상태 유지
                    app.AppLogger.Info("[AdminWindow] Keep taskbar enabled on admin exit to main view.");
                    return;
                }

                bool taskbarDisabled = _systemSettingsService.SetTaskbarEnabled(false);
                app?.AppLogger.Info($"[AdminWindow] Taskbar disabled when admin window closed: {taskbarDisabled}");
            }
            catch (Exception ex)
            {
                var app = (App)Application.Current;
                app?.AppLogger.Error($"[AdminWindow] Failed to disable taskbar on window close: {ex.Message}");
            }
        }

        private void TitleBarGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState != MouseButtonState.Pressed)
                return;

            DependencyObject source = e.OriginalSource as DependencyObject;
            while (source != null)
            {
                if (source is Button)
                    return;

                source = VisualTreeHelper.GetParent(source);
            }

            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
            }
        }
    }
}
