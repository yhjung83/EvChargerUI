using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EvChargerUI.Commons.Settings;
using EvChargerUI.Commons.Util;

namespace EvChargerUI.Services
{
    public class LogManager : IDisposable
    {
        private readonly FileLogger _logger;
        private Timer _timer;
        private readonly string _logDirectory;
        private readonly int _retentionDays;

        public LogManager(FileLogger logger)
        {
            _logger = logger;
            _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            _retentionDays = AppSettingsManager.ChargerOperationSettings.LogRetentionDays;
        }

        public void Start()
        {
            _logger.Info($"LogManager started. Log retention: {_retentionDays} days.");
            ScheduleNextRun();
        }

        private void ScheduleNextRun()
        {
            var now = DateTime.Now;
            var nextRun = now.Date.AddDays(1); // Next midnight
            var dueTime = nextRun - now;

            _logger.Info($"LogManager: Next cleanup scheduled for {nextRun} (in {dueTime.TotalHours:F1} hours).");

            _timer = new Timer(async _ =>
            {
                await ExecuteAsync();
                ScheduleNextRun(); // Reschedule for the next day
            }, null, dueTime, Timeout.InfiniteTimeSpan);
        }

        private async Task ExecuteAsync()
        {
            _logger.Info("LogManager: Starting daily log archival and cleanup...");
            try
            {
                await ArchiveYesterdayLogsAsync();
                await CleanupOldArchivesAsync();
                _logger.Info("LogManager: Daily log archival and cleanup finished.");
            }
            catch (Exception ex)
            {
                _logger.Error("LogManager: An error occurred during log archival/cleanup.", ex);
            }
        }

        private async Task ArchiveYesterdayLogsAsync()
        {
            var yesterday = DateTime.Now.AddDays(-1);
            var dateStr = yesterday.ToString("yyyy-MM-dd");
            _logger.Info($"LogManager: Archiving logs for {dateStr}.");

            var sourceFilePattern = $"*_{dateStr}*.log"; // e.g., APP_2025-11-17.log, DSP_2025-11-17.1.log
            var sourcePath = Path.Combine(_logDirectory, sourceFilePattern);
            
            var destArchiveName = $"logs_{dateStr}.zip";
            var destArchivePath = Path.Combine(_logDirectory, destArchiveName);

            if (!Directory.EnumerateFiles(_logDirectory, sourceFilePattern).Any())
            {
                _logger.Info($"LogManager: No log files found for {dateStr}. Nothing to archive.");
                return;
            }

            if (File.Exists(destArchivePath))
            {
                _logger.Warn($"LogManager: Archive '{destArchiveName}' already exists. Skipping archival.");
                return;
            }

            // 1. Compress files
            var compressCommand = $"powershell.exe -NoProfile -Command \"Compress-Archive -Path '{sourcePath}' -DestinationPath '{destArchivePath}' -Force\"";
            _logger.Info($"LogManager: Running compression: {compressCommand}");
            await RunShellCommandAsync(compressCommand);

            // 2. Verify and delete source files
            if (File.Exists(destArchivePath))
            {
                _logger.Info($"LogManager: Archive '{destArchiveName}' created successfully.");
                var deleteCommand = $"cmd.exe /c del \"{sourcePath}\"";
                _logger.Info($"LogManager: Running deletion: {deleteCommand}");
                await RunShellCommandAsync(deleteCommand);
                _logger.Info($"LogManager: Deleted source log files for {dateStr}.");
            }
            else
            {
                _logger.Error($"LogManager: Failed to create archive '{destArchiveName}'. Source files were not deleted.");
            }
        }

        private async Task RunShellCommandAsync(string command)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {command}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            process.Start();
            string stdout = await process.StandardOutput.ReadToEndAsync();
            string stderr = await process.StandardError.ReadToEndAsync();
            await Task.Run(() => process.WaitForExit()); // Use Task.Run to avoid blocking on WaitForExit

            if (process.ExitCode != 0)
            {
                _logger.Error($"LogManager: Shell command failed with exit code {process.ExitCode}.");
                if (!string.IsNullOrWhiteSpace(stdout)) _logger.Error($"--> STDOUT: {stdout.Trim()}");
                if (!string.IsNullOrWhiteSpace(stderr)) _logger.Error($"--> STDERR: {stderr.Trim()}");
            }
        }

        private async Task CleanupOldArchivesAsync()
        {
            if (_retentionDays <= 0)
            {
                _logger.Info("LogManager: Log retention is disabled. Skipping cleanup of old archives.");
                return;
            }

            _logger.Info($"LogManager: Cleaning up archives older than {_retentionDays} days.");
            var today = DateTime.Now.Date;
            var archiveFiles = Directory.EnumerateFiles(_logDirectory, "logs_*.zip");

            foreach (var file in archiveFiles)
            {
                try
                {
                    var fileName = Path.GetFileNameWithoutExtension(file); // "logs_2025-11-17"
                    var datePart = fileName.Substring("logs_".Length);     // "2025-11-17"
                    if (DateTime.TryParse(datePart, out var archiveDate))
                    {
                        if ((today - archiveDate).TotalDays > _retentionDays)
                        {
                            _logger.Info($"LogManager: Deleting old archive '{Path.GetFileName(file)}' as it is older than {_retentionDays} days.");
                            var deleteCommand = $"cmd.exe /c del \"{file}\"";
                            await RunShellCommandAsync(deleteCommand);
                        }
                    }
                    else
                    {
                        _logger.Warn($"LogManager: Could not parse date from archive filename: '{Path.GetFileName(file)}'.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"LogManager: Error processing archive file '{Path.GetFileName(file)}'.", ex);
                }
            }
            _logger.Info("LogManager: Finished cleaning up old archives.");
        }

        public void Dispose()
        {
            _timer?.Dispose();
            _logger.Info("LogManager stopped.");
        }
    }
}
