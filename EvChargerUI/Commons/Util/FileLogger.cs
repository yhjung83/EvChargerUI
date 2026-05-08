using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EvChargerUI.Commons.Util
{
    public enum LogLevel
    {
        Trace = 0,
        Debug = 1,
        Info = 2,
        Warn = 3,
        Error = 4,
        Fatal = 5,
        Off = 6
    }

    /// <summary>
    /// 비동기 파일 로거
    /// - 쓰레드 안전
    /// - 로그 레벨 필터
    /// - 날짜별/크기별 롤링
    /// - 자동 디렉터리 생성
    /// - 강제 Flush/Dispose 지원
    /// </summary>
    public sealed class FileLogger : IDisposable
    {
        private readonly string _directory;
        private readonly string _fileNamePrefix;
        private readonly string _fileExtension;
        private readonly long _maxBytesPerFile;
        private readonly int _maxRollFiles;            // 크기 롤링 시 최대 보관 개수(0이면 무제한)
        private readonly bool _rollByDate;             // 날짜별 파일 분리 여부(일 단위)
        private readonly Encoding _encoding;
        private readonly LogLevel _minLevel;

        private readonly BlockingCollection<string> _queue;
        private readonly CancellationTokenSource _cts;
        private readonly Task _worker;

        private DateTime _currentDate;                 // yyyy-MM-dd 기준
        private FileStream _stream;
        private StreamWriter _writer;
        private string _currentPath;
        private long _writtenBytes;

        // 기본값:
        // - 파일명: {prefix}_yyyy-MM-dd.log (rollByDate=true)
        // - 크기 롤링: 10MB, 보관 5개
        public FileLogger(
            string directory,
            string fileNamePrefix = "app",
            LogLevel minLevel = LogLevel.Info,
            bool rollByDate = true,
            long maxBytesPerFile = 10 * 1024 * 1024, // 10MB
            int maxRollFiles = 5,
            string fileExtension = ".log",
            Encoding encoding = null)
        {
            _directory = directory ?? ".";
            _fileNamePrefix = string.IsNullOrWhiteSpace(fileNamePrefix) ? "app" : fileNamePrefix;
            _fileExtension = string.IsNullOrWhiteSpace(fileExtension) ? ".log" : fileExtension;
            _maxBytesPerFile = maxBytesPerFile <= 0 ? long.MaxValue : maxBytesPerFile;
            _maxRollFiles = maxRollFiles < 0 ? 0 : maxRollFiles;
            _rollByDate = rollByDate;
            _encoding = encoding ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            _minLevel = minLevel;

            Directory.CreateDirectory(_directory);

            _queue = new BlockingCollection<string>(boundedCapacity: 10000);
            _cts = new CancellationTokenSource();

            // 초기 파일 오픈
            _currentDate = DateTime.Now.Date;
            OpenWriter(rollIndex: 0);

            // 백그라운드 쓰레드
            _worker = Task.Factory.StartNew(WorkerLoop, TaskCreationOptions.LongRunning);
        }

        // 퍼블릭 API ----------------------------------------------------------

        public void Log(LogLevel level, string message, Exception ex = null)
        {
            if (level < _minLevel || level == LogLevel.Off) return;

            var now = DateTime.Now;
            // 예: 2025-08-26 18:11:22.123 [INFO] (TID:1) 메시지
            var sb = new StringBuilder(256);
            sb.Append(now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture))
              .Append(" [").Append(level.ToString().ToUpperInvariant()).Append("]")
              .Append(" (TID:").Append(Thread.CurrentThread.ManagedThreadId).Append(") ")
              .Append(message ?? string.Empty);

            if (ex != null)
            {
                sb.AppendLine()
                  .Append(ex.ToString());
            }

            Enqueue(sb.ToString());
        }

        public void Trace(string msg) => Log(LogLevel.Trace, msg);
        public void Debug(string msg) => Log(LogLevel.Debug, msg);
        public void Info(string msg) => Log(LogLevel.Info, msg);
        public void Warn(string msg) => Log(LogLevel.Warn, msg);
        public void Error(string msg, Exception ex = null) => Log(LogLevel.Error, msg, ex);
        public void Fatal(string msg, Exception ex = null) => Log(LogLevel.Fatal, msg, ex);

        /// <summary>강제로 버퍼를 비우고 디스크에 기록</summary>
        public void Flush()
        {
            try
            {
                lock (this)
                {
                    _writer?.Flush();
                    _stream?.Flush(true);
                }
            }
            catch { /* ignore */ }
        }

        // 내부 구현 -----------------------------------------------------------

        private void Enqueue(string line)
        {
            // 큐가 가득 차면 살짝 버리거나(옵션화 가능) 대기
            // 여기서는 생산자 대기로 둠
            try
            {
                _queue.Add(line, _cts.Token);
            }
            catch (OperationCanceledException) { /* shutting down */ }
            catch (Exception) { /* ignore */ }
        }

        private void WorkerLoop()
        {
            try
            {
                foreach (var line in _queue.GetConsumingEnumerable(_cts.Token))
                {
                    try
                    {
                        RollIfNeeded();
                        WriteLine(line);
                    }
                    catch (Exception e)
                    {
                        // 파일 기록 중 에러 → Debug 출력
                        Console.WriteLine("[FileLogger] Write error: " + e);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 정상 종료 루트
            }
            finally
            {
                // 잔여 플러시
                try
                {
                    lock (this)
                    {
                        _writer?.Flush();
                        _stream?.Flush(true);
                        _writer?.Dispose();
                        _stream?.Dispose();
                        _writer = null;
                        _stream = null;
                    }
                }
                catch { /* ignore */ }
            }
        }

        private void RollIfNeeded()
        {
            var today = DateTime.Now.Date;

            // 날짜 롤링
            if (_rollByDate && today != _currentDate)
            {
                _currentDate = today;
                OpenWriter(rollIndex: 0);
                return;
            }

            // 크기 롤링
            if (_writtenBytes >= _maxBytesPerFile)
            {
                // {prefix}_yyyy-MM-dd.log → {prefix}_yyyy-MM-dd.1.log … 식으로 순환
                var nextIndex = GetNextRollIndex(_currentPath);
                OpenWriter(nextIndex);
                CleanupOldRollsIfNeeded();
            }
        }

        private void WriteLine(string line)
        {
            var bytes = _encoding.GetByteCount(line) + _encoding.GetByteCount(Environment.NewLine);
            lock (this)
            {
                _writer.WriteLine(line);
                _writtenBytes += bytes;
            }
        }

        private void OpenWriter(int rollIndex)
        {
            lock (this)
            {
                try
                {
                    _writer?.Flush();
                    _stream?.Flush(true);
                }
                catch { /* ignore */ }

                _writer?.Dispose();
                _stream?.Dispose();

                var fileName = BuildFileName(_currentDate, rollIndex);
                _currentPath = Path.Combine(_directory, fileName);

                // Append 모드
                _stream = new FileStream(_currentPath, FileMode.Append, FileAccess.Write, FileShare.Read);
                _writer = new StreamWriter(_stream, _encoding) { AutoFlush = true };
                _writtenBytes = _stream.Length;
            }
        }

        private string BuildFileName(DateTime date, int rollIndex)
        {
            // 기본: app_2025-08-26.log
            // 롤링: app_2025-08-26.1.log
            var datePart = _rollByDate ? "_" + date.ToString("yyyy-MM-dd") : string.Empty;
            if (rollIndex <= 0)
                return $"{_fileNamePrefix}{datePart}{_fileExtension}";
            return $"{_fileNamePrefix}{datePart}.{rollIndex}{_fileExtension}";
        }

        private int GetNextRollIndex(string currentPath)
        {
            var file = Path.GetFileName(currentPath);
            // 파일명에서 마지막 ".{n}.log" 패턴 추출
            var stem = Path.GetFileNameWithoutExtension(file); // e.g., app_2025-08-26 or app_2025-08-26.1
            int lastDot = stem.LastIndexOf('.');
            if (lastDot >= 0)
            {
                int n;
                if (int.TryParse(stem.Substring(lastDot + 1), out n))
                    return n + 1;
            }
            return 1;
        }

        private void CleanupOldRollsIfNeeded()
        {
            if (_maxRollFiles == 0) return; // 무제한 보관

            try
            {
                var pattern = $"{_fileNamePrefix}_{_currentDate:yyyy-MM-dd}.*{_fileExtension}".Replace("\\", "");
                var files = new DirectoryInfo(_directory).GetFiles(pattern);
                // "{prefix}_yyyy-MM-dd.{n}.log"만 정렬
                Array.Sort(files, (a, b) => a.CreationTimeUtc.CompareTo(b.CreationTimeUtc));
                var overflow = files.Length - _maxRollFiles;
                for (int i = 0; i < overflow; i++)
                {
                    try { files[i].Delete(); } catch { /* ignore */ }
                }
            }
            catch { /* ignore */ }
        }

        public void Dispose()
        {
            // 생산 종료
            try { _queue.CompleteAdding(); } catch { /* ignore */ }
            try { _cts.Cancel(); } catch { /* ignore */ }

            try { _worker.Wait(2000); } catch { /* ignore */ }

            _cts.Dispose();
            _queue.Dispose();
        }
    }
}
