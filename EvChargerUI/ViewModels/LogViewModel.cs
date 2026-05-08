using EvChargerUI.ViewModels.Commons;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Input;
using System.Windows.Threading;

namespace EvChargerUI.ViewModels
{
    public class LogViewModel : BaseViewModel
    {
        private string _appLogContent;
        private string _dspLogContent;
        private Action _goBackAction;
        private DispatcherTimer _logUpdateTimer;
        private string _activeLogType;

        public string AppLogContent
        {
            get => _appLogContent;
            set
            {
                _appLogContent = value;
                OnPropertyChanged(nameof(AppLogContent));
            }
        }

        public string DspLogContent
        {
            get => _dspLogContent;
            set
            {
                _dspLogContent = value;
                OnPropertyChanged(nameof(DspLogContent));
            }
        }

        public ICommand ShowAppLogCommand { get; }
        public ICommand ShowDspLogCommand { get; }
        public ICommand ClearLogsCommand { get; }
        public ICommand BackCommand { get; }

        public LogViewModel(Action goBackAction)
        {
            _goBackAction = goBackAction;

            ShowAppLogCommand = new RelayCommand(ShowAppLog);
            ShowDspLogCommand = new RelayCommand(ShowDspLog);
            ClearLogsCommand = new RelayCommand(ClearLogs);
            BackCommand = new RelayCommand(GoBack);

            _logUpdateTimer = new DispatcherTimer();
            _logUpdateTimer.Interval = TimeSpan.FromSeconds(1);
            _logUpdateTimer.Tick += LogUpdateTimer_Tick;
        }

        private void ShowAppLog(object obj)
        {
            _activeLogType = "APP";
            AppLogContent = ReadLatestLogFile("APP");
            _logUpdateTimer.Start();
        }

        private void ShowDspLog(object obj)
        {
            _activeLogType = "DSP";
            DspLogContent = ReadLatestLogFile("DSP");
            _logUpdateTimer.Start();
        }

        private void ClearLogs(object obj)
        {
            _logUpdateTimer.Stop();
            _activeLogType = string.Empty;
            AppLogContent = string.Empty;
            DspLogContent = string.Empty;
        }

        private void GoBack(object obj)
        {
            _logUpdateTimer.Stop();
            _activeLogType = string.Empty;
            _goBackAction?.Invoke();
        }

        private void LogUpdateTimer_Tick(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_activeLogType))
            {
                if (_activeLogType == "APP")
                {
                    AppLogContent = ReadLatestLogFile("APP");
                }
                else if (_activeLogType == "DSP")
                {
                    DspLogContent = ReadLatestLogFile("DSP");
                }
            }
        }

        private string ReadLatestLogFile(string logType)
        {
            string logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            if (!Directory.Exists(logDirectory))
            {
                return $"Error: Log directory '{logDirectory}' not found.";
            }

            string searchPattern = $"{logType}_*.log";
            var logFiles = Directory.GetFiles(logDirectory, searchPattern)
                                    .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                                    .ToList();

            if (!logFiles.Any())
            {
                return $"No {logType} log files found in '{logDirectory}'.";
            }

            try
            {
                const int maxLines = 1000;
                // Use FileStream with FileShare.ReadWrite to allow other processes to write to the file
                using (FileStream fs = new FileStream(logFiles.First(), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (StreamReader reader = new StreamReader(fs))
                {
                    List<string> allLines = new List<string>();
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        allLines.Add(line);
                    }
                    // Take the last maxLines and then reverse them to show latest at top
                    return string.Join(Environment.NewLine, allLines.Skip(Math.Max(0, allLines.Count() - maxLines)).Reverse());
                }
            }
            catch (Exception ex)
            {
                return $"Error reading log file: {ex.Message}";
            }
        }
    }
}
