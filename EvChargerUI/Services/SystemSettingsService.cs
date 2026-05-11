using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Management;
using System.Windows;
using EvChargerUI.Commons.Settings;
using EvChargerUI.Commons.Util;
using System.Globalization;

namespace EvChargerUI.Services
{
    public class SystemSettingsService : ISystemSettingsService
    {
        private readonly FileLogger _logger = (Application.Current as App)?.AppLogger;
        #region Windows API - Audio (Core Audio)

        [ComImport]
        [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumerator { }

        // eRender=0, eCapture=1, eAll=2
        private const int EDataFlow_eRender = 0;
        // eConsole=0, eMultimedia=1, eCommunications=2
        private const int ERole_eMultimedia = 1;

        private const int CLSCTX_INPROC_SERVER = 0x1;

        [ComImport]
        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            // HRESULT EnumAudioEndpoints(EDataFlow dataFlow, DWORD dwStateMask, IMMDeviceCollection** ppDevices);
            int EnumAudioEndpoints(int dataFlow, int dwStateMask, out object ppDevices);

            // HRESULT GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, IMMDevice** ppEndpoint);
            [PreserveSig]
            int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);

            // HRESULT GetDevice(LPCWSTR pwstrId, IMMDevice** ppDevice);
            int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice ppDevice);

            // HRESULT RegisterEndpointNotificationCallback(IMMNotificationClient* pClient);
            int RegisterEndpointNotificationCallback(IntPtr pClient);

            // HRESULT UnregisterEndpointNotificationCallback(IMMNotificationClient* pClient);
            int UnregisterEndpointNotificationCallback(IntPtr pClient);
        }

        [ComImport]
        [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            // HRESULT Activate(REFIID iid, DWORD dwClsCtx, PROPVARIANT* pActivationParams, void** ppInterface);
            [PreserveSig]
            int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams,
                         [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);

            // HRESULT OpenPropertyStore(DWORD stgmAccess, IPropertyStore** ppProperties);
            int OpenPropertyStore(int stgmAccess, out IntPtr ppProperties);

            // HRESULT GetId(LPWSTR* ppstrId);
            int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);

            // HRESULT GetState(DWORD* pdwState);
            int GetState(out int pdwState);
        }

        [ComImport]
        [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolume
        {
            int RegisterControlChangeNotify(IntPtr pNotify);
            int UnregisterControlChangeNotify(IntPtr pNotify);
            int GetChannelCount(out int pnChannelCount);

            [PreserveSig]
            int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
            [PreserveSig]
            int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
            [PreserveSig]
            int GetMasterVolumeLevel(out float pfLevelDB);
            [PreserveSig]
            int GetMasterVolumeLevelScalar(out float pfLevel);
        }

        #endregion

        #region Windows API - Display Brightness (Dxva2 & User32)

        [DllImport("dxva2.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(
            IntPtr hMonitor, out uint pdwNumberOfPhysicalMonitors);

        [DllImport("dxva2.dll", EntryPoint = "GetPhysicalMonitorsFromHMONITOR", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetPhysicalMonitorsFromHMONITOR(
            IntPtr hMonitor, uint dwPhysicalMonitorArraySize,
            [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

        [DllImport("dxva2.dll", EntryPoint = "DestroyPhysicalMonitors", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyPhysicalMonitors(
            uint dwPhysicalMonitorArraySize, [In] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

        [DllImport("dxva2.dll", EntryPoint = "GetMonitorBrightness", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorBrightness(
            IntPtr hPhysicalMonitor,
            out uint pdwMinimumBrightness,
            out uint pdwCurrentBrightness,
            out uint pdwMaximumBrightness);

        [DllImport("dxva2.dll", EntryPoint = "SetMonitorBrightness", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetMonitorBrightness(IntPtr hPhysicalMonitor, uint dwNewBrightness);

        [DllImport("dxva2.dll", EntryPoint = "GetMonitorCapabilities", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorCapabilities(
            IntPtr hPhysicalMonitor,
            out uint pdwMonitorCapabilities,
            out uint pdwSupportedColorTemperatures);

        [DllImport("user32.dll", EntryPoint = "EnumDisplayMonitors", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumDisplayMonitors(
            IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);

        private delegate bool MonitorEnumDelegate(
            IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct PHYSICAL_MONITOR
        {
            public IntPtr hPhysicalMonitor;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szPhysicalMonitorDescription;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        private const int MC_CAPS_BRIGHTNESS = 0x00000001;

        #endregion

        #region Windows API - System Time (kernel32)

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEMTIME
        {
            public ushort wYear;
            public ushort wMonth;
            public ushort wDayOfWeek;
            public ushort wDay;
            public ushort wHour;
            public ushort wMinute;
            public ushort wSecond;
            public ushort wMilliseconds;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetLocalTime(ref SYSTEMTIME st);

        #endregion

        #region Windows API - Taskbar (User32)

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnableWindow(IntPtr hWnd, [MarshalAs(UnmanagedType.Bool)] bool bEnable);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        #endregion

        private readonly DaylightService _daylightService;

        public SystemSettingsService()
        {
            _daylightService = new DaylightService();
        }

        #region Public APIs

        public bool SetDisplayBrightness(int dayLevel, int nightLevel)
        {
            try
            {
                if ((dayLevel < 0 || dayLevel > 100) || (nightLevel < 0 || nightLevel > 100))
                {
                    _logger.Info($"[SystemSettings] Invalid brightness - Day:{dayLevel}, Night:{nightLevel}");
                    return false;
                }

                // Save settings to settings.ini file
                DisplayBrightnessSettings settings = AppSettingsManager.DisplayBrightnessSettings;

                settings.LevelForDay = dayLevel;
                settings.LevelForNight = nightLevel;

                AppSettingsManager.Save();

                // Apply brightness
                int current = _daylightService.IsDayTime() ? dayLevel : nightLevel;
                ApplyBrightness(current);
                _logger.Info($"[SystemSettings] Brightness set - Day:{dayLevel}, Night:{nightLevel}, Applied:{current}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Info($"[SystemSettings] Error setting brightness: {ex.Message}");
                return false;
            }
        }

        public bool SetSoundVolume(int dayLevel, int nightLevel)
        {
            try
            {
                if ((dayLevel < 0 || dayLevel > 100) || (nightLevel < 0 || nightLevel > 100))
                {
                    _logger.Info($"[SystemSettings] Invalid volume - Day:{dayLevel}, Night:{nightLevel}");
                    return false;
                }

                // Save settings to settings.ini file
                SoundVolumeSettings settings = AppSettingsManager.SoundVolumeSettings;

                settings.LevelForDay = dayLevel;
                settings.LevelForNight = nightLevel;

                AppSettingsManager.Save();

                // Apply volume
                int current = _daylightService.IsDayTime() ? dayLevel : nightLevel;
                bool ok = ApplyVolume(current);
                if (ok)
                    _logger.Info($"[SystemSettings] Volume set - Day:{dayLevel}, Night:{nightLevel}, Applied:{current}");
                else
                    _logger.Info($"[SystemSettings] Volume apply failed (target:{current})");
                return ok;
            }
            catch (Exception ex)
            {
                _logger.Info($"[SystemSettings] Error setting volume: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 작업표시줄 활성/비활성 설정
        /// true: 표시 + 활성, false: 숨김 + 비활성
        /// </summary>
        /// <param name="isEnabled">true: 활성화(표시), false: 비활성화(숨김)</param>
        /// <returns>성공 여부</returns>
        public bool SetTaskbarEnabled(bool isEnabled)
        {
            try
            {
#if DEBUG
                if (!isEnabled)
                {
                    _logger?.Info("[SystemSettings] Debug build - skipping taskbar disable request.");
                    return true;
                }
#endif

                // 주/보조 모니터 작업표시줄 핸들 획득
                List<IntPtr> taskbarHandles = new List<IntPtr>();

                IntPtr primaryTaskbar = FindWindow("Shell_TrayWnd", null);
                if (primaryTaskbar != IntPtr.Zero)
                {
                    taskbarHandles.Add(primaryTaskbar);
                }

                IntPtr secondaryTaskbar = IntPtr.Zero;
                while (true)
                {
                    secondaryTaskbar = FindWindowEx(IntPtr.Zero, secondaryTaskbar, "Shell_SecondaryTrayWnd", null);
                    if (secondaryTaskbar == IntPtr.Zero)
                        break;

                    taskbarHandles.Add(secondaryTaskbar);
                }

                if (taskbarHandles.Count == 0)
                {
                    _logger?.Warn("[SystemSettings] Taskbar windows not found (Shell_TrayWnd/Shell_SecondaryTrayWnd)");
                    return false;
                }

                bool allSuccess = true;
                foreach (IntPtr taskbarHandle in taskbarHandles)
                {
                    bool success;
                    if (isEnabled)
                    {
                        // 활성화 시: 표시 후 입력 활성
                        bool showOk = ShowWindow(taskbarHandle, SW_SHOW);
                        bool enableOk = EnableWindow(taskbarHandle, true);
                        success = showOk && enableOk;
                    }
                    else
                    {
                        // 비활성화 시: 입력 비활성 후 숨김
                        bool disableOk = EnableWindow(taskbarHandle, false);
                        bool hideOk = ShowWindow(taskbarHandle, SW_HIDE);
                        success = disableOk && hideOk;
                    }

                    if (!success)
                    {
                        int error = Marshal.GetLastWin32Error();
                        _logger?.Warn($"[SystemSettings] Failed to set taskbar state. Handle={taskbarHandle}, Win32Error={error}, RequestedEnabled={isEnabled}");
                        allSuccess = false;
                    }
                }

                if (allSuccess)
                {
                    _logger?.Info($"[SystemSettings] Taskbar set to: {(isEnabled ? "Enabled+Visible" : "Disabled+Hidden")}, Count={taskbarHandles.Count}");
                }

                return allSuccess;
            }
            catch (Exception ex)
            {
                _logger?.Error($"[SystemSettings] Error setting taskbar state: {ex.Message}");
                return false;
            }
        }

        public int GetCurrentBrightness()
        {
            // 1) Try WMI (내장 패널)
            try
            {
                using (var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT CurrentBrightness FROM WmiMonitorBrightness"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        try
                        {
                            byte b = (byte)mo["CurrentBrightness"];
                            _logger.Info($"[GetCurrentBrightness] (WMI) {b}%");
                            return b;
                        }
                        catch (Exception ex)
                        {
                            _logger.Info($"[GetCurrentBrightness] WMI read error: {ex.Message}");
                        }
                        finally
                        {
                            if (mo != null) mo.Dispose();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Info($"[GetCurrentBrightness] WMI failed: {ex.Message}");
            }

            // 2) Try DDC/CI (외장 모니터)
            List<PHYSICAL_MONITOR> monitors = new List<PHYSICAL_MONITOR>();
            try
            {
                MonitorEnumDelegate cb = delegate (IntPtr hMon, IntPtr hdc, ref RECT rc, IntPtr data)
                {
                    uint count;
                    if (GetNumberOfPhysicalMonitorsFromHMONITOR(hMon, out count) && count > 0)
                    {
                        PHYSICAL_MONITOR[] arr = new PHYSICAL_MONITOR[count];
                        if (GetPhysicalMonitorsFromHMONITOR(hMon, count, arr))
                            monitors.AddRange(arr);
                        else
                            _logger.Info($"[GetBrightness] GetPhysicalMonitorsFromHMONITOR failed, err={Marshal.GetLastWin32Error()}");
                    }
                    else
                    {
                        _logger.Info($"[GetBrightness] GetNumberOfPhysicalMonitorsFromHMONITOR failed/zero, err={Marshal.GetLastWin32Error()}");
                    }
                    return true;
                };

                if (EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, cb, IntPtr.Zero))
                {
                    foreach (PHYSICAL_MONITOR m in monitors)
                    {
                        try
                        {
                            uint min, cur, max;
                            if (GetMonitorBrightness(m.hPhysicalMonitor, out min, out cur, out max))
                            {
                                int pct = max > min ? (int)((cur - min) * 100 / (max - min)) : 0;
                                _logger.Info($"[GetCurrentBrightness] (DDC/CI) {pct}%");
                                return pct;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Info($"[GetCurrentBrightness] DDC/CI read error: {ex.Message}");
                        }
                    }
                }
                else
                {
                    _logger.Info($"[GetCurrentBrightness] EnumDisplayMonitors failed, err={Marshal.GetLastWin32Error()}");
                }
            }
            catch (Exception ex)
            {
                _logger.Info($"[GetCurrentBrightness] DDC/CI failed: {ex.Message}");
            }
            finally
            {
                if (monitors.Count > 0)
                    DestroyPhysicalMonitors((uint)monitors.Count, monitors.ToArray());
            }

            _logger.Info($"[GetCurrentBrightness] Unknown (-1)");
            return -1;
        }

        public bool SetSystemTime(DateTime serverTime)
        {
            try
            {
                SYSTEMTIME st = new SYSTEMTIME
                {
                    wYear = (ushort)serverTime.Year,
                    wMonth = (ushort)serverTime.Month,
                    wDay = (ushort)serverTime.Day,
                    wHour = (ushort)serverTime.Hour,
                    wMinute = (ushort)serverTime.Minute,
                    wSecond = (ushort)serverTime.Second,
                    wMilliseconds = (ushort)serverTime.Millisecond,
                    wDayOfWeek = 0 // Not used by SetLocalTime
                };

                bool success = SetLocalTime(ref st);
                if (!success)
                {
                    int error = Marshal.GetLastWin32Error();
                    _logger.Warn($"[TIME_SYNC] 시스템 시간 설정 실패. Win32 오류코드={error}");
                    return false;
                }

                _logger.Info($"[TIME_SYNC] Windows 시스템 시간 동기화 성공. 서버시각={serverTime:yyyy-MM-dd HH:mm:ss}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"[TIME_SYNC] SetSystemTime 처리 중 예외 발생: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Internals

        private bool ApplyVolume(int level)
        {
            try
            {
                float vol = Math.Max(0f, Math.Min(1f, level / 100f));
                _logger.Info($"[ApplyVolume] Target={level}% ({vol})");

                IMMDeviceEnumerator enumr = (IMMDeviceEnumerator)new MMDeviceEnumerator();
                IMMDevice dev;
                int hr = enumr.GetDefaultAudioEndpoint(EDataFlow_eRender, ERole_eMultimedia, out dev);
                if (hr != 0 || dev == null)
                {
                    _logger.Info($"[ApplyVolume] GetDefaultAudioEndpoint failed, hr=0x{hr:X8}");
                    Marshal.ReleaseComObject(enumr);
                    return false;
                }

                Guid iid = typeof(IAudioEndpointVolume).GUID;
                object obj;
                hr = dev.Activate(ref iid, CLSCTX_INPROC_SERVER, IntPtr.Zero, out obj);

                // release enumerator/device early
                Marshal.ReleaseComObject(dev);
                Marshal.ReleaseComObject(enumr);

                if (hr != 0 || obj == null)
                {
                    _logger.Info($"[ApplyVolume] Activate IAudioEndpointVolume failed, hr=0x{hr:X8}");
                    if (obj != null) Marshal.ReleaseComObject(obj);
                    return false;
                }

                try
                {
                    IAudioEndpointVolume ep = (IAudioEndpointVolume)obj;

                    float before;
                    ep.GetMasterVolumeLevelScalar(out before);
                    _logger.Info($"[ApplyVolume] Before={(int)(before * 100)}%");

                    Guid ctx = Guid.NewGuid();
                    hr = ep.SetMasterVolumeLevelScalar(vol, ref ctx);
                    if (hr != 0)
                    {
                        _logger.Info($"[ApplyVolume] SetMasterVolumeLevelScalar failed, hr=0x{hr:X8}");
                        return false;
                    }

                    float after;
                    ep.GetMasterVolumeLevelScalar(out after);
                    _logger.Info($"[ApplyVolume] After={(int)(after * 100)}%");
                    return true;
                }
                finally
                {
                    Marshal.ReleaseComObject(obj);
                }
            }
            catch (Exception ex)
            {
                _logger.Info($"[ApplyVolume] Exception: {ex.Message}");
                return false;
            }
        }

        private void ApplyBrightness(int level)
        {
            bool ok = false;

            try
            {
                _logger.Info($"[ApplyBrightness] via DDC/CI => {level}%");
                ok = SetBrightnessUsingWindowsAPI(level);
                if (ok)
                    _logger.Info($"[ApplyBrightness] DDC/CI succeeded");
                else
                    _logger.Info($"[ApplyBrightness] DDC/CI not available or failed");
            }
            catch (Exception ex)
            {
                _logger.Info($"[ApplyBrightness] DDC/CI exception: {ex.Message}");
            }

            try
            {
                _logger.Info($"[ApplyBrightness] via WMI => {level}%");
                SetBrightnessUsingWMI(level);
                _logger.Info($"[ApplyBrightness] WMI attempted");
            }
            catch (Exception ex)
            {
                _logger.Info($"[ApplyBrightness] WMI exception: {ex.Message}");
            }

            _logger.Info($"[ApplyBrightness] Done (target {level}%)");
        }

        private bool SetBrightnessUsingWindowsAPI(int level)
        {
            List<PHYSICAL_MONITOR> list = new List<PHYSICAL_MONITOR>();
            bool any = false;

            try
            {
                MonitorEnumDelegate cb = delegate (IntPtr hMon, IntPtr hdc, ref RECT rc, IntPtr data)
                {
                    uint count;
                    if (GetNumberOfPhysicalMonitorsFromHMONITOR(hMon, out count) && count > 0)
                    {
                        PHYSICAL_MONITOR[] arr = new PHYSICAL_MONITOR[count];
                        if (GetPhysicalMonitorsFromHMONITOR(hMon, count, arr))
                            list.AddRange(arr);
                        else
                            _logger.Info($"[DDC/CI] GetPhysicalMonitorsFromHMONITOR failed, err={Marshal.GetLastWin32Error()}");
                    }
                    else
                    {
                        _logger.Info($"[DDC/CI] GetNumberOfPhysicalMonitorsFromHMONITOR failed/zero, err={Marshal.GetLastWin32Error()}");
                    }
                    return true;
                };

                if (!EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, cb, IntPtr.Zero))
                {
                    _logger.Info($"[DDC/CI] EnumDisplayMonitors failed, err={Marshal.GetLastWin32Error()}");
                    return false;
                }

                foreach (PHYSICAL_MONITOR m in list)
                {
                    try
                    {
                        uint caps, _dummy;
                        if (GetMonitorCapabilities(m.hPhysicalMonitor, out caps, out _dummy)
                            && (caps & MC_CAPS_BRIGHTNESS) != 0)
                        {
                            uint min, cur, max;
                            if (GetMonitorBrightness(m.hPhysicalMonitor, out min, out cur, out max))
                            {
                                uint tgt = (uint)(min + (max - min) * level / 100.0);
                                if (tgt < min) tgt = min;
                                if (tgt > max) tgt = max;

                                if (!SetMonitorBrightness(m.hPhysicalMonitor, tgt))
                                {
                                    _logger.Info($"[DDC/CI] SetMonitorBrightness failed, err={Marshal.GetLastWin32Error()}");
                                }
                                else
                                {
                                    any = true;
                                    System.Threading.Thread.Sleep(150);
                                    uint _min2, newCur, _max2;
                                    if (GetMonitorBrightness(m.hPhysicalMonitor, out _min2, out newCur, out _max2))
                                    {
                                        int pct = max > min ? (int)((newCur - min) * 100 / (max - min)) : 0;
                                        _logger.Info($"[DDC/CI] After={pct}%");
                                    }
                                }
                            }
                        }
                        else
                        {
                            _logger.Info($"[DDC/CI] Monitor doesn't support brightness capability");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Info($"[DDC/CI] per-monitor error: {ex.Message}");
                    }
                }

                return any;
            }
            finally
            {
                if (list.Count > 0)
                    DestroyPhysicalMonitors((uint)list.Count, list.ToArray());
            }
        }

        private void SetBrightnessUsingWMI(int level)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightnessMethods"))
                {
                    var results = searcher.Get();
                    var it = results.GetEnumerator();
                    if (!it.MoveNext())
                    {
                        _logger.Info($"[WMI] No WmiMonitorBrightnessMethods found");
                        return;
                    }

                    foreach (ManagementObject mo in results)
                    {
                        try
                        {
                            mo.InvokeMethod("WmiSetBrightness", new object[] { (uint)0, (byte)level });
                            _logger.Info($"[WMI] WmiSetBrightness OK");
                        }
                        catch (ManagementException mex)
                        {
                            string msg = mex.Message != null ? mex.Message : string.Empty;
                            if (msg.Contains("Not Supported") || msg.Contains("지원하지 않음"))
                                _logger.Info($"[WMI] Not supported on this monitor (normal)");
                            else
                                _logger.Info($"[WMI] ManagementException: {mex.Message}");
                        }
                        catch (Exception ex)
                        {
                            _logger.Info($"[WMI] Exception: {ex.Message}");
                        }
                        finally
                        {
                            if (mo != null) mo.Dispose();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Info($"[WMI] searcher failed: {ex.Message}");
            }
        }

        #endregion
    }
}
