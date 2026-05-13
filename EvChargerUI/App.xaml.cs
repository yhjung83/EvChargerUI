using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using EvChargerUI.Commons.Settings;
using EvChargerUI.Commons.Util;
using EvChargerUI.Models;
using EvChargerUI.Services;
using EvChargerUI.Services.Database;
using EvChargerUI.Views;

namespace EvChargerUI
{
    /// <summary>
    /// App.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class App : Application
    {
        public Charger Charger { get; set; }
        public MainView MainView { get; set; }
        public AdminWindow AdminWindow { get; set; }
        public DebugWindow DebugWindow { get; set; }

        public FileLogger DspLogger { get; private set; }
        public FileLogger AppLogger { get; private set; }
        private LogManager _logManager;
        private DebugTraceListener _debugTraceListener;

        protected override void OnStartup(StartupEventArgs e)
        {
            // 1. 로거 먼저 초기화 (설정 로드 중 로그 기록에 필요)
            AppLogger = new FileLogger(
                directory: Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs"),
                fileNamePrefix: "APP",
                minLevel: LogLevel.Debug,
                rollByDate: true,
                maxBytesPerFile: 10 * 1024 * 1024,
                maxRollFiles: 0
            );

            // 2. SQLite 초기화 (설정 로드 전 수행 - 설정 복원/백업에 사용)
            try
            {
                string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "evcharger.db");
                var sqlite = new SqliteService(dbPath);
                sqlite.Initialize();
                AppLogger.Info($"SQLite initialized at: {dbPath}");
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Failed to initialize SQLite: {ex.Message}");
            }

            // 3. 설정 파일 로드 (로거·DB 초기화 후 수행)
            AppSettingsManager.Load();

            // 4. DspLogSaveYn 설정에 따라 DSP 로거 초기화 (설정 로드 후)
            var dspLogLevel = AppSettingsManager.ChargerOperationSettings.DspLogSaveYn ? LogLevel.Debug : LogLevel.Off;
            DspLogger = new FileLogger(
                directory: Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs"),
                fileNamePrefix: "DSP",
                minLevel: dspLogLevel,
                rollByDate: true,
                maxBytesPerFile: 10 * 1024 * 1024,
                maxRollFiles: 0
            );

            // 5. 작업표시줄 비활성화 설정 적용 (시작 시)
            try
            {
                var systemSettingsService = new SystemSettingsService();
                bool taskbarDisabled = systemSettingsService.SetTaskbarEnabled(false);
                AppLogger.Info($"[Startup] Taskbar disabled on application startup: {taskbarDisabled}");
            }
            catch (Exception ex)
            {
                AppLogger.Error($"[Startup] Failed to disable taskbar on startup: {ex.Message}");
            }

            // 기존 실행 중인 프로세스 종료
            Process currentProcess = Process.GetCurrentProcess();
            string processName = currentProcess.ProcessName;
            
            Process[] existingProcesses = Process.GetProcessesByName(processName);
            foreach (Process process in existingProcesses)
            {
                if (process.Id != currentProcess.Id)
                {
                    try
                    {
                        process.Kill();
                        process.WaitForExit(3000); // 최대 3초 대기
                    }
                    catch (Exception ex)
                    {
                        // 프로세스 종료 실패 시 로그만 남기고 계속 진행
                        AppLogger.Error($"Failed to kill existing process {process.Id}: {ex.Message}");
                    }
                }
            }

            base.OnStartup(e);


            // 1. UI 스레드에서 발생하는 예외 처리 (WPF 메인 루프)
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;

            // 2. 백그라운드 스레드 또는 AppDomain 수준의 예외 처리
            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;

            // 3. Task 내부에서 발생하는 관찰되지 않은 예외 처리 (비동기 처리 시)
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            AppLogger.Info("");
            AppLogger.Info("");
            AppLogger.Info("--------------------------------------------------------");
            AppLogger.Info("EvChargerUI Start...");

            // 로그 관리자 시작
            _logManager = new LogManager(AppLogger);
            _logManager.Start();

            // 시작 시 저장된 밝기/볼륨 설정 적용
            ApplySystemSettingsOnStartup();

            // Charger 설정 확인
            if (!string.IsNullOrEmpty(AppSettingsManager.ChargerSettings.LeftChannelChargerId))
            {
                Charger = new Charger();
                Charger.EvCommInitialize();
                MainView = new MainView();
                MainView.Show();
                return;
            }

            AdminWindow = new AdminWindow();
            AdminWindow.Show();

        }

        public void ShowMainWindow()
        {
            if (string.IsNullOrEmpty(AppSettingsManager.ChargerSettings.LeftChannelChargerId))
            {
                // 충전기 ID 미설정 - 관리자 창으로 유지
                if (AdminWindow == null)
                {
                    AdminWindow = new AdminWindow();
                    AdminWindow.Show();
                }
                return;
            }

            if (Charger == null)
            {
                Charger = new Charger();
                Charger.EvCommInitialize();
            }

            if (MainView == null)
            {
                MainView = new MainView();
                MainView.Show();
            }

            if (AdminWindow != null)
            {
                AdminWindow.Close();
                AdminWindow = null;
            }

            try
            {
                var systemSettingsService = new SystemSettingsService();
                bool taskbarEnabled = systemSettingsService.SetTaskbarEnabled(true);
                AppLogger?.Info($"[App] Taskbar enabled when exiting admin mode: {taskbarEnabled}");
            }
            catch (Exception ex)
            {
                AppLogger?.Error($"[App] Failed to enable taskbar when exiting admin mode: {ex.Message}");
            }

        }

        public void ShowAdminWindow()
        {
            if (AdminWindow == null)
            {
                AdminWindow = new AdminWindow();
                AdminWindow.Show();
            }

            if (MainView != null)
            {
                if (MainView.DataContext is ViewModels.MainViewModel mainVm)
                    mainVm.Dispose();

                MainView.Close();
                MainView = null;
            }


        }

        /// <summary>
        /// 디버그 윈도우 초기화 및 TraceListener 등록
        /// </summary>
        private void InitializeDebugWindow()
        {
            // 이미 DebugWindow가 있고 닫히지 않았다면 다시 표시
            if (DebugWindow != null && !DebugWindow.IsClosed)
            {
                AppLogger.Info("DebugWindow already exists, showing it...");
                if (!DebugWindow.IsVisible)
                {
                    DebugWindow.Show();
                }
                DebugWindow.Activate();
                return;
            }

            AppLogger.Info("Creating new DebugWindow instance...");
            DebugWindow = new DebugWindow();
            
            AppLogger.Info("Showing DebugWindow...");
            DebugWindow.Show();
            AppLogger.Info("DebugWindow.Show() completed");

            // DebugTraceListener가 이미 등록되어 있다면 제거 후 다시 등록
            if (_debugTraceListener != null)
            {
                try
                {
                    Debug.Listeners.Remove(_debugTraceListener);
                }
                catch (Exception ex)
                {
                    AppLogger.Warn($"Failed to remove existing DebugTraceListener: {ex.Message}");
                }
            }

            // DebugTraceListener 생성 및 등록
            AppLogger.Info("Creating DebugTraceListener...");
            _debugTraceListener = new DebugTraceListener((message) =>
            {
                try
                {
                    if (DebugWindow != null && !DebugWindow.IsClosed)
                    {
                        DebugWindow.AppendMessage($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
                    }
                }
                catch (Exception ex)
                {
                    // 디버그 윈도우 자체의 에러는 무시 (무한 루프 방지)
                    System.Diagnostics.Debug.WriteLine($"DebugTraceListener error: {ex.Message}");
                }
            });

            // Debug.Listeners에 추가
            AppLogger.Info("Adding DebugTraceListener to Debug.Listeners...");
            Debug.Listeners.Add(_debugTraceListener);
            Debug.AutoFlush = true;

            AppLogger.Info("Debug window initialized and TraceListener registered successfully");
            
            // 테스트 메시지 출력
            Debug.WriteLine("Debug window test message - If you see this, debug window is working!");
        }

        /// <summary>
        /// 앱 시작 시 시스템 설정(밝기/볼륨) 적용
        /// </summary>
        private void ApplySystemSettingsOnStartup()
        {
            try
            {
                SystemSettingsService systemSettings = new SystemSettingsService();

                // 저장된 밝기 설정 확인
                int dayBrightness = AppSettingsManager.DisplayBrightnessSettings.LevelForDay;
                int nightBrightness = AppSettingsManager.DisplayBrightnessSettings.LevelForNight;

                // 저장된 볼륨 설정 확인
                int dayVolume = AppSettingsManager.SoundVolumeSettings.LevelForDay;
                int nightVolume = AppSettingsManager.SoundVolumeSettings.LevelForNight;

                // 기본값이 설정되어 있으면 적용
                if (dayBrightness > 0 || nightBrightness > 0)
                {
                    AppLogger.Info($"Applying saved brightness settings - Day: {dayBrightness}%, Night: {nightBrightness}%");
                    systemSettings.SetDisplayBrightness(dayBrightness, nightBrightness);
                }

                if (dayVolume > 0 || nightVolume > 0)
                {
                    AppLogger.Info($"Applying saved volume settings - Day: {dayVolume}%, Night: {nightVolume}%");
                    systemSettings.SetSoundVolume(dayVolume, nightVolume);
                }

                AppLogger.Info("System settings applied on startup");
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Failed to apply system settings on startup: {ex.Message}");
            }
        }

        /// <summary>
        /// 종료 원인을 로그에 기록한 후 애플리케이션을 종료합니다.
        /// </summary>
        /// <param name="reason">종료 원인 설명</param>
        public void ShutdownWithReason(string reason)
        {
            AppLogger?.Info($"[SHUTDOWN] 종료 요청. 원인: {reason}");
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            AppLogger?.Info($"[OnExit] 애플리케이션 종료 시작. ExitCode={e.ApplicationExitCode}");

            // 프로그램 종료 시 충전 중인 채널의 세션 저장
            if (Charger != null)
            {
                try
                {
                    Charger.HandleShutdown();
                }
                catch (Exception ex)
                {
                    AppLogger.Error($"Error during shutdown handling: {ex.Message}");
                }
                finally
                {
                    Charger.Dispose();
                    Charger = null;
                }
            }

            if (MainView != null)
            {
                if (MainView.DataContext is ViewModels.MainViewModel mainVm)
                    mainVm.Dispose();

                MainView.Close();
                MainView = null;
            }

            if (AdminWindow != null)
            {
                AdminWindow.Close();
                AdminWindow = null;
            }

            if (DebugWindow != null)
            {
                DebugWindow.Close();
                DebugWindow = null;
            }

            // DebugTraceListener 제거
            if (_debugTraceListener != null)
            {
                Debug.Listeners.Remove(_debugTraceListener);
                _debugTraceListener = null;
            }

            AppLogger.Info("[OnExit] EvChargerUI Exit...");
            
            _logManager?.Dispose();

            if (DspLogger != null)
            {
                DspLogger.Flush();
                DspLogger.Dispose();
                DspLogger = null;
            }
            if (AppLogger != null)
            {
                AppLogger.Flush();
                AppLogger.Dispose();
                AppLogger = null;
            }

            // 작업표시줄 복원 (안전장치)
            try
            {
                var systemSettingsService = new SystemSettingsService();
                bool taskbarRestored = systemSettingsService.SetTaskbarEnabled(true);
                if (AppLogger != null)
                {
                    AppLogger.Info($"[Exit] Taskbar restored on application exit: {taskbarRestored}");
                }
            }
            catch (Exception ex)
            {
                if (AppLogger != null)
                {
                    AppLogger.Error($"[Exit] Failed to restore taskbar on exit: {ex.Message}");
                }
            }

            base.OnExit(e);
        }


        /// <summary>
        /// UI 스레드에서 발생한 예외를 처리하는 핸들러
        /// </summary>
        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            AppLogger?.Fatal($"[CRASH-UI-DISPATCHER] Unhandled exception on UI thread");
            AppLogger?.Fatal($"[CRASH-UI-DISPATCHER] Exception Type: {e.Exception?.GetType().Name ?? "Unknown"}");
            AppLogger?.Fatal($"[CRASH-UI-DISPATCHER] Message: {e.Exception?.Message ?? "No message"}");
            AppLogger?.Fatal($"[CRASH-UI-DISPATCHER] StackTrace: {e.Exception?.StackTrace ?? "No stack trace"}");
            if (e.Exception?.InnerException != null)
            {
                AppLogger?.Fatal($"[CRASH-UI-DISPATCHER] InnerException: {e.Exception.InnerException.Message}");
                AppLogger?.Fatal($"[CRASH-UI-DISPATCHER] InnerException StackTrace: {e.Exception.InnerException.StackTrace}");
            }
            LogAndExit(e.Exception);
        }

        /// <summary>
        /// 백그라운드 스레드에서 발생한 예외를 처리하는 핸들러
        /// </summary>
        private void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            AppLogger?.Fatal($"[CRASH-APPDOMAIN] Unhandled exception in background thread/AppDomain");
            AppLogger?.Fatal($"[CRASH-APPDOMAIN] Exception Type: {ex?.GetType().Name ?? "Unknown"}");
            AppLogger?.Fatal($"[CRASH-APPDOMAIN] Message: {ex?.Message ?? "No message"}");
            AppLogger?.Fatal($"[CRASH-APPDOMAIN] StackTrace: {ex?.StackTrace ?? "No stack trace"}");
            if (ex?.InnerException != null)
            {
                AppLogger?.Fatal($"[CRASH-APPDOMAIN] InnerException: {ex.InnerException.Message}");
                AppLogger?.Fatal($"[CRASH-APPDOMAIN] InnerException StackTrace: {ex.InnerException.StackTrace}");
            }
            AppLogger?.Fatal($"[CRASH-APPDOMAIN] IsTerminating: {e.IsTerminating}");
            LogAndExit(ex);
        }

        /// <summary>
        /// Task(비동기 작업)에서 관찰되지 않은 예외를 처리하는 핸들러
        /// </summary>
        private void TaskScheduler_UnobservedTaskException(object sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
        {
            e.SetObserved();
            AppLogger?.Fatal($"[CRASH-TASK-UNOBSERVED] Unobserved task exception");
            AppLogger?.Fatal($"[CRASH-TASK-UNOBSERVED] Exception Type: {e.Exception?.GetType().Name ?? "Unknown"}");
            AppLogger?.Fatal($"[CRASH-TASK-UNOBSERVED] Message: {e.Exception?.InnerException?.Message ?? e.Exception?.Message ?? "No message"}");
            AppLogger?.Fatal($"[CRASH-TASK-UNOBSERVED] StackTrace: {e.Exception?.InnerException?.StackTrace ?? e.Exception?.StackTrace ?? "No stack trace"}");
            LogAndExit(e.Exception);
        }

        /// <summary>
        /// 예외 정보를 로그에 기록하고 사용자에게 알린 후 애플리케이션을 종료하는 메서드
        /// </summary>
        /// <param name="ex">발생한 예외 객체</param>
        private void LogAndExit(Exception ex)
        {
            try
            {
                string msg = (ex != null) ? ex.ToString() : "알 수 없는 오류";

                if (AppLogger != null)
                {
                    AppLogger.Fatal($"[FATAL] Application terminating due to unhandled exception");
                    AppLogger.Error("Program Error", ex);
                    // 비동기 큐 기반 로거이므로 백그라운드 워커가 큐를 처리할 시간 확보
                    Thread.Sleep(1000);
                    AppLogger.Flush();
                    AppLogger.Dispose();
                    AppLogger = null;
                }

                if (DspLogger != null)
                {
                    DspLogger.Flush();
                    DspLogger.Dispose();
                    DspLogger = null;
                }
            }
            catch (Exception logEx)
            {
                // 로깅 자체 실패 시도 무시
                System.Diagnostics.Debug.WriteLine($"Failed to log exception: {logEx.Message}");
            }

            System.Environment.Exit(1);
        }

    }
}
