using System;
using System.Diagnostics;
using System.Windows;

namespace EvChargerUI.Commons.Util
{
    /// <summary>
    /// Debug.WriteLine 출력을 디버그 윈도우로 전달하는 TraceListener
    /// </summary>
    public class DebugTraceListener : TraceListener
    {
        private Action<string> _onWrite;

        public DebugTraceListener(Action<string> onWrite)
        {
            _onWrite = onWrite;
        }

        public override void Write(string message)
        {
            if (_onWrite != null)
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    _onWrite(message);
                });
            }
        }

        public override void WriteLine(string message)
        {
            if (_onWrite != null)
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    _onWrite(message + Environment.NewLine);
                });
            }
        }
    }
}

