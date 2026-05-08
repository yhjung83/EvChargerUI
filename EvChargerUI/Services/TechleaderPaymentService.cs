using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using EvChargerUI.Commons.Settings;
using EvChargerUI.Commons.Util;
using EvChargerUI.Domains;

namespace EvChargerUI.Services
{
    public class TechleaderPaymentService : IPaymentService
    {
        private readonly FileLogger _logger;
        private const string DefaultMerchantName = "한국자동차환경협회(사)";
        private static void ParseCardIssuerNameAndAcquirerName(string errMsg1, out string cardIssuerName, out string cardAcquirerName)
        {
            cardIssuerName = null;
            cardAcquirerName = null;
            if (string.IsNullOrWhiteSpace(errMsg1)) return;

            // 숫자 제거 후, 영문/한글이 아닌 모든 문자를 공백으로 치환하고 공백을 1개로 정규화
            var s = errMsg1;
            s = Regex.Replace(s, @"\d+", string.Empty);
            s = Regex.Replace(s, @"[^A-Za-z가-힣]+", " ");
            s = Regex.Replace(s, @"\s+", " ").Trim();
            if (string.IsNullOrEmpty(s)) return;

            var parts = s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0) cardIssuerName = parts[0].Trim();
            if (parts.Length > 1) cardAcquirerName = parts[1].Trim();
        }
        private static string NormalizeInstallmentMonths(string divRaw)
        {
            var s = divRaw.Trim();
            if (string.IsNullOrEmpty(s)) return "-";
            if (s == "00") return "-";
            return s;
        }
        private TL3600 _tl3600;
        private TaskCompletionSource<Dictionary<string, string>> _payTaskCompletionSource;
        private TaskCompletionSource<Dictionary<string, string>> _cancelTaskCompletionSource;
        private TaskCompletionSource<Dictionary<string, string>> _readRfCardTaskCompletionSource;
        private TaskCompletionSource<Dictionary<string, string>> _healthCheckTaskCompletionSource;
        private bool _isIcCardInserted;
        private bool _isConnected;
        public bool IsConnected => _isConnected;
        public bool IsAvailable => _isConnected;
        public bool IsIcCardInserted => _isIcCardInserted;
        private readonly System.Timers.Timer _healthCheckTimer;
        private readonly object _healthCheckLock = new object();
        private readonly object _reconnectLock = new object();
        private readonly SemaphoreSlim _requestSemaphore = new SemaphoreSlim(1, 1);
        private readonly object _requestStateLock = new object();
        private bool _isHealthCheckRunning;
        private int _activeTransactionCount;
        private int _consecutiveHealthCheckFailures;
        private const int HealthCheckFailureThreshold = 3;

        private enum ActiveRequestType
        {
            None,
            Pay,
            Cancel,
            ReadRfCard
        }

        private ActiveRequestType _activeRequestType = ActiveRequestType.None;

        public TechleaderPaymentService()
        {
            _isConnected = false;
            _logger = (Application.Current as App)?.AppLogger;
            int intervalMs = Math.Max(1000, AppSettingsManager.ChargerSettings.PaymentDeviceHealthCheckInterval * 1000);

            _healthCheckTimer = new System.Timers.Timer(intervalMs);
            _healthCheckTimer.AutoReset = true;
            _healthCheckTimer.Elapsed += async (s, e) => await PerformHealthCheckAsync();
        }
        public bool Open()
        {
            bool portOpen = false;
            try
            {
                _tl3600 = new TL3600("ECP" + AppSettingsManager.ChargerSettings.StationId);
                _tl3600.SetResponseCallback(new TL3600.ResponseCallback(this.ResponseCallback));
                _tl3600.SetDbgLogEvent(new TL3600.DbgLogEvent(this.LogCallback));
                portOpen = _tl3600.Open("COM" + AppSettingsManager.ChargerSettings.PaymentDeviceComPortNo, AppSettingsManager.ChargerSettings.PaymentDeviceBaudRate);
                // COM 오픈만으로는 연결 정상으로 보지 않음 — TermCheck(헬스체크) 응답으로만 _isConnected=true
                _isConnected = false;

                if (!portOpen && _tl3600 != null)
                {
                    try { _tl3600.Close(); } catch { }
                    _tl3600 = null;
                }

                _healthCheckTimer.Start();
                if (portOpen)
                    _ = PerformHealthCheckAsync();
            }
            catch (Exception ex)
            {
                _logger?.Warn($"[TechleaderPaymentService] Open exception: {ex.Message}");
                _isConnected = false;
                if (_tl3600 != null)
                {
                    try { _tl3600.Close(); } catch { }
                    _tl3600 = null;
                }
                _healthCheckTimer.Start();
            }

            return portOpen;
        }

        public void Close()
        {
            // _healthCheckTimer.Stop();
            if (_tl3600 != null)
            {
                _tl3600.Close();
                _tl3600 = null;
            }
            _isConnected = false;
        }

        public async Task<PaymentInfo > PayCost(int cost, string csName)
        {
            await _requestSemaphore.WaitAsync();
            BeginRequest(ActiveRequestType.Pay);
            try
            {
                PaymentInfo retVal = null;

                // 원단위 절삭: 1원 자리 버림(=10원 단위로 내림)
                cost = MoneyUtil.TruncateWonUnit(cost);
                Dictionary<string, string> result = await SendPayAsync(cost, csName);

                if (result != null && result["payCode"] == "1" && result.Count >= 15)
                {
                    retVal = new PaymentInfo();
                    retVal.PayCode = result["payCode"];
                    retVal.AuthNum = result["authNum"];
                    retVal.TotalCost = result["totalCost"];
                    retVal.PayDate = result["payDate"];
                    retVal.PayTime = result["payTime"];

                    retVal.PgNum = result["pgnum"];

                    // 전표(팝업) 표시용 추가 매핑
                    retVal.MerchantName = DefaultMerchantName;
                    retVal.MerchantId = result["regNum"];                      // 가맹번호(MID로 추정)
                    retVal.MaskedCardNumber = result["cardNum"];                           // 카드번호(고정길이)
                    retVal.InstallmentMonths = NormalizeInstallmentMonths(result["div"]);  // 할부개월(00이면 일시불)
                    ParseCardIssuerNameAndAcquirerName(result["errMsg1"].Trim(), out var cardIssuerName, out var cardAcquirerName);
                    retVal.CardIssuerName = cardIssuerName;
                    retVal.CardAcquirerName = cardAcquirerName;
                }
                else
                {
                    // 결제 실패 또는 취소 시 단말기에 Ready 명령(CMD 'E') 전송하여 대기 상태로 복귀
                    await CancelCardReading();
                }

                //    retVal.PayCode = result["payCode"].Trim();
                //    retVal.AuthNum = result["authNum"].Trim();
                //    retVal.TotalCost = result["totalCost"].Trim();
                //    retVal.PayDate = result["payDate"].Trim();
                //    retVal.PayTime = result["payTime"].Trim();

                //    retVal.PgNum = result["pgnum"].Trim();

                //    // 전표(팝업) 표시용 추가 매핑
                //    retVal.MerchantName = DefaultMerchantName;
                //    retVal.MerchantId = result["regNum"].Trim();                      // 가맹번호(MID로 추정)
                //    retVal.MaskedCardNumber = result["cardNum"].Trim();                           // 카드번호(고정길이)
                //    retVal.InstallmentMonths = NormalizeInstallmentMonths(result["div"].Trim());  // 할부개월(00이면 일시불)
                //    ParseCardIssuerNameAndAcquirerName(result["errMsg1"].Trim(), out var cardIssuerName, out var cardAcquirerName);
                //    retVal.CardIssuerName = cardIssuerName;
                //    retVal.CardAcquirerName = cardAcquirerName;
                //}

                return retVal;
            }
            finally
            {
                EndRequest(ActiveRequestType.Pay);
                _requestSemaphore.Release();
            }
        }

        public async Task<bool> CancelPay(PaymentInfo paymentInfo, int cancelCost, string csName)
        {
            await _requestSemaphore.WaitAsync();
            BeginRequest(ActiveRequestType.Cancel);
            try
            {
                bool retVal = false;
                Dictionary<string, string> result = null;
                Dictionary<string, string> paymentDic = new Dictionary<string, string>();

                paymentDic["authNum"] = paymentInfo.AuthNum;
                paymentDic["totalCost"]= paymentInfo.TotalCost;
                paymentDic["payDate"] = paymentInfo.PayDate;
                paymentDic["payTime"] = paymentInfo.PayTime;
                paymentDic["pgnum"] = paymentInfo.PgNum;

                // 원단위 절삭: 1원 자리 버림(=10원 단위로 내림)
                cancelCost = MoneyUtil.TruncateWonUnit(cancelCost);

                //if (int.Parse(paymentInfo.TotalCost) > cancelCost)
                if((int.Parse(paymentInfo.TotalCost) - cancelCost) > 100)
                {
                    result = await SendPartCancelPay(paymentDic, cancelCost, csName);
                }
                else
                {
                    result = await SendCancelPay(paymentDic, csName);
                }

                if (result != null)
                {
                    retVal = result["payCode"] == "1";
                }

                return retVal;
            }
            finally
            {
                EndRequest(ActiveRequestType.Cancel);
                _requestSemaphore.Release();
            }
        }

        public async Task<bool> CancelCardReading()
        {
            try
            {
                _logger?.Debug("[TechleaderPaymentService] CancelCardReading - Sending TermReadyReq (CMD 'E') to cancel card waiting.");

                // 1. 대기 중인 요청 TaskCompletionSource를 null로 완료시켜 PayCost/CancelPay/ReadRfCard 대기 해제
                var hasPending = false;

                var payTcs = _payTaskCompletionSource;
                if (payTcs != null)
                {
                    hasPending = true;
                    payTcs.TrySetResult(null);
                    _payTaskCompletionSource = null;
                }

                var cancelTcs = _cancelTaskCompletionSource;
                if (cancelTcs != null)
                {
                    hasPending = true;
                    cancelTcs.TrySetResult(null);
                    _cancelTaskCompletionSource = null;
                }

                var readRfCardTcs = _readRfCardTaskCompletionSource;
                if (readRfCardTcs != null)
                {
                    hasPending = true;
                    readRfCardTcs.TrySetResult(null);
                    _readRfCardTaskCompletionSource = null;
                }

                if (hasPending)
                    _logger?.Debug("[TechleaderPaymentService] CancelCardReading - Pending tasks cancelled.");

                // 2. 단말기에 Ready 명령(CMD 'E', 0x45) 전송하여 카드 대기 상태 중단
                //    결제 승인 완료 전 취소이므로 Reset이 아닌 Ready 명령 사용
                if (_tl3600 != null)
                {
                    _tl3600.SetState(TL3600.State.Ready);
                    _tl3600.TermReadyReq(DateTime.Now);
                }

                // 3. 내부 상태 초기화
                _isIcCardInserted = false;
                TL3600.checkcancel = false;

                // Ready 명령 후 단말기 안정화 대기
                await Task.Delay(500);

                // 4. 단말기를 Ready 상태로 복원
                if (_tl3600 != null)
                {
                    _tl3600.SetState(TL3600.State.Ready);
                }

                _logger?.Debug("[TechleaderPaymentService] CancelCardReading - Complete. Terminal returned to Ready state.");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.Warn($"[TechleaderPaymentService] CancelCardReading - Exception: {ex.Message}");
                return false;
            }
        }

        public async Task<string> ReadRfCard()
        {
            await _requestSemaphore.WaitAsync();
            BeginRequest(ActiveRequestType.ReadRfCard);
            try
            {
                Dictionary<string, string> result = await SendReadRfCardAsync();

                if (result != null && result.ContainsKey("cardnum") && result["cardnum"] != null)
                {
                    return result["cardnum"].Substring(4);
                }

                return null;
            }
            finally
            {
                EndRequest(ActiveRequestType.ReadRfCard);
                _requestSemaphore.Release();
            }
        }

        private void ResponseCallback(TL3600.ResponseType type, Dictionary<string, string> retVal)
        {
            switch (type)
            {
                case TL3600.ResponseType.Pay:
                    _isConnected = retVal != null;
                    if (_payTaskCompletionSource != null)
                    {
                        _payTaskCompletionSource.TrySetResult(retVal);
                        _payTaskCompletionSource = null;
                    }
                    break;
                case TL3600.ResponseType.CancelPay:
                    _isConnected = retVal != null;
                    if (_cancelTaskCompletionSource != null)
                    {
                        _cancelTaskCompletionSource.TrySetResult(retVal);
                        _cancelTaskCompletionSource = null;
                    }
                    break;
                case TL3600.ResponseType.Search:
                    _isConnected = retVal != null;
                    if (_readRfCardTaskCompletionSource != null)
                    {
                        _readRfCardTaskCompletionSource.TrySetResult(retVal);
                        _readRfCardTaskCompletionSource = null;
                    }
                    break;
                case TL3600.ResponseType.Error:
                    // 헬스체크 에러 응답 우선 처리
                    TaskCompletionSource<Dictionary<string, string>> errorHealthCheckTcs = null;
                    lock (_healthCheckLock)
                    {
                        errorHealthCheckTcs = _healthCheckTaskCompletionSource;
                    }

                    if (errorHealthCheckTcs != null)
                    {
                        _isConnected = false;
                        _consecutiveHealthCheckFailures++;
                        errorHealthCheckTcs.TrySetResult(retVal);
                        lock (_healthCheckLock)
                        {
                            _healthCheckTaskCompletionSource = null;
                        }
                        return;
                    }

                    // 일반 에러 응답 처리: 현재 활성 요청 1건만 완료
                    _isConnected = false;
                    var activeRequestType = GetActiveRequestType();
                    switch (activeRequestType)
                    {
                        case ActiveRequestType.Pay:
                            if (_payTaskCompletionSource != null)
                            {
                                _payTaskCompletionSource.TrySetResult(retVal);
                                _payTaskCompletionSource = null;
                            }
                            break;
                        case ActiveRequestType.Cancel:
                            if (_cancelTaskCompletionSource != null)
                            {
                                _cancelTaskCompletionSource.TrySetResult(retVal);
                                _cancelTaskCompletionSource = null;
                            }
                            break;
                        case ActiveRequestType.ReadRfCard:
                            if (_readRfCardTaskCompletionSource != null)
                            {
                                _readRfCardTaskCompletionSource.TrySetResult(retVal);
                                _readRfCardTaskCompletionSource = null;
                            }
                            break;
                        default:
                            _logger?.Warn("[TechleaderPaymentService] Error response received without active request context.");
                            break;
                    }
                    break;
                case TL3600.ResponseType.Event:
                    if (retVal.ContainsKey("event"))
                    {
                        //if (retVal["event"] == "O") _isIcCardInserted = false;
                        //else if (retVal["event"] == "I") _isIcCardInserted = true;
                        _isIcCardInserted = true;

                    }
                    break;
                case TL3600.ResponseType.Check:
                    string StatusComport = "-";
                    string StatusRfModule = "-";
                    string StatusVAN = "-";
                    
                    if (retVal != null)
                    {
                        if (retVal.ContainsKey("commStat")) StatusComport = retVal["commStat"];
                        if (retVal.ContainsKey("rfModuleStat")) StatusRfModule = retVal["rfModuleStat"];
                        if (retVal.ContainsKey("vanStat")) StatusVAN = retVal["vanStat"];
                    }

                    Console.WriteLine($"결제단말기 상태 -> StatusComport:{StatusComport}, StatusRfModule:{StatusRfModule}, StatusVAN:{StatusVAN}");

                    // 헬스체크 응답 마지막에 처리
                    TaskCompletionSource<Dictionary<string, string>> healthCheckTcs = null;
                    lock (_healthCheckLock)
                    {
                        healthCheckTcs = _healthCheckTaskCompletionSource;
                    }
                    
                    if (healthCheckTcs != null)
                    {
                        _isConnected = retVal != null;
                        healthCheckTcs.TrySetResult(retVal);
                        lock (_healthCheckLock)
                        {
                            _healthCheckTaskCompletionSource = null;
                        }
                        return;
                    }
                    // 헬스체크 컨텍스트가 없으면(타임아웃 후 지연 응답 포함) 일반 요청으로 라우팅하지 않음
                    _logger?.Debug("[TechleaderPaymentService] Ignored stale Check response without active health check context.");
                    break;
            }
        }

        private void LogCallback(string msg)
        {
            //TLLog tlLog1 = new TLLog();
            //TLLog tlLog2;
            //try
            //{
            //    tlLog1.WriteTL3500BSCommLog(msg);
            //    tlLog2 = (TLLog)null;
            //}
            //catch
            //{
            //    tlLog2 = (TLLog)null;
            //}
        }

        private Task<Dictionary<string, string>> SendPayAsync(int cost, string csName)
        {
            _payTaskCompletionSource = new TaskCompletionSource<Dictionary<string, string>>();

            int tax = (int)((double)cost - (double)cost / 1.1);

            _tl3600.PayReq_G(cost, tax, true, DateTime.Now, csName);

            return _payTaskCompletionSource.Task;
        }

        /// <summary>
        /// 전체 취소
        /// </summary>
        /// <param name="payInfoDic"></param>
        /// <param name="csName"></param>
        /// <returns></returns>
        private Task<Dictionary<string, string>> SendCancelPay(Dictionary<string, string> payInfoDic,  string csName)
        {
            _cancelTaskCompletionSource = new TaskCompletionSource<Dictionary<string, string>>();

            int cancelCost = int.Parse(payInfoDic["totalCost"]);
            int tax = (int)((double)cancelCost - (double)cancelCost / 1.1);
            _tl3600.CancelPay_G(payInfoDic, cancelCost, tax, 4, DateTime.Now, csName);
            return _cancelTaskCompletionSource.Task;
        }

        /// <summary>
        /// 부분 취소
        /// </summary>
        /// <param name="payInfoDic"></param>
        /// <param name="cancelCost"></param>
        /// <param name="csName"></param>
        /// <returns></returns>
        private Task<Dictionary<string, string>> SendPartCancelPay(Dictionary<string, string> payInfoDic, int cancelCost, string csName)
        {
            _cancelTaskCompletionSource = new TaskCompletionSource<Dictionary<string, string>>();

            int tax = (int)((double)cancelCost - (double)cancelCost / 1.1);
            _tl3600.CancelPay_G(payInfoDic, cancelCost, tax, 5, DateTime.Now, csName);
            return _cancelTaskCompletionSource.Task;
        }


        private Task<Dictionary<string, string>> SendReadRfCardAsync()
        {
            _readRfCardTaskCompletionSource = new TaskCompletionSource<Dictionary<string, string>>();


            _tl3600.CardInfoReq(DateTime.Now);

            return _readRfCardTaskCompletionSource.Task;
        }

        private async Task PerformHealthCheckAsync()
        {
            if (!TryEnterHealthCheck())
                return;

            TaskCompletionSource<Dictionary<string, string>> healthCheckTcs = null;
            var shouldReconnect = false;
            try
            {
                if (IsTransactionRunning())
                {
                    _logger?.Debug("[TechleaderPaymentService] HealthCheck skipped: transaction is running.");
                    return;
                }

                if (_tl3600 != null)
                {
                    lock (_healthCheckLock)
                    {
                        healthCheckTcs = new TaskCompletionSource<Dictionary<string, string>>();
                        _healthCheckTaskCompletionSource = healthCheckTcs;
                    }

                    try
                    {
                        var sw = Stopwatch.StartNew();
                        _tl3600.TermCheckReq(DateTime.Now);

                        // 타임아웃 5초
                        var timeoutTask = Task.Delay(5 * 1000);
                        var completedTask = await Task.WhenAny(healthCheckTcs.Task, timeoutTask);

                        if (completedTask == timeoutTask)
                        {
                            // 타임아웃 발생 - 연결 끊김으로 간주
                            _isConnected = false;
                            _consecutiveHealthCheckFailures++;
                            shouldReconnect = _consecutiveHealthCheckFailures >= HealthCheckFailureThreshold;
                            sw.Stop();
                            _logger?.Warn($"[TechleaderPaymentService] HealthCheck TermCheck 응답 경과: {sw.ElapsedMilliseconds}ms (결과: 타임아웃, connected=false, failures={_consecutiveHealthCheckFailures})");
                            lock (_healthCheckLock)
                            {
                                _healthCheckTaskCompletionSource = null;
                            }
                        }
                        else
                        {
                            // 응답 받음 - ResponseCallback에서 _isConnected 업데이트됨
                            try
                            {
                                var result = await healthCheckTcs.Task;
                                sw.Stop();
                                if (_isConnected)
                                    _consecutiveHealthCheckFailures = 0;
                                else
                                    _consecutiveHealthCheckFailures++;

                                shouldReconnect = !_isConnected && _consecutiveHealthCheckFailures >= HealthCheckFailureThreshold;
                                _logger?.Info($"[TechleaderPaymentService] HealthCheck TermCheck 응답 경과: {sw.ElapsedMilliseconds}ms (결과: 응답수신, connected={_isConnected}, failures={_consecutiveHealthCheckFailures})");
                                // ResponseCallback에서 이미 _isConnected를 업데이트했으므로 여기서는 확인만
                            }
                            catch
                            {
                                sw.Stop();
                                _logger?.Warn($"[TechleaderPaymentService] HealthCheck TermCheck 응답 경과: {sw.ElapsedMilliseconds}ms (결과: 응답 처리 예외, connected={_isConnected})");
                                _isConnected = false;
                                _consecutiveHealthCheckFailures++;
                                shouldReconnect = _consecutiveHealthCheckFailures >= HealthCheckFailureThreshold;
                            }
                            finally
                            {
                                lock (_healthCheckLock)
                                {
                                    _healthCheckTaskCompletionSource = null;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.Warn($"[TechleaderPaymentService] HealthCheck TermCheck 예외: {ex.Message}");
                        _isConnected = false;
                        _consecutiveHealthCheckFailures++;
                        shouldReconnect = _consecutiveHealthCheckFailures >= HealthCheckFailureThreshold;
                        lock (_healthCheckLock)
                        {
                            _healthCheckTaskCompletionSource = null;
                        }
                    }
                }
                else
                {
                    _isConnected = false;
                    _consecutiveHealthCheckFailures++;
                    shouldReconnect = _consecutiveHealthCheckFailures >= HealthCheckFailureThreshold;
                    _logger?.Warn($"[TechleaderPaymentService] HealthCheck TermCheck 응답 경과: N/A (TL3600 미생성, 단말기 미오픈, failures={_consecutiveHealthCheckFailures})");
                }
            }
            catch
            {
                _isConnected = false;
                _consecutiveHealthCheckFailures++;
                shouldReconnect = _consecutiveHealthCheckFailures >= HealthCheckFailureThreshold;
            }
            finally
            {
                try
                {
                    if (shouldReconnect)
                    {
                        _logger?.Warn($"[TechleaderPaymentService] HealthCheck reconnect triggered after {_consecutiveHealthCheckFailures} failures.");
                        TryReconnectSerialPort();
                    }
                }
                finally
                {
                    ExitHealthCheck();
                }
            }
        }

        /// <summary>
        /// 헬스체크 실패 등으로 단말기가 끊긴 경우 COM 포트를 닫고 다시 Open 합니다.
        /// </summary>
        private void TryReconnectSerialPort()
        {
            lock (_reconnectLock)
            {
                try
                {
                    if (IsTransactionRunning())
                    {
                        _logger?.Warn("[TechleaderPaymentService] Reconnect deferred: transaction is running.");
                        return;
                    }

                    _logger?.Info("[TechleaderPaymentService] COM reconnect: closing port...");
                    if (_tl3600 != null)
                    {
                        try { _tl3600.Close(); } catch (Exception ex) { _logger?.Warn($"[TechleaderPaymentService] Close during reconnect: {ex.Message}"); }
                        _tl3600 = null;
                    }

                    _tl3600 = new TL3600("ECP" + AppSettingsManager.ChargerSettings.StationId);
                    _tl3600.SetResponseCallback(new TL3600.ResponseCallback(this.ResponseCallback));
                    _tl3600.SetDbgLogEvent(new TL3600.DbgLogEvent(this.LogCallback));
                    bool portOpen = _tl3600.Open("COM" + AppSettingsManager.ChargerSettings.PaymentDeviceComPortNo, AppSettingsManager.ChargerSettings.PaymentDeviceBaudRate);
                    _isConnected = false;

                    if (!portOpen && _tl3600 != null)
                    {
                        try { _tl3600.Close(); } catch { }
                        _tl3600 = null;
                    }

                    _logger?.Info($"[TechleaderPaymentService] COM reconnect Open result: {portOpen} (단말 정상은 TermCheck 성공 후 IsConnected=true)");

                    if (portOpen)
                    {
                        _consecutiveHealthCheckFailures = 0;

                        // PerformHealthCheckAsync의 finally 안에서 호출되므로 ExitHealthCheck 이후에 돌도록 지연
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(100).ConfigureAwait(false);
                            await PerformHealthCheckAsync().ConfigureAwait(false);
                        });
                    }
                }
                catch (Exception ex)
                {
                    _isConnected = false;
                    _tl3600 = null;
                    _logger?.Warn($"[TechleaderPaymentService] COM reconnect exception: {ex.Message}");
                }
            }
        }

        private bool TryEnterHealthCheck()
        {
            lock (_healthCheckLock)
            {
                if (_isHealthCheckRunning)
                    return false;

                _isHealthCheckRunning = true;
                return true;
            }
        }

        private void ExitHealthCheck()
        {
            lock (_healthCheckLock)
            {
                _isHealthCheckRunning = false;
            }
        }

        private void BeginRequest(ActiveRequestType requestType)
        {
            lock (_requestStateLock)
            {
                _activeRequestType = requestType;
                _activeTransactionCount++;
            }
        }

        private void EndRequest(ActiveRequestType requestType)
        {
            lock (_requestStateLock)
            {
                if (_activeRequestType == requestType)
                    _activeRequestType = ActiveRequestType.None;

                if (_activeTransactionCount > 0)
                    _activeTransactionCount--;
            }
        }

        private bool IsTransactionRunning()
        {
            lock (_requestStateLock)
            {
                return _activeTransactionCount > 0;
            }
        }

        private ActiveRequestType GetActiveRequestType()
        {
            lock (_requestStateLock)
            {
                return _activeRequestType;
            }
        }

    }
}
