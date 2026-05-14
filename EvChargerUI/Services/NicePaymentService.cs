using System;
using System.IO;
using System.IO.Ports;
using System.Timers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using EvChargerUI.Commons.Settings;
using EvChargerUI.Commons.Util;
using EvChargerUI.Domains;

namespace EvChargerUI.Services
{
    public class NicePaymentService : IPaymentService
    {
        private readonly FileLogger _logger;
        private readonly System.Timers.Timer _healthCheckTimer;
        private readonly object _healthCheckLock = new object();
        private bool _isHealthCheckRunning;

        private volatile bool _isConnected;
        public bool IsConnected => _isConnected;
        private volatile bool _isAvailable;
        public bool IsAvailable => _isAvailable;

        private volatile bool _isPaymentDeviceCommFault;
        public bool IsPaymentDeviceCommFault => _isPaymentDeviceCommFault;
        private bool _prevPaymentDeviceCommFault;
        private readonly Action<bool> _onPaymentCommFaultChanged;

        private void UpdateConnectionState(bool isConnected, bool? isAvailable = null)
        {
            _isConnected = isConnected;
            _isAvailable = isAvailable ?? isConnected;
        }

        private void UpdatePaymentCommFault(bool isConnected)
        {
            bool newFault = !isConnected;
            if (newFault == _prevPaymentDeviceCommFault)
                return;

            _isPaymentDeviceCommFault = newFault;
            _prevPaymentDeviceCommFault = newFault;
            _logger?.Warn($"[NicePaymentService] PaymentDeviceCommFault changed: {newFault}");
            _onPaymentCommFaultChanged?.Invoke(newFault);
        }

        [DllImport("NVCAT.dll", CharSet = CharSet.Unicode)]
        public static extern int NICEVCAT(byte[] SendBuf, byte[] RecvBuf);

        [DllImport("NVCAT.dll", CharSet = CharSet.Unicode)]
        public static extern int REQ_BALANCE(byte[] type, byte[] RecvBuf);

        [DllImport("NVCAT.dll", CharSet = CharSet.Unicode)]
        public static extern int REQ_STOP();

        /// <summary>NVCAT 모듈 사용 설명서: void __stdcall READ_STATUS(char* msgstatus, char* msg).</summary>
        [DllImport("NVCAT.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private static extern void READ_STATUS([Out] StringBuilder msgstatus, [Out] StringBuilder msg);

        

        public NicePaymentService(Action<bool> onPaymentCommFaultChanged = null)
        {
            _onPaymentCommFaultChanged = onPaymentCommFaultChanged;
            UpdateConnectionState(false, false);
            _logger = (Application.Current as App)?.AppLogger;
            int intervalMs = Math.Max(10_000,
                AppSettingsManager.ChargerSettings.PaymentDeviceHealthCheckInterval * 1000);

            _healthCheckTimer = new System.Timers.Timer(intervalMs);
            _healthCheckTimer.Elapsed += OnHealthCheckElapsed;
            _healthCheckTimer.AutoReset = true;
            _healthCheckTimer.Start();
        }

        public bool Open()
        {
            try
            {
                // 연결 여부는 헬스체크·실거래에서만 갱신 — 열자마자 true로 두면 전원 뽑은 뒤에도 녹색으로 남음
                UpdateConnectionState(false, false);
                return true;
            }
            catch
            {
                UpdateConnectionState(false);
                return false;
            }
        }

        public void Close()
        {
            UpdateConnectionState(false);
        }

        public Task<PaymentInfo> PayCost(int cost, string csName)
        {
            return Task.Run(() =>
            {
                string fs = ((char)28).ToString();
                cost = MoneyUtil.TruncateWonUnit(cost);
                int tax = (int)((double)cost - (double)cost / 1.1);
                string sendData = "0200" + fs + "10" + fs + "C" + fs + cost.ToString() + fs + tax.ToString() + fs + "0" + fs + "00" + fs + "" + fs + "" + fs + "" + fs + fs + fs + fs + "" + fs + fs + fs + fs + "신용승인" + fs;

                byte[] mSend = Encoding.GetEncoding(1252).GetBytes(sendData);
                byte[] mRecv = new byte[2048];

                PaymentInfo result = null;
                int ret = 0;
                Exception caught = null;
                try
                {
                    ret = NICEVCAT(mSend, mRecv);
                    if (ret == 1)
                    {
                        string response = Encoding.GetEncoding(949).GetString(mRecv);
                        string[] responseStrings = response.Split((char)28);

                        Console.WriteLine("responseStrings length: " + responseStrings.Length);
                        for (int i = 0; i < responseStrings.Length; i++)
                            Console.WriteLine($"responseStrings[{i}]: '{responseStrings[i]}'");

                        result = new PaymentInfo();
                        result.TotalCost = responseStrings[3];
                        result.InstallmentMonths = responseStrings[6] == "00" ? "-" : responseStrings[6];
                        result.AuthNum = responseStrings[7].Trim();
                        result.PayDate = responseStrings[8].Substring(0, 6);
                        result.PayTime = responseStrings[8].Substring(6, 6);
                        result.CardIssuerName = responseStrings[10].Trim();
                        result.CardAcquirerName = responseStrings[12].Trim();
                        result.MaskedCardNumber = responseStrings[17].Trim();
                        result.PgNum = responseStrings[20];
                        result.MerchantName = "한국자동차환경협회(사)";
                        result.MerchantId = responseStrings[34];
                    }
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
                return result;
            });
        }

        public Task<string> ReadRfCard()
        {
            return Task.Run(() =>
            {
                byte[] recv = new byte[2048];
                string result = null;
                int ret = 0;
                Exception caught = null;
                try
                {
                    ret = REQ_BALANCE(Encoding.GetEncoding(1252).GetBytes("1"), recv);
                    if (ret == 1)
                        result = Encoding.UTF8.GetString(recv, 11, 16);
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
                return result;
            });
        }

        public Task<bool> CancelPay(PaymentInfo paymentInfo, int cancelCost, string csName)
        {
            return Task.Run(() =>
            {
                bool retVal = false;
                try
                {
                    if (paymentInfo == null)
                    {
                        _logger?.Warn("[NicePaymentService] CancelPay - paymentInfo is null, skipping cancel.");
                        return false;
                    }

                    string fs = ((char)28).ToString();
                    cancelCost = MoneyUtil.TruncateWonUnit(cancelCost);
                    int tax = (int)((double)cancelCost - (double)cancelCost / 1.1);

                    int totalCost = Int32.Parse(paymentInfo.TotalCost);
                    int totalTax = (int)((double)totalCost - (double)totalCost / 1.1);

                    string sendData;
                    if (totalCost == cancelCost || (totalCost - cancelCost) < 100)
                        sendData = "0420" + fs + "10" + fs + "N" + fs + totalCost.ToString() + fs + totalTax.ToString() + fs + "0" + fs + "00" + fs + paymentInfo.AuthNum + fs + paymentInfo.PayDate + fs + "" + fs + fs + fs + paymentInfo.PgNum + fs + fs + fs + fs + fs + fs + fs + fs + fs + fs + fs + fs;
                    else
                        sendData = "0520" + fs + "30" + fs + "N" + fs + cancelCost.ToString() + fs + tax.ToString() + fs + "0" + fs + "00" + fs + paymentInfo.AuthNum + fs + paymentInfo.PayDate + fs + "" + fs + fs + fs + paymentInfo.PgNum + fs + fs + fs + fs + fs + "" + fs + "P" + paymentInfo.TotalCost + fs + "PCL" + fs + fs + fs + fs + fs + fs + fs + fs + fs + fs;

                    byte[] mSend = Encoding.GetEncoding(1252).GetBytes(sendData);
                    byte[] mRecv = new byte[2048];

                    int ret = NICEVCAT(mSend, mRecv);
                    retVal = ret == 1;
                }
                catch (Exception ex)
                {
                    _logger?.Error($"[NicePaymentService] CancelPay - exception: {ex.Message}");
                }
                return retVal;
            });
        }

        public Task<bool> CancelCardReading()
        {
            return Task.Run(() =>
            {
                try
                {
                    int ret = REQ_STOP();
                    _logger?.Debug($"[NicePaymentService] CancelCardReading - REQ_STOP returned {ret}.");

                    if (ret == 0)
                    {
                        UpdateConnectionState(true);
                        return true;
                    }
                    if (ret == -1)
                        UpdateConnectionState(false);
                    return false;
                }
                catch (Exception ex)
                {
                    UpdateConnectionState(false);
                    _logger?.Warn($"[NicePaymentService] CancelCardReading - REQ_STOP threw exception: {ex.Message}");
                    return false;
                }
            });
        }

        private void OnHealthCheckElapsed(object sender, ElapsedEventArgs e)
        {
            lock (_healthCheckLock)
            {
                if (_isHealthCheckRunning)  return;
                _isHealthCheckRunning = true;
            }

            try
            {
                var ok = CheckComPort();
                UpdateConnectionState(ok);
                _logger?.Debug($"[NicePaymentService] HealthCheck - COM port check: {ok}");
            }
            catch (Exception ex)
            {
                UpdateConnectionState(false);
                _logger?.Error($"[NicePaymentService] HealthCheck - Exception occurred: {ex.Message}");
            }
            finally
            {
                UpdatePaymentCommFault(_isConnected);
                lock (_healthCheckLock) { _isHealthCheckRunning = false; }
            }
        }

        private bool CheckComPort()
        {
            var portNoRaw = AppSettingsManager.ChargerSettings.PaymentDeviceComPortNo;
            if (string.IsNullOrWhiteSpace(portNoRaw))
                return false;

            var portName = NormalizeComPortName(portNoRaw);

            var ports = SerialPort.GetPortNames();
            var exists = false;
            for (int i = 0; i < ports.Length; i++)
            {
                if (string.Equals(ports[i], portName, StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }
            if (!exists) return false;

            try
            {
                using (var sp = new SerialPort(portName, AppSettingsManager.ChargerSettings.PaymentDeviceBaudRate))
                {
                    sp.ReadTimeout = 200;
                    sp.WriteTimeout = 200;
                    sp.Open();
                    sp.Close();
                }

                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string NormalizeComPortName(string raw)
        {
            var s = raw.Trim();
            if (string.IsNullOrEmpty(s)) return s;

            if (Regex.IsMatch(s, @"(?i)^COM\d+$"))
                return s.ToUpperInvariant();

            if (int.TryParse(s, out var n) && n > 0)
                return "COM" + n;

            return s.ToUpperInvariant();
        }
    }
}
