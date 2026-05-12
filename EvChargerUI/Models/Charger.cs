using EvChargerUI.Commons.Enum;
using EvChargerUI.Commons.Settings;
using EvChargerUI.Commons.Util;
using EvChargerUI.Domains;
using EvChargerUI.Services;
using EvChargerUI.Services.Database;
using EvChargerUI.Services.DspControl;
using EvChargerUI.Services.EvComm.HttpJsonRequest;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Forms.VisualStyles;
using System.Windows.Threading;
using static QRCoder.QRCodeGenerator;
using PaymentInfo = EvChargerUI.Domains.PaymentInfo;
using EvChargerUI.Services.FaultHandling;

namespace EvChargerUI.Models
{
    /// <summary>
    /// 중단된 충전 복구 완료 이벤트 인자
    /// </summary>
    public class InterruptedChargingRestoredEventArgs : EventArgs
    {
        public int ChannelNo { get; set; }
        public double ChargePower { get; set; }
        public int ActualChargeAmount { get; set; }
        public int UserSetChargeAmount { get; set; }
        public int CancelChargeAmount { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int ChargeTime { get; set; }
    }

    public class Charger : IEvCommToChargerDelegate, IDisposable
    {
        private readonly IEvCommService _evCommService;
        public IEvCommService EvCommService => _evCommService;
        private readonly IDspControlService _dspControlService;
        public IDspControlService DspControlService => _dspControlService;
        private readonly IPaymentService _paymentService;
        public bool IsPaymentServiceConnected => _paymentService?.IsAvailable ?? false;

        private DispatcherTimer _emergencyTimer;

        private readonly ChargerChannel[] _channels;

        public ChargerChannel[] Channels => _channels;

        private int _faultCount = 0;

        private DispatcherTimer _statusTimer;
        private DispatcherTimer _realtimeChargerTimer;
        private DispatcherTimer _priceScheduleTimer;
        private FileLogger _logger = ((App)Application.Current).AppLogger;
        private int _dumpInProgressFlag = 0; // 0: idle, 1: processing
        private volatile bool _isEmergencyTickBusy = false;
        private volatile bool _isRealtimeTickBusy = false;
        private readonly object _timerLock = new object();
        private readonly string _appStartDate;

        private static readonly Dictionary<string, DumpRoute> _dumpRouteMap = new Dictionary<string, DumpRoute>(StringComparer.OrdinalIgnoreCase)
        {
            { "0", new DumpRoute("chargers", "station/dChargers/", "station/chargers/", new [] { "create_date", "send_date" }) },
            { "1", new DumpRoute("chargingInfo", "station/dChargingInfo/", "station/charginginfo/", new [] { "create_date", "send_date", "start_date" }) },
            { "2", new DumpRoute("chargingStart", "station/dChargingStart/", "station/chargingstart/", new [] { "create_date", "send_date", "start_date" }) },
            { "3", new DumpRoute("chargingEnd", "station/dChargingEnd/", "station/chargingend/", new [] { "create_date", "end_date", "send_date", "start_date" }) },
            { "4", new DumpRoute("alarmHistory", "station/dAlarmHistory/", "station/alarmhistory/", new [] { "create_date", "alarm_date", "send_date" }) }
        };

        public Charger()
        {
            _appStartDate = DateTime.Now.ToString("yyyyMMddHHmmss");

            ChargerSettings cs = AppSettingsManager.ChargerSettings;
            StationId = cs.StationId;

            _evCommService = new EvCommService(AppSettingsManager.EvCommSettings.ServerBaseUrl, this);
            _evCommService.Open();

            int channelCount = 0;
            if (!string.IsNullOrEmpty(cs.LeftChannelChargerId)) channelCount++;
            if (!string.IsNullOrEmpty(cs.RightChannelChargerId)) channelCount++;

            _channels = new ChargerChannel[channelCount];
            if (!string.IsNullOrEmpty(cs.LeftChannelChargerId))
            {
                _channels[0] = new ChargerChannel(0, cs.StationId, cs.LeftChannelChargerId, cs.LeftQrCode);
                _channels[0].ChargingSelect = cs.LeftConnectorType;
            }
            if (!string.IsNullOrEmpty(cs.RightChannelChargerId))
            {
                _channels[1] = new ChargerChannel(1, cs.StationId, cs.RightChannelChargerId, cs.RightQrCode);
                _channels[1].ChargingSelect = cs.RightConnectorType;
            }

            switch (AppSettingsManager.ChargerSettings.ChargerManufacturerCode)
            {
                case "klinelex":
                    _dspControlService = new KlinelexDspControlService();
                    break;
                case "evsis":
                    _dspControlService = new EvsisDspControlService();
                    break;
                case "signet":
                    _dspControlService = new SignetDspControlService();
                    break;
                case "chaevi":
                    _dspControlService = new ChaeviDspControlService();
                    
                    break;
            }
            if(_dspControlService != null) 
                _dspControlService.Open();

            switch (AppSettingsManager.ChargerSettings.PaymentManufacturerCode)
            {
                case "nice":
                    _paymentService = new NicePaymentService();
                    break;
                case "techleader":
                    _paymentService = new TechleaderPaymentService();
                    break;
            }

            if (_paymentService != null)
                _paymentService.Open();

            _emergencyTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(0.5) };
            _emergencyTimer.Tick += async (s, e) =>
            {
                if (_isEmergencyTickBusy) return;

                _isEmergencyTickBusy = true;
                try
                {
                    // DSP 제어 서비스 호출을 백그라운드 스레드에서 실행하여 UI 스레드 블로킹 방지
                    bool isEmergency = await Task.Run(() => _dspControlService.GetEmergencyStatus());
                    if (isEmergency)
                    {
                        RaiseEmergency();
                    }
                    else
                    {
                        ClearEmergency();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"[EmergencyTimer] An unexpected error occurred: {ex.Message}");
                }
                finally
                {
                    _isEmergencyTickBusy = false;
                }
            };
            
            lock (_timerLock)
            {
                _emergencyTimer.Start();
            }

            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string backupExePath = Path.Combine(baseDir, "EvChargerUI_.exe");
                if (File.Exists(backupExePath))
                {
                    _logger.Info("[Startup Cleanup] Found old backup file. Deleting EvChargerUI_.exe.");
                    File.Delete(backupExePath);
                } 
            }
            catch (Exception ex)
            {
                _logger.Error($"[Startup Cleanup] Failed to delete backup file: {ex.Message}");
            }

            _updateCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _updateCheckTimer.Tick += PerformUpdate;
            
            lock (_timerLock)
            {
                _updateCheckTimer.Start();
            }

        }

        ~Charger()
        {
            Dispose();
        }

        public void EvCommInitialize()
        {
            _logger.Info("[EvCommInitialize] Start");
            
            // 1분간격 /station/chargers/{충전소ID}/{충전기ID}
            int statusUpdateInterval = AppSettingsManager.EvCommSettings.StatusUpdateInterval;
            int EVSE_Status = AppSettingsManager.EvCommSettings.EVSE_Status;
            string EVSE_PayYN = AppSettingsManager.EvCommSettings.EVSE_PayYN;
            string EVSE_Test = AppSettingsManager.EvCommSettings.EVSE_Test;
            
            // 초기 DSP 연결 상태 확인 및 플래그 초기화
            bool initialDspConnected = false;
            bool initialHasFault = false;
            try
            {
                if (_dspControlService == null)
                {
                    _logger.Warn("[EvCommInitialize] DspControlService is not configured (ChargerManufacturerCode not set).");
                }
                else
                {
                    initialDspConnected = _dspControlService.IsOpen();
                    // 각 채널에서 fault 상태 확인 (DSP 연결이 정상일 때만)
                    if (initialDspConnected)
                    {
                        foreach (ChargerChannel ch in _channels)
                        {
                            try
                            {
                                if (_dspControlService.GetFaultStatus(ch.ChannelNo))
                                {
                                    initialHasFault = true;
                                    break;
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.Error($"[EvCommInitialize] Error checking initial fault status for channel {ch.ChannelNo}: {ex.Message}");
                                // 초기 fault 체크 실패는 무시
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[EvCommInitialize] Error checking initial DSP status: {ex.Message}");
            }
            
            bool initialDspNormal = initialDspConnected && !initialHasFault;
            _isDspDisconnected = !initialDspNormal;
            AppSettingsManager.EvCommSettings.EVSE_DSP_Status = initialDspNormal ? 0 : 1;
            _logger.Info($"[EvCommInitialize] Initial DSP status: connected={initialDspConnected}, fault={initialHasFault}, normal={initialDspNormal}, EVSE_DSP_Status={AppSettingsManager.EvCommSettings.EVSE_DSP_Status}");
            
            // 초기 EVSE_Status 저장
            _lastEvseStatus = AppSettingsManager.EvCommSettings.EVSE_Status;
            
            _logger.Info($"[EvCommInitialize] StatusUpdateInterval={statusUpdateInterval}, EVSE_Status={EVSE_Status}");

            //EVSE_Status = 2;
            bool[] barrgetreadyState = new bool[5];
            bool[] barrgetStandbyState = new bool[5];
            bool[] barrGetrunState = new bool[5];

            if (statusUpdateInterval != 0)
            {
                _logger.Info("[EvCommInitialize] StatusUpdateInterval != 0, creating timers");
                _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(statusUpdateInterval * 60) };
                _statusTimer.Tick += (s, e) =>
                {
                    foreach (ChargerChannel ch in _channels)
                    {
                        // ChargerMode 값 사용 (Emergency나 DSP 연결 문제로 3이면 점검중 유지)
                        int mode = AppSettingsManager.EvCommSettings.ChargerMode;
                        _evCommService.SendChargerStatus(ch.StationId, ch.ChargerId, mode,
                            _dspControlService.GetChargingRunStatus(ch.ChannelNo) ? 2 : 1, 0,
                            _dspControlService.GetPlugCheckStatus(ch.ChannelNo) ? 2 : 1,
                            (uint)(_dspControlService.GetPowerMeter(ch.ChannelNo) * 1000), "");

                        barrgetreadyState[ch.ChannelNo] = _dspControlService.GetChargerReadyStatus(ch.ChannelNo);
                        barrgetStandbyState[ch.ChannelNo] = !_dspControlService.GetStandByStatus(ch.ChannelNo);
                        barrGetrunState[ch.ChannelNo] = _dspControlService.GetChargingRunStatus(ch.ChannelNo);

                        Debug.WriteLine($"채널 {ch.ChannelNo} 준비상태: {barrgetreadyState[ch.ChannelNo]}");
                        Debug.WriteLine($"채널 {ch.ChannelNo} 대기상태: {barrgetStandbyState[ch.ChannelNo]}");
                        Debug.WriteLine($"채널 {ch.ChannelNo} 충전상태: {barrGetrunState[ch.ChannelNo]}");
                    }
                };

                _statusTimer.Start();

                _realtimeChargerTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _realtimeChargerTimer.Tick += async (s, e) =>
                {
                    if (_isRealtimeTickBusy) return;

                    _isRealtimeTickBusy = true;
                    try
                    {
                        foreach (ChargerChannel ch in _channels)
                        {
                            EVSE_PayYN = AppSettingsManager.EvCommSettings.EVSE_PayYN;
                            PayYNStatusChanged?.Invoke(EVSE_PayYN == "N");

                            EVSE_Test = AppSettingsManager.EvCommSettings.EVSE_Test;
                            TestStatusChanged?.Invoke(EVSE_Test != "N");
                        
                            // EVSE_Status 변경 감지
                            int currentEvseStatus = AppSettingsManager.EvCommSettings.EVSE_Status;
                            if (_lastEvseStatus != currentEvseStatus)
                            {
                                _lastEvseStatus = currentEvseStatus;
                                EvseStatusChanged?.Invoke(currentEvseStatus);
                                _logger.Info($"[ReartimeChageeTimer] EVSE_Status changed to: {currentEvseStatus}");
                            }
                        
                            // DSP 연결 상태 체크 및 업데이트
                            bool isDspConnected = false;
                            bool hasFault = false;
                            try
                            {
                                // IsOpen() 체크
                                isDspConnected = _dspControlService.IsOpen();
                            
                                // 각 채널에서 fault 상태 확인 (DSP 연결이 정상일 때만)
                                if (isDspConnected)
                                {
                                    foreach (ChargerChannel channel in _channels)
                                    {
                                        try
                                        {
                                            if (_dspControlService.GetFaultStatus(channel.ChannelNo))
                                            {
                                                hasFault = true;
                                                _logger.Warn($"[ReartimeChageeTimer] Fault detected on channel {channel.ChannelNo}");
                                                break;
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.Error($"[ReartimeChageeTimer] Error checking fault status for channel {channel.ChannelNo}: {ex.Message}");
                                            // fault 체크 실패는 연결 끊김으로 간주하지 않음 (일시적 오류일 수 있음)
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.Error($"[ReartimeChageeTimer] Error checking DSP connection: {ex.Message}");
                                isDspConnected = false;
                            }
                        
                            // DSP 연결 끊김 또는 fault 발생 시 이상 상태로 처리
                            bool isDspNormal = isDspConnected && !hasFault;
                            int currentDspStatus = AppSettingsManager.EvCommSettings.EVSE_DSP_Status;
                            int newDspStatus = isDspNormal ? 0 : 1;
                        
                            // 상태가 변경되었거나, 플래그와 불일치하는 경우 업데이트
                            if (currentDspStatus != newDspStatus || (_isDspDisconnected && newDspStatus == 0) || (!_isDspDisconnected && newDspStatus == 1))
                            {
                                AppSettingsManager.EvCommSettings.EVSE_DSP_Status = newDspStatus;
                                AppSettingsManager.Save();
                                string reason = !isDspConnected ? "connection lost" : (hasFault ? "fault detected" : "normal");
                                _logger.Info($"[ReartimeChageeTimer] EVSE_DSP_Status updated: {currentDspStatus} -> {newDspStatus}, reason={reason}, isDspConnected={isDspConnected}, hasFault={hasFault}, _isDspDisconnected={_isDspDisconnected}");
                            
                                // DSP 연결 상태 변경 이벤트 발생
                                if (newDspStatus == 1 && !_isDspDisconnected)
                                {
                                    // DSP 연결 끊김 또는 fault 발생
                                    _isDspDisconnected = true;
                                    OnDspConnectionLost();
                                }
                                else if (newDspStatus == 0 && _isDspDisconnected)
                                {
                                    // DSP 연결 복구 및 fault 해제
                                    _isDspDisconnected = false;
                                    OnDspConnectionRestored();
                                }
                            }
                        
                            // Emergency 상태 체크 및 업데이트 (DSP 연결이 정상일 때만)
                            if (isDspConnected)
                            {
                                // DSP 제어 서비스 호출을 백그라운드 스레드에서 실행하여 UI 스레드 블로킹 방지
                                bool isEmergency = await Task.Run(() => _dspControlService.GetEmergencyStatus());
                                int currentEmergency = AppSettingsManager.EvCommSettings.EVSE_EmergencyStop;
                                int newEmergency = isEmergency ? 1 : 0;
                                if (currentEmergency != newEmergency)
                                {
                                    AppSettingsManager.EvCommSettings.EVSE_EmergencyStop = newEmergency;
                                    AppSettingsManager.Save();
                                    _logger.Info($"[ReartimeChageeTimer] EVSE_EmergencyStop updated: {currentEmergency} -> {newEmergency}");
                                }
                            }
                        
                            // 네트워크 상태 체크 및 ChargerMode 업데이트
                            // 네트워크 상태는 httpPostResponse에서 업데이트되지만, 주기적으로 ChargerMode를 체크하여 점검중 상태 전환
                            CheckAndUpdateChargerMode();
                        }
                    }
                    catch(Exception ex)
                    {
                        _logger.Error($"[ReartimeChageeTimer] An unexpected error occurred: {ex.Message}");
                    }
                    finally
                    {
                        _isRealtimeTickBusy = false;
                    }
                };
                
                lock (_timerLock)
                {
                    _realtimeChargerTimer.Start();
                }

                // 단가 스케줄 적용 타이머 (1분 정각 주기): DB에서 현재 활성 스케줄을 읽어 INI에 반영
                _priceScheduleTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
                _priceScheduleTimer.Tick += (s, e) =>
                {
                    // 매 Tick마다 정각(:00초)에 맞게 Interval 재조정
                    DateTime now = DateTime.Now;
                    int remainMs = (60 - now.Second) * 1000 - now.Millisecond;
                    if (remainMs < 100) remainMs += 60000;
                    _priceScheduleTimer.Interval = TimeSpan.FromMilliseconds(remainMs);

                    try
                    {
                        // station_id 기준으로 처리 (채널 구분 없이 첫 번째 채널의 stationId 사용)
                        string stationId = _channels.Length > 0 ? _channels[0].StationId : null;
                        AppSettingsManager.ApplyActivePriceSchedule(stationId);

                        // 충전 중인 채널의 CurrentUserUnitCost를 현재 시간대 단가로 갱신
                        int currentHour = now.Hour;
                        float newUnitCost = AppSettingsManager.ChargerOperationSettings.PriceForHour[currentHour];
                        foreach (ChargerChannel ch in _channels)
                        {
                            if (ch == null) continue;
                            if (ch.CurrentSequence != ChargeSequence.Charging) continue;

                            float oldUnitCost = ch.CurrentUserUnitCost;
                            if (Math.Abs(oldUnitCost - newUnitCost) < 0.01f) continue;

                            _logger.Info($"[PriceScheduleTimer] Channel {ch.ChannelNo}: UnitCost changed {oldUnitCost} → {newUnitCost}");

                            // 구간별 과금: 현재 구간 저장 후 새 구간 시작
                            try
                            {
                                var mainView = ((App)Application.Current).MainView;
                                if (mainView?.DataContext is ViewModels.MainViewModel mainViewModel)
                                {
                                    ViewModels.ChargerViewModel chargerVM = null;
                                    if (ch.ChannelNo == 0 && mainViewModel.LeftChargerView?.DataContext is ViewModels.ChargerViewModel leftVm)
                                        chargerVM = leftVm;
                                    else if (ch.ChannelNo == 1 && mainViewModel.RightChargerView?.DataContext is ViewModels.ChargerViewModel rightVm)
                                        chargerVM = rightVm;

                                    if (chargerVM != null)
                                    {
                                        double currentPowerMeter = chargerVM.PowerMeter;
                                        int currentSegmentCost = Commons.Util.MoneyUtil.TruncateWonUnit(
                                            (int)((currentPowerMeter - ch.CurrentSegmentStartPowerMeter) * oldUnitCost));
                                        int totalAccumulatedCost = ch.AccumulatedCostBeforeCurrentSegment + currentSegmentCost;

                                        ch.UnitCostChangeHistory.Add(new UnitCostChangeRecord
                                        {
                                            PowerMeter = currentPowerMeter,
                                            UnitCost = oldUnitCost,
                                            AccumulatedCost = totalAccumulatedCost
                                        });

                                        ch.CurrentSegmentStartPowerMeter = currentPowerMeter;
                                        ch.AccumulatedCostBeforeCurrentSegment = totalAccumulatedCost;

                                        _logger.Info($"[PriceScheduleTimer] Channel {ch.ChannelNo}: Segment saved. PowerMeter={currentPowerMeter:F4}, OldCost={oldUnitCost}, Accumulated={totalAccumulatedCost}");

                                        // 단가 업데이트 후 UI 재계산 트리거
                                        ch.CurrentUserUnitCost = newUnitCost;
                                        chargerVM.PowerMeter = currentPowerMeter;
                                    }
                                    else
                                    {
                                        ch.CurrentUserUnitCost = newUnitCost;
                                    }
                                }
                                else
                                {
                                    ch.CurrentUserUnitCost = newUnitCost;
                                }
                            }
                            catch (Exception ex2)
                            {
                                _logger.Error($"[PriceScheduleTimer] Channel {ch.ChannelNo}: Segment save error: {ex2.Message}");
                                ch.CurrentUserUnitCost = newUnitCost;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"[PriceScheduleTimer] Error: {ex.Message}");
                    }
                };

                // 다음 정각(:00초)까지 남은 시간으로 첫 Interval 설정
                {
                    DateTime now = DateTime.Now;
                    int remainMs = (60 - now.Second) * 1000 - now.Millisecond;
                    if (remainMs < 100) remainMs += 60000;
                    _priceScheduleTimer.Interval = TimeSpan.FromMilliseconds(remainMs);
                    _logger.Info($"[PriceScheduleTimer] 첫 Tick까지 {remainMs / 1000.0:F1}초 대기 (다음 정각 :00초 맞춤)");
                }

                lock (_timerLock)
                {
                    _priceScheduleTimer.Start();
                }

                // 시작 즉시 DB 활성 스케줄 반영 (타이머 첫 Tick은 다음 정각이므로 별도 즉시 실행)
                Task.Delay(500).ContinueWith(_ =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            string stationId = _channels.Length > 0 ? _channels[0].StationId : null;
                            AppSettingsManager.ApplyActivePriceSchedule(stationId);
                            _logger.Info("[EvCommInitialize] Initial price schedule applied from DB.");
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"[EvCommInitialize] Initial price schedule apply failed: {ex.Message}");
                        }
                    });
                });

                // 최초 실행시 서버 요청 /station/status/{충전소ID}/{충전기ID}
                // 타이머 시작 후 지연 실행 (DSP 초기화 대기)
                Task.Delay(5000).ContinueWith(_ =>
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            SendRTimeChargerStatus();
                            //EVSE_Status = 0;
                        });
                    });
            }
            else
            {
                _logger.Info("[EvCommInitialize] StatusUpdateInterval == 0, timers not created");
            }
            
            // 프로그램 시작 시 충전 중이었던 세션 복구 처리
            // MainViewModel 생성 후 실행되도록 약간 지연
            Task.Delay(2000).ContinueWith(_ =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    RestoreChargingSessions();
                });
            });
            
            _logger.Info("[EvCommInitialize] End");
        }

        /// <summary>
        /// 결제 단말기 헬스체크를 수행하는 공통 메서드
        /// (호환용) Nice는 타이머 헬스체크, Techleader는 동일. 여기서는 별도 동작 없음.
        /// </summary>
        public void CheckPaymentDeviceHealth()
        {
            //if (_paymentService != null)
            //{
            //    // 결제 서비스가 자체적으로 헬스체크를 수행하므로
            //    // 여기서는 IsConnected 상태만 확인
            //    // 실제 헬스체크는 각 PaymentService의 타이머에서 수행됨
            //    bool isAvailable = IsPaymentServiceConnected;
            //}
        }

        private sealed class ChargerStatusSnapshot
        {
            public string ResponseDate { get; set; }
            public string UiVer { get; set; }
            public string ChargerStatus { get; set; }
            public string RfStatus { get; set; }
            public string IcStatus { get; set; }
            public string AppStartDate { get; set; }
            public string StopButtonStatus { get; set; }
            public string ChargingMode { get; set; }
            public string ElectricityMeterMode { get; set; }
            public string UiMode { get; set; }
            public string PowerModule { get; set; }
            public string FreeSpace { get; set; }
            public string AvaMem { get; set; }
            public string TimeLimitYn { get; set; }
            public string TimeLimitValue { get; set; }
            public string SunYn { get; set; }
            public string TestYn { get; set; }
            public string PayYn { get; set; }
            public string AutoSetYn { get; set; }
            public string BacklightDay { get; set; }
            public string BacklightNight { get; set; }
            public string VolumeDay { get; set; }
            public string VolumeNight { get; set; }
            public string VolumeMovieDay { get; set; }
            public string VolumeMovieNight { get; set; }
            public string ChargerFirmware { get; set; }
            public string NoticeCnt { get; set; }
            public string UpdateFileCount { get; set; }
            public string SdInfo { get; set; }
            public string SdType { get; set; }
            public string RfErrCnt { get; set; }
            public string IcErrCnt { get; set; }
            public string ChErrCnt { get; set; }
            public string MovieSize { get; set; }
            public string IcVer { get; set; }
            public string RfVer { get; set; }
            public string ChargerVer { get; set; }
            public string KevaVer { get; set; }
            public string KevaRemoteVer { get; set; }
            public string SystemDate { get; set; }
            public string LcdIp { get; set; }
            public string CurrentUnitCost { get; set; }
        }

        private ChargerStatusSnapshot BuildChargerStatusSnapshot(ChargerChannel ch, DateTime now, bool uiVerWithPrefix)
        {
            string appVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            string uiVer = uiVerWithPrefix ? $"VER {appVersion}" : appVersion;
            string responseDate = now.ToString("yyyyMMddHHmmss");
            string systemDate = now.ToString("yyyyMMddHHmmss");
            string appStartDate = _appStartDate;

            int dspStatus = AppSettingsManager.EvCommSettings.EVSE_DSP_Status;
            int emergencyStatus = AppSettingsManager.EvCommSettings.EVSE_EmergencyStop;

            string chargerStatus = dspStatus == 1 ? "1" : "0";
            string rfStatus = "0";
            string icStatus = "0";

            if (_paymentService != null && !IsPaymentServiceConnected)
            {
                rfStatus = "1";
                icStatus = "1";
            }

            string stopButtonStatus = emergencyStatus == 1 ? "1" : "0";
            string chargingMode = "2";
            string electricityMeterMode = "2";

            if (dspStatus == 1)
            {
                chargerStatus = "1";
                stopButtonStatus = "0";
                chargingMode = "2";
                electricityMeterMode = "2";
            }
            else
            {
                CheckAndUpdateChargerMode();

                if (_dspControlService.GetFaultStatus(ch.ChannelNo))
                {
                    chargerStatus = "1";
                }

                if (_dspControlService.GetChargingRunStatus(ch.ChannelNo))
                    chargingMode = "1";
                else if (_dspControlService.GetStandByStatus(ch.ChannelNo))
                    chargingMode = "0";
                else
                    chargingMode = "2";

                electricityMeterMode = _dspControlService.IsPmsConnected()
                    ? (_dspControlService.GetFaultCode(ch.ChannelNo) != "904" ? "1" : "0")
                    : "0";
            }

            int chargerModeValue = AppSettingsManager.EvCommSettings.ChargerMode;
            string uiMode = chargerModeValue == 3 ? "2" : (chargerModeValue == 2 ? "3" : "1");
            //string powerModule = "0000000000000000";
            string powerModule = _dspControlService.GetPowerModuleStatusBits(ch.ChannelNo);

            string freeSpace = "0";
            try
            {
                string rootPath = Path.GetPathRoot(AppDomain.CurrentDomain.BaseDirectory);
                if (!string.IsNullOrEmpty(rootPath))
                {
                    DriveInfo drive = new DriveInfo(rootPath);
                    freeSpace = (drive.AvailableFreeSpace / 1024).ToString();
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[BuildChargerStatusSnapshot] Failed to read free space: {ex.Message}");
            }

            string avaMem = "0";
            try
            {
                long availableBytes = GetAvailablePhysicalMemoryBytes();
                avaMem = availableBytes.ToString();
            }
            catch (Exception ex)
            {
                _logger.Error($"[BuildChargerStatusSnapshot] Failed to read available memory: {ex.Message}");
            }

            string timelimitYn = AppSettingsManager.ChargerOperationSettings.IsChargeTimeLimited ? "Y" : "N";
            string timelimitValue = AppSettingsManager.ChargerOperationSettings.ChargeLimitTime.ToString();
            string testYn = AppSettingsManager.ChargerOperationSettings.IsTestOperation ? "Y" : "N";
            string payYn = AppSettingsManager.ChargerOperationSettings.IsPaymentApplied ? "Y" : "N";
            string sunYn = "N";
            string autosetYn = "Y";

            string volumeDay = AppSettingsManager.SoundVolumeSettings.LevelForDay.ToString();
            string volumeNight = AppSettingsManager.SoundVolumeSettings.LevelForNight.ToString();
            string volumemovieDay = volumeDay;
            string volumemovieNight = volumeNight;
            string backlightDay = AppSettingsManager.DisplayBrightnessSettings.LevelForDay.ToString();
            string backlightNight = AppSettingsManager.DisplayBrightnessSettings.LevelForNight.ToString();

            string chargerFirmware = _dspControlService.GetChargerFirmwareVersion();
            if (string.IsNullOrEmpty(chargerFirmware))
                chargerFirmware = "1.0.0";

            string noticeCnt = "5";
            string updateFileCount = "0";
            string sdInfo = "1";
            string sdType = "0";
            string rferrCnt = "0";
            string icerrCnt = "0";
            string cherrCnt = "0";
            string movieSize = "0";
            string icVer = null;
            string rfVer = null;
            string chargerVer = null;
            string kevaVer = null;
            string kevaremoteVer = null;

            string lcdIp = GetLocalIPAddress();
            float currentUnitCost = AppSettingsManager.ChargerOperationSettings.PriceForHour[now.Hour];
            string currentUnitCostStr = currentUnitCost.ToString("F2");

            return new ChargerStatusSnapshot
            {
                ResponseDate = responseDate,
                UiVer = uiVer,
                ChargerStatus = chargerStatus,
                RfStatus = rfStatus,
                IcStatus = icStatus,
                AppStartDate = appStartDate,
                StopButtonStatus = stopButtonStatus,
                ChargingMode = chargingMode,
                ElectricityMeterMode = electricityMeterMode,
                UiMode = uiMode,
                PowerModule = powerModule,
                FreeSpace = freeSpace,
                AvaMem = avaMem,
                TimeLimitYn = timelimitYn,
                TimeLimitValue = timelimitValue,
                SunYn = sunYn,
                TestYn = testYn,
                PayYn = payYn,
                AutoSetYn = autosetYn,
                BacklightDay = backlightDay,
                BacklightNight = backlightNight,
                VolumeDay = volumeDay,
                VolumeNight = volumeNight,
                VolumeMovieDay = volumemovieDay,
                VolumeMovieNight = volumemovieNight,
                ChargerFirmware = chargerFirmware,
                NoticeCnt = noticeCnt,
                UpdateFileCount = updateFileCount,
                SdInfo = sdInfo,
                SdType = sdType,
                RfErrCnt = rferrCnt,
                IcErrCnt = icerrCnt,
                ChErrCnt = cherrCnt,
                MovieSize = movieSize,
                IcVer = icVer,
                RfVer = rfVer,
                ChargerVer = chargerVer,
                KevaVer = kevaVer,
                KevaRemoteVer = kevaremoteVer,
                SystemDate = systemDate,
                LcdIp = lcdIp,
                CurrentUnitCost = currentUnitCostStr
            };
        }

        /// <summary>
        /// 실시간 충전기 상태를 서버로 전송
        /// </summary>
        public void SendRTimeChargerStatus()
        {
            DateTime now = DateTime.Now;
            
            // 각 채널에 대해 RTimeChargerStatus 전송
            foreach (ChargerChannel ch in _channels)
            {
                var snapshot = BuildChargerStatusSnapshot(ch, now, uiVerWithPrefix: true);
                
                // 서버로 RTimeChargerStatus 전송
                _evCommService.SendRTimeChargerStatus(
                    ch.StationId,
                    ch.ChargerId,
                    snapshot.ResponseDate,
                    snapshot.UiVer,
                    snapshot.ChargerStatus,
                    snapshot.RfStatus,
                    snapshot.IcStatus,
                    snapshot.AppStartDate,
                    snapshot.StopButtonStatus,
                    snapshot.ChargingMode,
                    snapshot.ElectricityMeterMode,
                    snapshot.UiMode,
                    snapshot.PowerModule,
                    snapshot.FreeSpace,
                    snapshot.AvaMem,
                    snapshot.TimeLimitYn,
                    snapshot.TimeLimitValue,
                    snapshot.TestYn,
                    snapshot.PayYn,
                    snapshot.VolumeDay,
                    snapshot.VolumeNight,
                    snapshot.VolumeMovieDay,
                    snapshot.VolumeMovieNight,
                    snapshot.ChargerFirmware,
                    snapshot.NoticeCnt,
                    snapshot.SystemDate,
                    snapshot.LcdIp,
                    snapshot.CurrentUnitCost
                );
            }
        
        }
        
        /// <summary>
        /// 로컬 네트워크 IP 주소 가져오기
        /// </summary>
        private string GetLocalIPAddress()
        {
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        return ip.ToString();
                    }
                }
            }
            catch
            {
                // IP 가져오기 실패 시 기본값 반환
            }
            return "127.0.0.1";
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        private long GetAvailablePhysicalMemoryBytes()
        {
            MEMORYSTATUSEX memoryStatus = new MEMORYSTATUSEX();
            memoryStatus.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));

            if (!GlobalMemoryStatusEx(ref memoryStatus))
            {
                return 0;
            }

            if (memoryStatus.ullAvailPhys > long.MaxValue)
            {
                return long.MaxValue;
            }

            return (long)memoryStatus.ullAvailPhys;
        }

        public void Dispose()
        {
            lock (_timerLock)
            {
                _emergencyTimer?.Stop();
                _statusTimer?.Stop();
                _realtimeChargerTimer?.Stop();
                _updateCheckTimer?.Stop();
                _priceScheduleTimer?.Stop();
            }
            
            _evCommService?.Close();
            _paymentService?.Close();
            _dspControlService?.Close();
        }


        public string StationId { get; private set; }
        public bool IsPaymentRequired { get; set; }

        public void SendChargingStart(int channelNo)
        {
            ChargerChannel ch = _channels[channelNo];

            string payType = "C";
            switch (ch.PaymentMethod)
            {
                case PaymentMethod.RfCard:
                    payType = "N";
                    break;
                case PaymentMethod.IcCard:
                case PaymentMethod.SamsungPay:
                    payType = "Y";
                    break;
                case PaymentMethod.QrCode:
                    payType = "Q";
                    break;
            }

            // previous_trno: QR 결제일 때는 tid, 신용카드일 때는 AuthNum, 그 외는 "-9999"
            string previousTrno = "-9999";
            if (ch.PaymentMethod == PaymentMethod.QrCode && !string.IsNullOrEmpty(ch.QrTid))
            {
                previousTrno = ch.QrTid;
            }
            else if (ch.PrePaymentInfo != null)
            {
                previousTrno = ch.PrePaymentInfo.AuthNum;
            }
            
            _evCommService.SendChargingStart(ch.StationId, ch.ChargerId, DateTime.Now.ToString("yyyyMMddHHmmss"), 
                ch.ChargingStartTime.ToString("yyyyMMddHHmmss"),
                String.IsNullOrEmpty(ch.MembershipNo) ? "-9999" : ch.MembershipNo,
                previousTrno,
                ch.PrePaymentInfo != null ? ch.PrePaymentInfo.PayDate : "-9999",
               payType,AppSettingsManager.ChargerOperationSettings.IsPaymentApplied ? "Y" : "N",  ch.ChargingSelect,
                (uint)(_dspControlService.GetPowerMeter(channelNo) * 1000),
                ch.PrePaymentInfo != null ? Int32.Parse(ch.PrePaymentInfo.TotalCost) : 0,
                 (int)(_dspControlService.GetVoltage(channelNo) * 10),
                (int)(_dspControlService.GetCurrent(channelNo) * 10), 
                (_dspControlService.GetRemainedMinute(channelNo) * 60).ToString(),
                ch.CurrentUserUnitCost.ToString(), _dspControlService.GetSoc(channelNo).ToString(),
                ch.OrderNo
                );

            // 충전 시작 시 즉시 세션 저장
            try
            {
                double currentEnergy = _dspControlService.GetPowerMeter(channelNo);
                
                // BasePowerMeter가 0이면 현재 전력량을 BasePowerMeter로 설정
                if (ch.BasePowerMeter <= 0)
                {
                    ch.BasePowerMeter = currentEnergy;
                    _logger.Info($"[SendChargingStart] Channel {channelNo}: BasePowerMeter was 0, setting to currentEnergy={currentEnergy}");
                }
                
                ChargingSessionManager.SaveSession(ch, currentEnergy, "Charging");
                _logger.Info($"[SendChargingStart] Channel {channelNo}: Session saved - StartEnergy={ch.BasePowerMeter}, CurrentEnergy={currentEnergy}, Difference={currentEnergy - ch.BasePowerMeter:F4}");
            }
            catch (Exception ex)
            {
                _logger.Error($"[SendChargingStart] Failed to save session for channel {channelNo}: {ex.Message}");
            }
        }

        public void SendChargingProgress(int channelNo)
        {
            ChargerChannel ch = _channels[channelNo];

            string payType = "C";
            switch (ch.PaymentMethod)
            {
                case PaymentMethod.RfCard:
                    payType = "N";
                    break;
                case PaymentMethod.IcCard:
                case PaymentMethod.SamsungPay:
                    payType = "Y";
                    break;
                case PaymentMethod.QrCode:
                    payType = "Q";
                    break;
            }

            // previous_trno: QR 결제일 때는 tid, 신용카드일 때는 AuthNum, 그 외는 "-9999"
            string previousTrno = "-9999";
            if (ch.PaymentMethod == PaymentMethod.QrCode && !string.IsNullOrEmpty(ch.QrTid))
            {
                previousTrno = ch.QrTid;
            }
            else if (ch.PrePaymentInfo != null)
            {
                previousTrno = ch.PrePaymentInfo.AuthNum;
            }
            
            _evCommService.SendChargingInfo(ch.StationId, ch.ChargerId, DateTime.Now.ToString("yyyyMMddHHmmss"),
                ch.ChargingStartTime.ToString("yyyyMMddHHmmss"),
                String.IsNullOrEmpty(ch.MembershipNo) ? "-9999" : ch.MembershipNo,
                previousTrno,
                ch.PrePaymentInfo != null ? ch.PrePaymentInfo.PayDate : "-9999",
               payType, AppSettingsManager.ChargerOperationSettings.IsPaymentApplied ? "Y" : "N", ch.ChargingSelect,
                (uint)(_dspControlService.GetPowerMeter(channelNo) * 1000),
                ch.PrePaymentInfo != null ? Int32.Parse(ch.PrePaymentInfo.TotalCost) : 0,
                 (int)(_dspControlService.GetVoltage(channelNo) * 10),
                (int)(_dspControlService.GetCurrent(channelNo) * 10),
                (_dspControlService.GetRemainedMinute(channelNo) * 60).ToString(),
                Commons.Util.ChargingAmountUtil.ToRoundedChargeW(Math.Max(0.0, _dspControlService.GetPowerMeter(channelNo) - ch.BasePowerMeter)),
                (int)(Math.Max(0.0, _dspControlService.GetPowerMeter(channelNo) - ch.BasePowerMeter) * ch.CurrentUserUnitCost),
                ch.CurrentUserUnitCost.ToString(), _dspControlService.GetSoc(channelNo).ToString(),
                ch.OrderNo
                );

            // 충전 중일 때 주기적으로 세션 상태 저장
            if (ChargingSessionManager.IsCharging(ch))
            {
                try
                {
                    double currentEnergy = _dspControlService.GetPowerMeter(channelNo);

                    // "처음 전력량(BasePowerMeter)"가 0이면, 0을 기준으로 고정하지 말고
                    // 최초로 들어오는 유효 현재 전력량(>0)을 기준값으로 덮어쓴다.
                    if (ch.BasePowerMeter <= 0 && currentEnergy > 0)
                    {
                        ch.BasePowerMeter = currentEnergy;
                        _logger.Info($"[SendChargingProgress] Channel {channelNo}: BasePowerMeter was 0, set to currentEnergy={currentEnergy}");
                    }

                    // 구간별 과금 기준(CurrentSegmentStartPowerMeter)도 첫 유효 delta에서 1회 보정
                    double currentDelta = Math.Max(0.0, currentEnergy - ch.BasePowerMeter);
                    if (ch.CurrentSegmentStartPowerMeter <= 0 && currentDelta > 0)
                    {
                        ch.CurrentSegmentStartPowerMeter = currentDelta;
                    }

                    ChargingSessionManager.SaveSession(ch, currentEnergy, "Charging");
                }
                catch (Exception ex)
                {
                    _logger.Error($"[SendChargingProgress] Failed to save session for channel {channelNo}: {ex.Message}");
                }
            }
        }

        public void SendChargingEnd(int channelNo, int chargeEndType)
        {
            ChargerChannel ch = _channels[channelNo];

            string payType = "C";
            switch (ch.PaymentMethod)
            {
                case PaymentMethod.RfCard:
                    payType = "N";
                    break;
                case PaymentMethod.IcCard:
                case PaymentMethod.SamsungPay:
                    payType = "Y";
                    break;
                case PaymentMethod.QrCode:
                    payType = "Q";
                    break;
            }

            // previous_trno: QR 결제일 때는 tid, 신용카드일 때는 AuthNum, 그 외는 "-9999"
            string previousTrno = "-9999";
            if (ch.PaymentMethod == PaymentMethod.QrCode && !string.IsNullOrEmpty(ch.QrTid))
            {
                previousTrno = ch.QrTid;
            }
            else if (ch.PrePaymentInfo != null)
            {
                previousTrno = ch.PrePaymentInfo.AuthNum;
            }
            
            _evCommService.SendChargingEnd(ch.StationId, ch.ChargerId, DateTime.Now.ToString("yyyyMMddHHmmss"),
                ch.ChargingStartTime.ToString("yyyyMMddHHmmss"),
                String.IsNullOrEmpty(ch.MembershipNo) ? "-9999" : ch.MembershipNo,
                previousTrno,
                ch.PrePaymentInfo != null ? ch.PrePaymentInfo.PayDate : "-9999",
               payType, AppSettingsManager.ChargerOperationSettings.IsPaymentApplied ? "Y" : "N", ch.ChargingSelect,
                (uint)(_dspControlService.GetPowerMeter(channelNo) * 1000),
                ch.PrePaymentInfo != null ? Int32.Parse(ch.PrePaymentInfo.TotalCost) : 0,
                 (int)(_dspControlService.GetVoltage(channelNo) * 10),
                (int)(_dspControlService.GetCurrent(channelNo) * 10),
                ch.ChargeTime, 
                (uint)ch.FinalPowerMeter,
                chargeEndType, /* 충전 완료 구분*/
                ch.ChargingEndTime.ToString("yyyyMMddHHmmss"),
                ch.ChargeAmount,
                ch.CancelChargeAmount,
                "" /* 포인트 사용구분*/,
                DateTime.Now.ToString("yyyyMMddHHmmss"), /* 취소시간 */
                "Y", /* 취소결과*/
                ch.CurrentUserUnitCost.ToString(), _dspControlService.GetSoc(channelNo).ToString(),
                ch.OrderNo
                );

            // 정상 종료: 세션 파일 삭제
            try
            {
                ChargingSessionManager.DeleteSession(channelNo);
            }
            catch (Exception ex)
            {
                _logger.Error($"[SendChargingEnd] Failed to delete session for channel {channelNo}: {ex.Message}");
            }
        }

        public void SendChargingEndAlarm(int channelNo)
        {
            ChargerChannel ch = _channels[channelNo];
            if (!string.IsNullOrEmpty(ch.ChargeEndCallbackPhoneNumber))
            {
                // _evCommService.SendSendSMS(ch.StationId, ch.ChargerId, ch.ChargeEndCallbackPhoneNumber, "4", "SMS", ch.StationId, ch.ChargerId,
                //     (_dspControlService.GetPowerMeter(channelNo) - ch.BasePowerMeter).ToString("F1"), ch.ChargeAmount.ToString(),
                //     (ch.ChargingEndTime - ch.ChargingStartTime).ToString(@"hh\:mm"));
                
                _evCommService.SendSendSMS(ch.StationId, ch.ChargerId, ch.ChargeEndCallbackPhoneNumber, "4", "SMS", ch.StationId, ch.ChargerId,
                    (ch.FinalPowerMeter / 1000.0).ToString("F2"), ch.ChargeAmount.ToString(),
                    (ch.ChargingEndTime - ch.ChargingStartTime).ToString(@"hh\:mm\:ss"));
                // 알림 전송 후 전화번호 초기화
                ch.ChargeEndCallbackPhoneNumber = null;
            }

        }

        public bool SendGetResvStation(int channelNo, out string phoneNo, out string reservationNo)
        {
            ChargerChannel ch = _channels[channelNo];
            return _evCommService.SendResvStation(ch.StationId, out phoneNo, out reservationNo);
        }

        public bool SendSendSmsResvInfo(int channelNo, string phoneNo, string reservationNo)
        {
            ChargerChannel ch = _channels[channelNo];
            return _evCommService.SendSendSMS(ch.StationId, ch.ChargerId, phoneNo, "1", "SMS", ch.StationId, ch.ChargerId, reservationNo, null, null);
        }

        public bool SendSendSmsResvCancel(int channelNo, string phoneNo)
        {
            ChargerChannel ch = _channels[channelNo];
            return _evCommService.SendSendSMS(ch.StationId, ch.ChargerId, phoneNo, "2", "SMS", null, ch.ChargerId, null, null, null);
        }

        public bool SendReservationCharger(int channelNo, string phoneNo)
        {
            ChargerChannel ch = _channels[channelNo];
            string reservationNo = null;
            bool retVal = _evCommService.SendInsertResv(ch.StationId, ch.ChargerId, phoneNo, out reservationNo);
            if (retVal)
            {
                _evCommService.SendSendSMS(ch.StationId, ch.ChargerId, phoneNo, "5", "SMS", ch.StationId, reservationNo, null, null, null);
            }
            return retVal;
        }

        public bool SendAuthReservation(int channelNo, string phoneNo, string reservationNo)
        {
            ChargerChannel ch = _channels[channelNo];

            return _evCommService.SendAuthResv(ch.StationId, phoneNo + reservationNo);

        }

        public bool SendCancelReservation(int channelNo, string phoneNo)
        {
            ChargerChannel ch = _channels[channelNo];

            return _evCommService.SendCancelResv(ch.StationId, DateTime.Now.ToString("yyyyMMddHHmmss"), phoneNo);
        }


        #region EvCommToChargerDelegate Implementation
        
        public event EventHandler<QrChargingStartedEventArgs> QrChargingStarted;
        public event EventHandler<QrChargingEndedEventArgs> QrChargingEnded;

        public class QrChargingStartedEventArgs : EventArgs
        {
            public string StationId { get; set; }
            public string ChargerId { get; set; }
            public string Tid { get; set; }
            public string ChargerType { get; set; }
        }

        public class QrChargingEndedEventArgs : EventArgs
        {
            public string StationId { get; set; }
            public string ChargerId { get; set; }
            public string Tid { get; set; }
        }

        public bool ChangeChargerStatus(string stationId, string chargerId, string status, out string errorCode)
        {
            throw new NotImplementedException();
        }

        public bool ChangeChargingTimeLimitInfo(string stationId, string chargerId, bool timeLimitOnFlag, int minute, out string errorCode)
        {
            AppSettingsManager.ChargerOperationSettings.IsChargeTimeLimited = timeLimitOnFlag;
            AppSettingsManager.ChargerOperationSettings.ChargeLimitTime = minute;

            errorCode = null;
            return true;
        }

        public bool ChangeChargingUnitPrices(string stationId, string chargerId, double[] prices, string applyDate, string endDate, string createDate, out string errorCode)
        {
            // 현재 시간대 단가 변경 이력용 - 덮어쓰기 전에 보관
            int currentHour = DateTime.Now.Hour;
            float oldPriceAtCurrentHour = AppSettingsManager.ChargerOperationSettings.PriceForHour[currentHour];
            float newPriceAtCurrentHour = (currentHour < prices.Length) ? (float)prices[currentHour] : oldPriceAtCurrentHour;

            // 기존 단가 vs 신규 단가 비교 로그
            var changedHours = new System.Text.StringBuilder();
            for (int i = 0; i < prices.Length; i++)
            {
                float oldVal = AppSettingsManager.ChargerOperationSettings.PriceForHour[i];
                float newVal = (float)prices[i];
                if (Math.Abs(oldVal - newVal) > 0.001f)
                {
                    changedHours.Append($"  H{i:D2}: {oldVal} → {newVal}\n");
                }
            }

            if (changedHours.Length > 0)
            {
                _logger.Info($"[ChangeChargingUnitPrices] 단가 변경 감지 stationId={stationId} chargerId={chargerId} applyDate={applyDate} endDate={endDate} createDate={createDate}\n{changedHours}");
            }
            else
            {
                _logger.Info($"[ChangeChargingUnitPrices] 단가 변경 없음 stationId={stationId} chargerId={chargerId} createDate={createDate}");
            }

            for (int i = 0; i < prices.Length; i++)
            {
                AppSettingsManager.ChargerOperationSettings.PriceForHour[i] = (float) prices[i];
            }
            AppSettingsManager.Save();

            // DB에 적용 시점 기록
            try
            {
                string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "evcharger.db");
                var sqlite = new SqliteService(dbPath);
                sqlite.Initialize();
                var repo = new PriceScheduleRepository(sqlite);
                repo.MarkAsApplied(stationId, createDate);
                _logger.Info($"[ChangeChargingUnitPrices] Marked schedule as applied. stationId={stationId}, createDate={createDate}");

                // 현재 시간대 단가 변경 이력 기록
                if (Math.Abs(oldPriceAtCurrentHour - newPriceAtCurrentHour) > 0.001f)
                {
                    string changeTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    sqlite.ExecuteNonQuery(
                        @"INSERT INTO PriceChangeLog (station_id, hour_index, old_price, new_price, change_source, changed_at)
                          VALUES (@sid, @hour, @old, @new, @src, @at)",
                        new SQLiteParameter("@sid", stationId),
                        new SQLiteParameter("@hour", currentHour),
                        new SQLiteParameter("@old", Math.Round((double)oldPriceAtCurrentHour, 1)),
                        new SQLiteParameter("@new", Math.Round((double)newPriceAtCurrentHour, 1)),
                        new SQLiteParameter("@src", "INI"),
                        new SQLiteParameter("@at", changeTime));
                    _logger.Info($"[ChangeChargingUnitPrices] PriceChangeLog 기록 H{currentHour:D2}: {oldPriceAtCurrentHour} → {newPriceAtCurrentHour}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[ChangeChargingUnitPrices] Failed to mark schedule as applied: {ex.Message}");
            }

            errorCode = null;
            return true;
        }

        public bool ChangeDisplayBrightness(string stationId, string chargerId, int dayLevel, int nightLevel, out string errorCode)
        {
            AppSettingsManager.DisplayBrightnessSettings.LevelForDay = dayLevel;
            AppSettingsManager.DisplayBrightnessSettings.LevelForNight = nightLevel;
            AppSettingsManager.Save();

            errorCode = null;
            return true;
        }

        public bool ChangePaymentRequiredFlag(string stationId, string chargerId, bool flag, out string errorCode)
        {
            AppSettingsManager.ChargerOperationSettings.IsPaymentApplied = flag;
            AppSettingsManager.Save();

            errorCode = null;
            return true;

        }

        public bool ChangeSoundVolume(string stationId, string chargerId, int dayLevel, int nightLevel, out string errorCode)
        {
            AppSettingsManager.SoundVolumeSettings.LevelForDay = dayLevel;
            AppSettingsManager.SoundVolumeSettings.LevelForNight = nightLevel;
            AppSettingsManager.Save();

            errorCode = null;
            return true;
        }

        public bool ChangeTestMode(string stationId, string chargerId, bool testModeOnFlag, out string errorCode)
        {
            AppSettingsManager.ChargerOperationSettings.IsTestOperation = testModeOnFlag;
            AppSettingsManager.Save();

            errorCode = null;
            return true;
        }

        public JObject GetChargerInfo(string stationId, string chargerId, out string errorCode)
        {
            ChargerChannel ch = null;
            foreach (var channel in _channels)
            {
                if (channel.ChargerId == chargerId)
                {
                    ch = channel;
                    break;
                }
            }

            if (ch == null)
            {
                errorCode = "CHARGER_NOT_FOUND";
                _logger.Error($"[GetChargerInfo] Charger not found with id: {chargerId}");
                return new JObject
                {
                    ["result"] = "fail",
                    ["error_code"] = errorCode,
                    ["response_date"] = DateTime.Now.ToString("yyyyMMddHHmmss")
                };
            }

            var snapshot = BuildChargerStatusSnapshot(ch, DateTime.Now, uiVerWithPrefix: false);

            errorCode = null;
            return new JObject
            {
                 //ch.StationId,
                    //ch.ChargerId
                ["response_date"] = snapshot.ResponseDate,
                ["station_id"] = ch.StationId,
                ["charger_id"] = ch.ChargerId,
                ["ui_ver"] = snapshot.UiVer,
                ["charger_status"] = snapshot.ChargerStatus,
                ["rf_status"] = snapshot.RfStatus,
                ["ic_status"] = snapshot.IcStatus,
                ["app_start_date"] = snapshot.AppStartDate,
                ["stop_button_status"] = snapshot.StopButtonStatus,
                ["charging_mode"] = snapshot.ChargingMode,
                ["electricity_meter_mode"] = snapshot.ElectricityMeterMode,
                ["ui_mode"] = snapshot.UiMode,
                ["power_module"] = snapshot.PowerModule,
                ["free_space"] = snapshot.FreeSpace,
                ["ava_mem"] = snapshot.AvaMem,
                ["timelimit_yn"] = snapshot.TimeLimitYn,
                ["timelimit_value"] = snapshot.TimeLimitValue,
                ["sun_yn"] = snapshot.SunYn,
                ["test_yn"] = snapshot.TestYn,
                ["pay_yn"] = snapshot.PayYn,
                ["autoset_yn"] = snapshot.AutoSetYn,
                ["backlight_day"] = snapshot.BacklightDay,
                ["backlight_night"] = snapshot.BacklightNight,
                ["volume_day"] = snapshot.VolumeDay,
                ["volume_night"] = snapshot.VolumeNight,
                ["volumemovie_day"] = snapshot.VolumeMovieDay,
                ["volumemovie_night"] = snapshot.VolumeMovieNight,
                ["charger_firmware"] = snapshot.ChargerFirmware,
                ["notice_cnt"] = snapshot.NoticeCnt,
                ["update_file_count"] = snapshot.UpdateFileCount,
                ["sd_info"] = snapshot.SdInfo,
                ["sd_type"] = snapshot.SdType,
                ["rferr_cnt"] = snapshot.RfErrCnt,
                ["icerr_cnt"] = snapshot.IcErrCnt,
                ["cherr_cnt"] = snapshot.ChErrCnt,
                ["movie_size"] = snapshot.MovieSize,
                ["ic_ver"] = snapshot.IcVer,
                ["rf_ver"] = snapshot.RfVer,
                ["charger_ver"] = snapshot.ChargerVer,
                ["keva_ver"] = snapshot.KevaVer,
                ["kevaremote_ver"] = snapshot.KevaRemoteVer,
                ["system_date"] = snapshot.SystemDate,
                ["lcd_ip"] = snapshot.LcdIp,
                ["current_unit_cost"] = snapshot.CurrentUnitCost
            };
        }
        public JObject GetNonMemberUnitCost(string stationId, string chargerId)
        {
            JObject response = _evCommService.SendCheckCurrentUnitCost(stationId, chargerId);

            return response;
        }
        public bool ResetCharger(string stationId, string chargerId, out string errorCode)
        {
           _dspControlService.ResetCharger();

           errorCode = null;
           return true;
        }

        public string GetChargerFirmwareVersion()
        {
            try
            {
                if (_dspControlService != null)
                {
                    string firmwareVersion = _dspControlService.GetChargerFirmwareVersion();
                    return string.IsNullOrEmpty(firmwareVersion) ? "1.0.0" : firmwareVersion;
                }
                return "1.0.0";
            }
            catch
            {
                return "1.0.0";
            }
        }
        public string GetFaultCode(int channelNo)
        {
            if (_dspControlService != null && _dspControlService.IsOpen())
            {
                return _dspControlService.GetFaultCode(channelNo);
            }
            return "0501"; // DSP not connected
        }

        public bool UpdateFirmware(string stationId, string chargerId, string versionNo, string filePath, out string errorCode)
        {
            throw new NotImplementedException();
        }

        public bool UpdateImageFile(string stationId, string chargerId, string versionNo, string filePath, out string errorCode)
        {
            try
            {
                Uri uri = new Uri(filePath);
                string fileName = "image_update.zip";

                string finalImageUpdateDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UpdateFrontFile");
                Directory.CreateDirectory(finalImageUpdateDir);

                string downloadPath = Path.Combine(finalImageUpdateDir, fileName);

                using (WebClient webClient = new WebClient())
                {
                    webClient.Headers.Add("user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 2.0.50727; .NET CLR 3.0.04506.590; .NET CLR 3.5.20706; .NET CLR 3.0.04506.648; .NET CLR 3.5.21022; .NET CLR 3.0.4506.2152; .NET CLR 3.5.30729;)");
                    webClient.DownloadFile(uri, downloadPath);
                }

                _logger.Info($"[UpdateImageFile] Download completed: {downloadPath}");
                errorCode = null;
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"[UpdateImageFile] Download failed. filePath={filePath}, error={ex.Message}");
                errorCode = "IMAGE_UPDATE_DOWNLOAD_FAILED";
                return false;
            }
        }

        public bool UpdateMovFile(string stationId, string chargerId, string versionNo, string filePath, out string errorCode)
        {
            throw new NotImplementedException();
        }

        public async Task<bool> UpdateUIProgram(string stationId, string chargerId, string versionNo, string filePath, string md5Value)
        {
            string downloadPath = null;
            try
            {
                using (WebClient webClient = new WebClient())
                {
                    Uri uri = new Uri(filePath);
                    string fileName = Path.GetFileName(uri.AbsolutePath);
                    downloadPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update", md5Value);

                    Directory.CreateDirectory(Path.GetDirectoryName(downloadPath));
                    webClient.Headers.Add("user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 2.0.50727; .NET CLR 3.0.04506.590; .NET CLR 3.5.20706; .NET CLR 3.0.04506.648; .NET CLR 3.5.21022; .NET CLR 3.0.4506.2152; .NET CLR 3.5.30729;)");
                    //"user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 2.0.50727; .NET CLR 3.0.04506.590; .NET CLR 3.5.20706; .NET CLR 3.0.04506.648; .NET CLR 3.5.21022; .NET CLR 3.0.4506.2152; .NET CLR 3.5.30729;)"
                    await webClient.DownloadFileTaskAsync(uri, downloadPath); 

                    //if (!string.IsNullOrEmpty(md5Value))
                    //{
                    //    using (var md5 = MD5.Create())
                    //    {
                    //        using (var stream = File.OpenRead(downloadPath))
                    //        {
                    //            byte[] hash = md5.ComputeHash(stream);
                    //            string downloadedFileMd5 = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    //            if (downloadedFileMd5 != md5Value.ToLowerInvariant())
                    //            {
                    //                _logger.Error($"[HttpAsyncServer] UPDATE: MD5 mismatch for {fileName}. Expected: {md5Value}, Actual: {downloadedFileMd5}");
                    //                return false;
                    //            }
                    //        }
                    //    }
                    //}

                    // Unzip the file
                    _logger.Info($"[HttpAsyncServer] UPDATE: Download complete. Starting extraction for {downloadPath}.");
                    await Task.Run(() =>
                    {
                        string extractPath = Path.GetDirectoryName(downloadPath);
                        using (ZipArchive archive = ZipFile.OpenRead(downloadPath))
                        {
                            foreach (ZipArchiveEntry entry in archive.Entries)
                            {
                                string destinationPath = Path.GetFullPath(Path.Combine(extractPath, entry.FullName));

                                if (!destinationPath.StartsWith(extractPath, StringComparison.OrdinalIgnoreCase))
                                {
                                    throw new InvalidOperationException("Attempting to extract file outside of the destination directory.");
                                }
                                
                                if (string.IsNullOrEmpty(entry.Name))
                                {
                                    Directory.CreateDirectory(destinationPath);
                                }
                                else
                                {
                                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                                    entry.ExtractToFile(destinationPath, true);
                                }
                            }
                        }
                    });
                    _logger.Info($"[HttpAsyncServer] UPDATE: Extraction complete.");

                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[HttpAsyncServer] UPDATE: File processing failed for {filePath}. Error: {ex.Message}");
                return false;
            }
        }

        public bool StartChargingAndRemoteDone(string stationId, string chargerId, string tid, string chargerType, out string errorCode)
        {
            QrChargingStarted?.Invoke(this, new QrChargingStartedEventArgs
            {
                StationId = stationId,
                ChargerId = chargerId,
                Tid = tid,
                ChargerType = chargerType
            });

            errorCode = null;
            return true;
        }

        public bool StopChargingAndRemoteDone(string stationId, string chargerId, string tid, out string errorCode)
        {
            QrChargingEnded?.Invoke(this, new QrChargingEndedEventArgs
            {
                StationId = stationId,
                ChargerId = chargerId,
                Tid = tid
            });
            
            errorCode = null;
            return true;
        }

        /// <summary>
        /// TransmissionLog에 저장된 전문을 조회하여 덤프용 엔드포인트로 재전송한다.
        /// </summary>
        /// <param name="stationId">요청 받은 충전소 ID</param>
        /// <param name="chargerId">요청 받은 충전기 ID</param>
        /// <param name="dumpType">덤프 구분 코드(0~4)</param>
        /// <param name="dumpStartTime">조회 시작 시간(yyyyMMddHHmmss)</param>
        /// <param name="dumpEndTime">조회 종료 시간(yyyyMMddHHmmss)</param>
        /// <param name="errorCode">실패 시 에러 코드</param>
        /// <returns>모든 전문 전송 성공 여부</returns>
        public bool DumpReq(string stationId, string chargerId, string dumpType, string dumpStartTime, string dumpEndTime, out string errorCode)
        {
            errorCode = null;

            var route = ResolveDumpRoute(dumpType);
            if (route == null)
            {
                errorCode = "INVALID_DUMP_TYPE";
                _logger.Warn($"[DumpReq] Unknown dumpType={dumpType}");
                return false;
            }

            if (System.Threading.Interlocked.CompareExchange(ref _dumpInProgressFlag, 1, 0) == 1)
            {
                errorCode = "DUMP_IN_PROGRESS";
                _logger.Warn("[DumpReq] Dump is already in progress. Reject new request.");
                return false;
            }

            Task.Run(async () =>
            {
                try
                {
                    bool success = await ProcessDumpAsync(route, stationId, chargerId, dumpStartTime, dumpEndTime);
                    if (!success)
                    {
                        _logger.Warn($"[DumpReq] Dump job finished with errors. type={dumpType}");
                    }
                    else
                    {
                        _logger.Info($"[DumpReq] Dump job completed successfully. type={dumpType}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"[DumpReq] Dump job failed: {ex.Message}", ex);
                }
                finally
                {
                    System.Threading.Interlocked.Exchange(ref _dumpInProgressFlag, 0);
                }
            });

            return true;
        }

        #region Dump Helpers
        /// <summary>
        /// 덤프 경로 정보를 조회한다.
        /// </summary>
        private static DumpRoute ResolveDumpRoute(string dumpType)
        {
            if (string.IsNullOrWhiteSpace(dumpType))
            {
                return null;
            }

            _dumpRouteMap.TryGetValue(dumpType.Trim(), out DumpRoute route);
            return route;
        }

        /// <summary>
        /// TransmissionLog에서 덤프 대상 레코드를 조회한다.
        /// </summary>
        private async Task<bool> ProcessDumpAsync(DumpRoute route, string stationId, string chargerId, string dumpStartTime, string dumpEndTime)
        {
            DateTime? start = ParseDumpTimestamp(dumpStartTime);
            DateTime? end = ParseDumpTimestamp(dumpEndTime);

            if (start.HasValue && end.HasValue && Nullable.Compare(end, start) < 0)
            {
                DateTime? tmp = start;
                start = end;
                end = tmp;
            }

            IList<DumpRecord> candidates = LoadDumpRecords(route, stationId, chargerId);

            var records = new List<DumpRecord>();
            foreach (DumpRecord record in candidates)
            {
                DateTime? timestamp = ExtractRecordTimestamp(route, record);

                if (start.HasValue && (!timestamp.HasValue || timestamp.Value < start.Value))
                    continue;

                if (end.HasValue && (!timestamp.HasValue || timestamp.Value > end.Value))
                    continue;

                records.Add(record);
            }

            if (records.Count == 0)
            {
                _logger.Info($"[DumpReq] No records found for dump. route={route.DumpAddUrl}, station={stationId}, charger={chargerId}");
                return true;
            }

            int successCount = 0;
            foreach (DumpRecord record in records)
            {
                bool sent = await TrySendDumpRecordAsync(route, record, stationId, chargerId);
                if (sent)
                {
                    successCount++;
                }

                await Task.Delay(100); // throttle to protect server
            }

            return successCount == records.Count;
        }

        private IList<DumpRecord> LoadDumpRecords(DumpRoute route, string stationId, string chargerId)
        {
            var results = new List<DumpRecord>();
            string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "evcharger.db");

            SqliteService sqlite = new SqliteService(dbPath);

            var query = new System.Text.StringBuilder();
            query.Append("SELECT add_url, station_id, charger_id, request_json, created_at FROM TransmissionLog WHERE 1=1");

            var parameters = new List<SQLiteParameter>();

            if (!string.IsNullOrWhiteSpace(route.MessageType))
            {
                query.Append(" AND LOWER(message_type) = @messageType");
                parameters.Add(new SQLiteParameter("@messageType", route.MessageType.ToLowerInvariant()));
            }

            if (!string.IsNullOrWhiteSpace(route.SourceAddUrl))
            {
                query.Append(" AND LOWER(add_url) LIKE @addUrl");
                parameters.Add(new SQLiteParameter("@addUrl", route.SourceAddUrl.ToLowerInvariant() + "%"));
            }

            if (!string.IsNullOrWhiteSpace(stationId))
            {
                query.Append(" AND station_id = @stationId");
                parameters.Add(new SQLiteParameter("@stationId", stationId));
            }

            if (!string.IsNullOrWhiteSpace(chargerId))
            {
                query.Append(" AND (charger_id = @chargerId OR IFNULL(charger_id, '') = '')");
                parameters.Add(new SQLiteParameter("@chargerId", chargerId));
            }

            query.Append(" ORDER BY datetime(created_at) ASC, id ASC");

            DataTable table = sqlite.Query(query.ToString(), parameters.ToArray());

            foreach (DataRow row in table.Rows)
            {
                results.Add(new DumpRecord
                {
                    AddUrl = Convert.ToString(row["add_url"]),
                    StationId = Convert.ToString(row["station_id"]),
                    ChargerId = Convert.ToString(row["charger_id"]),
                    RequestJson = Convert.ToString(row["request_json"]),
                    CreatedAt = ParseCreatedAt(Convert.ToString(row["created_at"]))
                });
            }

            return results;
        }

        /// <summary>
        /// 레코드의 기준 시각을 추출한다.
        /// </summary>
        private DateTime? ExtractRecordTimestamp(DumpRoute route, DumpRecord record)
        {
            try
            {
                if (TryBuildJsonPayload(record.RequestJson, out string jsonEnvelope, out _, out JObject json))
                {
                    //Console.WriteLine("Dump (record.RequestJson: " + record.RequestJson);
                    //Console.WriteLine("Dump (jsonEnvelope: " + jsonEnvelope);
                    foreach (string key in route.TimestampKeys)
                    {
                        JToken token = json.SelectToken(key);
                        if (token != null)
                        {
                            DateTime? parsed = ParseDumpTimestamp(token.Value<string>());
                            if (parsed.HasValue)
                            {
                                return parsed;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"[DumpReq] Failed to parse record timestamp: {ex.Message}");
            }

            if (record.CreatedAt != DateTime.MinValue)
            {
                return record.CreatedAt;
            }

            return null;
        }

        /// <summary>
        /// 덤프 레코드를 순차적으로 서버에 전송한다.
        /// </summary>
        private Task<bool> TrySendDumpRecordAsync(DumpRoute route, DumpRecord record, string fallbackStationId, string fallbackChargerId)
        {
            return Task.Run(() =>
            {
                string station = string.IsNullOrWhiteSpace(record.StationId) ? fallbackStationId : record.StationId;
                string charger = string.IsNullOrWhiteSpace(record.ChargerId) ? fallbackChargerId : record.ChargerId;

                if (string.IsNullOrWhiteSpace(station) || string.IsNullOrWhiteSpace(charger))
                {
                    _logger.Warn("[DumpReq] Missing station or charger id. Skip.");
                    return false;
                }

                if (!TryBuildJsonPayload(record.RequestJson, out string jsonEnvelope, out string payload, out _))
                {
                    _logger.Warn("[DumpReq] Empty payload. Skip.");
                    return false;
                }

                string baseUrl = NormalizeBaseUrl(AppSettingsManager.EvCommSettings.ServerBaseUrl);
                string endpoint = BuildDumpEndpoint(baseUrl, route.DumpAddUrl, station, charger);
                string requestUrl = endpoint + "?param=" + Uri.EscapeDataString(jsonEnvelope);

                try
                {
                    WebHelper helper = new WebHelper();
                    JObject response = helper.Post("DUMP", new Uri(requestUrl), payload, 5000);
                    _logger.Info($"[SEND] DUMP :{endpoint}/{payload}");
                    bool success = response != null;

                    if (!success)
                    {
                        //_logger.Warn($"[DumpReq] Dump send failed -> {requestUrl}");
                        _logger.Error("[RECV] DUMP : no response!!!");
                    }
                    else
                    {
                        _logger.Info($"[RECV] DUMP : {response.ToString(Formatting.None)}");
                    }

                        return success;
                }
                catch (Exception ex)
                {
                    _logger.Error($"[DumpReq] Dump send error: {ex.Message}");
                    
                    return false;
                }
            });
        }

        /// <summary>
        /// 덤프 요청 시간을 DateTime으로 변환한다.
        /// </summary>
        private static DateTime? ParseDumpTimestamp(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Equals("0"))
                return null;

            string[] formats = { "yyyyMMddHHmmss", "yyyyMMddHHmm", "yyyyMMdd", "yyyy-MM-dd HH:mm:ss" };
            if (DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
            {
                return parsed;
            }

            if (DateTime.TryParse(value, out parsed))
            {
                return parsed;
            }

            return null;
        }

        /// <summary>
        /// TransmissionLog의 created_at 문자열을 DateTime으로 변환한다.
        /// </summary>
        private static DateTime ParseCreatedAt(string value)
        {
            if (DateTime.TryParse(value, out DateTime result))
            {
                return result;
            }

            return DateTime.MinValue;
        }

        /// <summary>
        /// 서버 기본 URL을 표준화한다.
        /// </summary>
        private static string NormalizeBaseUrl(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return string.Empty;
            }

            return baseUrl.EndsWith("/") ? baseUrl : baseUrl + "/";
        }

        /// <summary>
        /// 덤프용 엔드포인트 URL을 조합한다.
        /// </summary>
        private static string BuildDumpEndpoint(string baseUrl, string addUrl, string stationId, string chargerId)
        {
            string normalizedAddUrl = addUrl.StartsWith("station/", StringComparison.OrdinalIgnoreCase)
                ? addUrl
                : "station/" + addUrl.TrimStart('/');

            if (!normalizedAddUrl.EndsWith("/"))
            {
                normalizedAddUrl += "/";
            }

            string trimmedBase = NormalizeBaseUrl(baseUrl);
            return trimmedBase + normalizedAddUrl + stationId + "/" + chargerId;
        }

        /// <summary>
        /// 저장된 JSON 문자열에서 중괄호를 제거한다.
        /// </summary>
        private static string TrimJsonPayload(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return string.Empty;
            }

            string trimmed = json.Trim();
            //Console.WriteLine("TrimJsonPayload: " + trimmed);
            if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
            {
                return trimmed.Length > 2 ? trimmed.Substring(1, trimmed.Length - 2) : string.Empty;
            }

            return trimmed;
        }

        private static bool TryBuildJsonPayload(string source, out string envelope, out string inner, out JObject jsonObject)
        {
            envelope = null;
            inner = null;
            jsonObject = null;

            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            foreach (string candidate in GetJsonCandidates(source))
            {
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                try
                {
                    //Console.WriteLine("TryBuildJsonPayload: Trying candidate: " + candidate);
                    JObject obj = JObject.Parse(candidate);
                    string normalized = obj.ToString(Formatting.None);
                    string payload = TrimJsonPayload(normalized);

                    if (string.IsNullOrWhiteSpace(payload))
                        continue;

                    envelope = normalized;
                    inner = payload;
                    jsonObject = obj;
                    return true;
                }
                catch (JsonException ex)
                {
                    // JsonReaderException, JsonSerializationException 등 모든 JSON 예외 처리
                    Console.WriteLine("TryBuildJsonPayload: JSON parse error: " + ex.Message);
                    continue;
                }
                catch (Exception)
                {
                    // 기타 예외도 처리
                    continue;
                }
            }

            return false;
        }

        private static IEnumerable<string> GetJsonCandidates(string source)
        {
            return GetJsonCandidatesInternal(source, 0);
        }

        private static IEnumerable<string> GetJsonCandidatesInternal(string source, int depth)
        {
            if (depth > 3) // 최대 깊이 제한
                yield break;

            string trimmed = source.Trim();
            
            // 1. 이중 중괄호 처리: {{...}} -> {...} (가장 먼저 처리)
            if (trimmed.StartsWith("{{") && trimmed.EndsWith("}}"))
            {
                string singleBrace = trimmed.Substring(1, trimmed.Length - 2);
                if (!string.IsNullOrWhiteSpace(singleBrace))
                {
                    yield return singleBrace;
                    
                    // 처리된 결과에 대해 재귀적으로 처리
                    foreach (string candidate in GetJsonCandidatesInternal(singleBrace, depth + 1))
                    {
                        if (!string.Equals(candidate, singleBrace, StringComparison.Ordinal))
                        {
                            yield return candidate;
                        }
                    }
                }
                // 이중 중괄호면 원본은 반환하지 않음 (파싱 실패 확실)
                yield break;
            }

            // 2. 원본 반환 (이중 중괄호가 아닐 때만)
            yield return trimmed;

            // 3. 문자열 리터럴 디코딩
            if (trimmed.StartsWith("\"") && trimmed.EndsWith("\""))
            {
                string decoded = TryDeserializeJsonString(trimmed);
                if (!string.IsNullOrWhiteSpace(decoded) && !string.Equals(decoded, trimmed, StringComparison.Ordinal))
                {
                    yield return decoded;
                    
                    // 디코딩된 결과에 대해 재귀적으로 처리
                    foreach (string candidate in GetJsonCandidatesInternal(decoded, depth + 1))
                    {
                        if (!string.Equals(candidate, decoded, StringComparison.Ordinal))
                        {
                            yield return candidate;
                        }
                    }
                }
            }

            // 4. 이스케이프 처리
            if (trimmed.Contains("\\\""))
            {
                string unescaped = trimmed.Replace("\\\"", "\"").Replace("\\\\", "\\");
                if (!string.Equals(unescaped, trimmed, StringComparison.Ordinal))
                {
                    yield return unescaped;
                }
            }

            // 5. payload 처리
            string payload = TrimJsonPayload(trimmed);
            if (!string.IsNullOrWhiteSpace(payload))
            {
                yield return "{" + payload + "}";
            }
        }

        private static string TryDeserializeJsonString(string jsonString)
        {
            try
            {
                return JsonConvert.DeserializeObject<string>(jsonString);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 덤프 대상 정보를 보관한다.
        /// </summary>
        private sealed class DumpRoute
        {
            public DumpRoute(string messageType, string dumpAddUrl, string sourceAddUrl, IReadOnlyList<string> timestampKeys)
            {
                MessageType = messageType;
                DumpAddUrl = dumpAddUrl;
                SourceAddUrl = sourceAddUrl;
                TimestampKeys = timestampKeys ?? Array.Empty<string>();
            }

            public string MessageType { get; }
            public string DumpAddUrl { get; }
            public string SourceAddUrl { get; }
            public IReadOnlyList<string> TimestampKeys { get; }
        }

        /// <summary>
        /// TransmissionLog 조회 결과를 보관한다.
        /// </summary>
        private sealed class DumpRecord
        {
            public string AddUrl { get; set; }
            public string StationId { get; set; }
            public string ChargerId { get; set; }
            public string RequestJson { get; set; }
            public DateTime CreatedAt { get; set; }
        }
        #endregion
        #endregion

        public void InitCharger(int channelNo)
        {
            _dspControlService.SetChargerInit(channelNo);
        }

        public void InitStandby(int channelNo)
        {
            _dspControlService.SetChargeStandBy(channelNo);
        }
        public void InitStandby2(int channelNo)
        {            

            for (int i = 0; i < 5; i++)
            {
                _dspControlService.SetChargeStandBy(channelNo);
                Task.Delay(200).Wait();
            }
        }

        public async Task TaskInitStandby(int channelNo)
        {
            _dspControlService.SetChargeStandBy(channelNo);
            int CheckCount = 0;
            while (!_dspControlService.GetStandByStatus(channelNo))
            {
                await Task.Delay(200);
                _dspControlService.SetChargeStandBy(channelNo);
                if(CheckCount > 10)
                {
                    InitCharger(channelNo);
                    break;
                }
            }
                _dspControlService.SetChargeStandBy(channelNo);

        }
        public async Task  ReadyToCharging(int channelNo)
        {
            int connectorType = _channels[channelNo].ChargingSelect;

            _dspControlService.SetChargeStandBy(channelNo);
            await Task.Delay(200);
            _dspControlService.SetChargeReady(channelNo, connectorType);
            while (!_dspControlService.GetChargerReadyStatus(channelNo))
            {
                await Task.Delay(500);
                //_dspControlService.SetChargeStandBy(channelNo);
                _dspControlService.SetChargeReady(channelNo, connectorType);
            }
            //_dspControlService.SetChargeReady(channelNo);
            
        }

        public void SelectConnector(int channelNo)
        {
            int connectorType = _channels[channelNo].ChargingSelect;
            _dspControlService.SetCableType(channelNo, connectorType);
        }


        public Task<bool> RequestMemberCardAuth(int channelNo)
        {
            return Task.Run(() =>
            {
                bool result = false;
                string stationId = _channels[channelNo].StationId;
                string chargerId = _channels[channelNo].ChargerId;
                string membershipNo = _channels[channelNo].MembershipNo;

                result = _evCommService.SendUser(ref _channels[channelNo]);
                return result;
            });
        }

        public async Task OpenDoor(int channelNo)
        {
            _dspControlService.SetDoorStatus(channelNo, true);
            while (_dspControlService.GetChargerDoorStatus(channelNo))
            {
                await Task.Delay(500);
            }

        }

        public async Task WaitForConnectorPlugIn(int channelNo)
        {
            ChargerChannel ch = _channels[channelNo];
            int connectorType = _channels[channelNo].ChargingSelect;

            bool initialPlugChecked = _dspControlService.GetPlugCheckStatus(channelNo);
            bool initialChargingRun = _dspControlService.GetChargingRunStatus(channelNo);
            bool initialFault = _dspControlService.GetFaultStatus(channelNo);
            bool startBeforePlugCheckEnabled = _dspControlService.IsEnableStartChargingBeforePlugCheck();

            _logger.Info($"[충전기] 커넥터 연결 대기 시작. 채널={channelNo}, 커넥터타입={connectorType}");
            _logger.Info($"[충전기] 커넥터 연결 대기 진입 상태. 채널={channelNo}, Plug={initialPlugChecked}, ChargingRun={initialChargingRun}, Fault={initialFault}, StartBeforePlugCheck={startBeforePlugCheckEnabled}");

            // 이브이시스 차데모의 경우 WaitForConnectorPlugIn 시퀀스 pass
            if (AppSettingsManager.ChargerSettings.ChargerManufacturerCode == "evsis" && ch.ChargingSelect == 2)
            {
                _logger.Info($"[충전기] 커넥터 연결 대기 건너뜀 (evsis 차데모). 채널={channelNo}");
                return;
            }

            if (_dspControlService.IsEnableStartChargingBeforePlugCheck())
            {
                _dspControlService.SetChargePrepare(channelNo, connectorType);

                while(!_dspControlService.GetCharginPrepareCheck(channelNo))
                {
                    if (ch.IsWaitForConnectorPlugInCancelled)
                    {
                        _logger.Info($"[충전기] 커넥터 연결 대기 취소 (준비 상태 확인 중). 채널={channelNo}");
                        return;
                    }
                    // NOTE: 시그넷 동작 확인 필요
                    // 커넥터가 이미 꽂혀있으면 MCU가 준비완료(8)를 건너뛰고 연결완료(5)로 바로 전이할 수 있음
                    if (_dspControlService.GetPlugCheckStatus(channelNo))
                    {
                        _logger.Info($"[충전기] 커넥터 이미 연결됨 (상태5 감지). 준비 상태 대기 생략. 채널={channelNo}");
                        return;
                    }
                    // NOTE: END
                    await Task.Delay(200);
                }

                if (ch.IsWaitForConnectorPlugInCancelled)
                {
                    _logger.Info($"[충전기] 커넥터 연결 대기 취소 (충전 시작 명령 전). 채널={channelNo}");
                    return;
                }

                _dspControlService.SetChargeStart(channelNo, connectorType);
            }

            if (ch.IsWaitForConnectorPlugInCancelled)
            {
                _logger.Info($"[충전기] 커넥터 연결 대기 취소 (플러그 체크 전). 채널={channelNo}");
                return;
            }

            while (!_dspControlService.GetPlugCheckStatus(channelNo))
            {
                if (ch.IsWaitForConnectorPlugInCancelled)
                {
                    _logger.Info($"[충전기] 커넥터 연결 대기 취소 (플러그 체크 중). 채널={channelNo}");
                    return;
                }
                await Task.Delay(200);
            }

            _logger.Info($"[충전기] 커넥터 연결 대기 완료. 채널={channelNo}");
        }


        public void SetChargePrepare(int channelNo)
        {
            int connectorType = _channels[channelNo].ChargingSelect;
            _dspControlService.SetChargePrepare(channelNo, connectorType);
        }

        public void StartCharging(int channelNo)
        {
            _logger.Info($"[충전기] 충전 시작 호출. 채널={channelNo}, 제조사={AppSettingsManager.ChargerSettings.ChargerManufacturerCode}");

            if (AppSettingsManager.ChargerSettings.ChargerManufacturerCode == "evsis")
            {
                SetChargeReadyForEvsis(channelNo);
                _logger.Info($"[충전기] 충전 시작 -> EVSIS 충전 준비 명령. 채널={channelNo}");
            }
            else if (!_dspControlService.IsEnableStartChargingBeforePlugCheck())
            {
                int connectorType = _channels[channelNo].ChargingSelect;
                _dspControlService.SetChargeStart(channelNo, connectorType);
                _logger.Info($"[충전기] 충전 시작 -> 충전 시작 명령 전송. 채널={channelNo}, 커넥터타입={connectorType}");
            }
            else
            {
                _logger.Info($"[충전기] 충전 시작 건너뜀 (플러그 체크 전에 이미 시작됨). 채널={channelNo}");
            }
        }

        public async Task WaitForChargingStart(int channelNo)
        {
            
            while (!_dspControlService.GetChargingRunStatus(channelNo))
            {
                await Task.Delay(500);
            }
        }

        public bool CheckChargingStart(int channelNo)
        {
            bool result = _dspControlService.GetChargingRunStatus(channelNo);
            if (result)
            {
                _logger.Info($"[충전기] 충전 시작 감지=TRUE. 채널={channelNo}");
            }
            return result;
        }


        public bool CheckChargingRun(int channelNo)
        {
            bool result = false;
            int connectorType = _channels[channelNo].ChargingSelect;

            _logger.Info($"CheckChargingRun: {channelNo}");

            if (_dspControlService.GetChargingRunStatus(channelNo))
            {
                _logger.Info($"CheckChargingRun: {channelNo} true");
                _faultCount = 0;
                _dspControlService.SetChargeRun(channelNo, connectorType);
                result = true;
            }
            else
            {
                if (_dspControlService.GetFaultStatus(channelNo))
                {
                    _logger.Info($"CheckChargingRun: {channelNo} fault");
                    _faultCount++;
                    if (_faultCount > 2)
                    {
                        _dspControlService.SetChargeComplete(channelNo);
                    }
                    else
                    {
                        _logger.Info($"CheckChargingRun: {channelNo} fault count: {_faultCount}");
                        result = true;
                    }
                }
                else
                {
                    _logger.Info($"CheckChargingRun: {channelNo} complete");
                    _dspControlService.SetChargeComplete(channelNo);
                }
            }
            return result;

        }

        public ChargingInfo GetChargingInfo(int channelNo)
        {
            ChargingInfo result = new ChargingInfo();
            result.Soc = _dspControlService.GetSoc(channelNo);
            result.Current = _dspControlService.GetCurrent(channelNo);
            result.Voltage = _dspControlService.GetVoltage(channelNo);
            result.PowerMeter = _dspControlService.GetPowerMeter(channelNo);

            return result;
        }

        public void StopChargingold(int channelNo)
        {
            int connectorType = _channels[channelNo].ChargingSelect;
            _dspControlService.SetChargeStop(channelNo, connectorType);
        }
        public void StopChargingold2(int channelNo)
        {
            int connectorType = _channels[channelNo].ChargingSelect;
            for (int i = 0; i < 1; i++)
            {
                if (_dspControlService.GetChargingFinishStatus(channelNo))
                    break;
                _dspControlService.SetChargeStop(channelNo, connectorType);
                // Task.Delay(200).Wait();
            }
            _dspControlService.SetChargeStop(channelNo, connectorType);

            if(_dspControlService.GetChargingFinishStatus(channelNo))
            {
                for (int i = 0; i < 1; i++)
                {
                    _dspControlService.SetChargerInit(channelNo);
                    // Task.Delay(200).Wait();
                }
                _dspControlService.SetChargerInit(channelNo);
            }
            _dspControlService.SetChargerInit(channelNo);
        }

        public async Task StopCharging(int channelNo)
        {
            int connectorType = _channels[channelNo].ChargingSelect;

            _logger.Info($"[충전기] 충전 중지 시작. 채널={channelNo}, 커넥터타입={connectorType}");
            _dspControlService.SetChargeStop(channelNo, connectorType);

            int CheckCount = 0; //충전중 종료되었을때 예외처리하기 위함.

            while (!_dspControlService.GetChargingFinishStatus(channelNo))
            {
                await Task.Delay(200);
                _dspControlService.SetChargeStop(channelNo, connectorType);
                CheckCount++;

                if (CheckCount > 10) //충전중 종료되었을때 예외처리하기 위함.
                {
                    _logger.Warn($"[충전기] 충전 중지 타임아웃. 채널={channelNo}, 확인횟수={CheckCount}");
                    break;
                }
            }
            _dspControlService.SetChargerInit(channelNo);
            _logger.Info($"[충전기] 충전 중지 완료. 채널={channelNo}, 확인횟수={CheckCount}");

        }

        public async Task RequestUnplugConnector(int channelNo)
        {
            _dspControlService.SetDoorStatus(channelNo, true);
            while (_dspControlService.GetChargerDoorStatus(channelNo))
            {
                await Task.Delay(200);
            }
            _dspControlService.SetDoorStatus(channelNo, false);
        }

        public async Task PayCost(ChargerChannel chargerChannel)
        {
            PaymentInfo result =  await _paymentService.PayCost(chargerChannel.UserSetChargeAmount, chargerChannel.CsName);

            chargerChannel.PrePaymentInfo = result;
        }

        public async Task CancelPay(ChargerChannel chargerChannel)
        {
            bool result = await _paymentService.CancelPay(chargerChannel.PrePaymentInfo,
                chargerChannel.CancelChargeAmount, chargerChannel.CsName);
            chargerChannel.IsPaymentCancelSuccess = result;

        }

        public async Task CancelCardReading(ChargerChannel chargerChannel)
        {
            bool result = await _paymentService.CancelCardReading();
            // chargerChannel.IsPaymentCancelSuccess = result;
        }

        public async Task ReadRfCard(ChargerChannel chargerChannel)
        {
            string result = await _paymentService.ReadRfCard();

            chargerChannel.MembershipNo = result;
        }

        public double GetCurrentPowerMeter(int channelNo)
        {
            return _dspControlService.GetPowerMeter(channelNo);
        }

        /// <summary>
        /// 복구 가능한 세션이 있는지 확인
        /// </summary>
        public bool HasRestorableSessions()
        {
            try
            {
                var sessions = ChargingSessionManager.LoadAllSessions();
                foreach (var session in sessions)
                {
                    // Status가 "Charging" 또는 "PlugConnector"인 세션만 복구 가능
                    if (session.Status == "Charging" || session.Status == "PlugConnector")
                    {
                        _logger.Info($"[HasRestorableSessions] Found restorable session for channel {session.ConnectorId} with status {session.Status}");
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error($"[HasRestorableSessions] Error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 프로그램 시작 시 충전 중이었던 세션 복구 처리
        /// </summary>
        private void RestoreChargingSessions()
        {
            try
            {
                string sessionDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sessions");

                // 저장된 모든 세션 확인
                var sessions = ChargingSessionManager.LoadAllSessions();
                _logger.Info($"[RestoreChargingSessions] Found {sessions.Count} session(s)");
                
                if (sessions.Count == 0)
                    return;

                foreach (var session in sessions)
                {
                    _logger.Info($"[RestoreChargingSessions] Checking session {session.ConnectorId}: Status={session.Status}, StartTime={session.StartTime}, LastEnergy={session.LastEnergy}");
                    
                    // Status가 "Charging" 또는 "PlugConnector"인 세션만 처리
                    if (session.Status != "Charging" && session.Status != "PlugConnector")
                    {
                        _logger.Info($"[RestoreChargingSessions] Session {session.ConnectorId} status is {session.Status}, skipping");
                        ChargingSessionManager.DeleteSession(session.ConnectorId);
                        continue;
                    }

                    _logger.Info($"[RestoreChargingSessions] Found interrupted session for connector {session.ConnectorId} with status {session.Status}, processing...");

                    // 충전 결과를 서버로 전송하고 부분환불 진행
                    ProcessInterruptedCharging(session);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[RestoreChargingSessions] Error: {ex.Message}");
                _logger.Error($"[RestoreChargingSessions] StackTrace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// 중단된 충전 세션 처리: 서버로 전송 및 부분환불
        /// </summary>
        private async void ProcessInterruptedCharging(ChargingSessionState session)
        {
            try
            {
                _logger.Info($"[ProcessInterruptedCharging] Channel {session.ConnectorId}: Processing interrupted session");
                _logger.Info($"[ProcessInterruptedCharging] Channel {session.ConnectorId}: StartEnergy={session.StartEnergy}, LastEnergy={session.LastEnergy}");
                _logger.Info($"[ProcessInterruptedCharging] Channel {session.ConnectorId}: StartTime={session.StartTime}, LastUpdateTime={session.LastUpdateTime}");

                // DSP에서 현재 적산량 읽어오기
                double currentEnergy = session.LastEnergy; // 기본값은 세션에 저장된 값
                try
                {
                    if (_dspControlService != null && _dspControlService.IsOpen())
                    {
                        currentEnergy = _dspControlService.GetPowerMeter(session.ConnectorId);
                        _logger.Info($"[ProcessInterruptedCharging] Channel {session.ConnectorId}: Current energy from DSP={currentEnergy:F4} kWh, Session LastEnergy={session.LastEnergy:F4} kWh");
                        
                        // DSP에서 읽은 값이 세션의 LastEnergy보다 작으면 세션 값을 사용 (DSP가 리셋되었을 수 있음)
                        if (currentEnergy < session.LastEnergy)
                        {
                            _logger.Warn($"[ProcessInterruptedCharging] Channel {session.ConnectorId}: DSP energy ({currentEnergy:F4}) is less than session LastEnergy ({session.LastEnergy:F4}), using session value");
                            currentEnergy = session.LastEnergy;
                        }
                    }
                    else
                    {
                        _logger.Warn($"[ProcessInterruptedCharging] Channel {session.ConnectorId}: DSP is not available, using session LastEnergy");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"[ProcessInterruptedCharging] Channel {session.ConnectorId}: Failed to read current energy from DSP: {ex.Message}, using session LastEnergy");
                }

                // 전력량 차이 계산 (현재 적산량 - StartEnergy)
                // "기존 적산값(StartEnergy)"이 0이면 0으로 고정하지 말고, 최초 유효 현재 적산값(>0)으로 덮어써서 기준을 잡는다.
                double startEnergy = session.StartEnergy;
                if (startEnergy <= 0 && currentEnergy > 0)
                {
                    startEnergy = currentEnergy;
                    _logger.Info($"[ProcessInterruptedCharging] Channel {session.ConnectorId}: StartEnergy was 0, set to currentEnergy={currentEnergy:F4} kWh");
                }

                double chargePower = currentEnergy - startEnergy;
                
                _logger.Info($"[ProcessInterruptedCharging] Channel {session.ConnectorId}: Calculated chargePower={chargePower:F4} kWh");
                
                // 예외처리: 전력량 값이 비정상(음수, 0)일 경우 처리 스킵
                // 단, 아주 작은 값(0.001 kWh 미만)도 허용하여 최소한의 과금이라도 처리
                if (chargePower < 0)
                {
                    _logger.Error($"[ProcessInterruptedCharging] Channel {session.ConnectorId}: Invalid charge power ({chargePower:F2}), skipping. StartEnergy={startEnergy}, LastEnergy={session.LastEnergy}");
                    ChargingSessionManager.DeleteSession(session.ConnectorId);
                    return;
                }
                
                // 실제 과금 금액 계산 — 현재 시간대 단가(INI H00~H23) 사용
                // 원단위 절삭: 1원 자리 버림(=10원 단위로 내림)
                int currentHour = session.StartTime.Hour;
                float unitCostAtStart = AppSettingsManager.ChargerOperationSettings.PriceForHour[currentHour];
                int actualChargeAmount = MoneyUtil.TruncateWonUnit((int)(chargePower * unitCostAtStart));

                // 충전 시간 계산
                int chargeTime = (int)(session.LastUpdateTime - session.StartTime).TotalSeconds;
                if (chargeTime < 0)
                {
                    chargeTime = 0;
                }

                // 결제 타입 결정
                string payType = "C";
                switch (session.PaymentMethod)
                {
                    case PaymentMethod.RfCard:
                        payType = "N";
                        break;
                    case PaymentMethod.IcCard:
                    case PaymentMethod.SamsungPay:
                        payType = "Y";
                        break;
                    case PaymentMethod.QrCode:
                        payType = "Q";
                        break;
                }

                // 종료 시간 설정
                DateTime endTime = session.LastUpdateTime;
                if ((DateTime.Now - session.LastUpdateTime).TotalMinutes > 60)
                {
                    endTime = DateTime.Now;
                }

                _logger.Info($"[ProcessInterruptedCharging] Channel {session.ConnectorId}: ChargePower={chargePower:F2} kWh, ActualChargeAmount={actualChargeAmount}, UserSetAmount={session.UserSetChargeAmount}");

                // previous_trno: QR 결제일 때는 tid, 신용카드일 때는 AuthNum, 그 외는 "-9999"
                string previousTrno = "-9999";
                if (session.PaymentMethod == PaymentMethod.QrCode && !string.IsNullOrEmpty(session.QrTid))
                {
                    previousTrno = session.QrTid;
                }
                else if (session.PrePaymentInfo != null)
                {
                    previousTrno = session.PrePaymentInfo.AuthNum;
                }
                
                // DSP에 충전 종료 시그널 전송
                try
                {
                    StopChargingold2(session.ConnectorId);
                }
                catch (Exception ex)
                {
                    _logger.Error($"[ProcessInterruptedCharging] Channel {session.ConnectorId}: Failed to send charge stop signal to DSP: {ex.Message}");
                    // DSP 시그널 전송 실패해도 계속 진행
                }

                ChargerChannel ch = _channels[session.ConnectorId];
                
                // 충전 종료 메시지 전송 (비정상 종료)
                _evCommService.SendChargingEnd(
                    session.StationId,
                    session.ChargerId,
                    DateTime.Now.ToString("yyyyMMddHHmmss"), // send_date
                    session.StartTime.ToString("yyyyMMddHHmmss"), // start_date
                    String.IsNullOrEmpty(session.MembershipNo) ? "-9999" : session.MembershipNo, // cardNumber
                    previousTrno, // previousTrno
                    session.PrePaymentInfo != null ? session.PrePaymentInfo.PayDate : "-9999", // previousDate
                    payType,
                    AppSettingsManager.ChargerOperationSettings.IsPaymentApplied ? "Y" : "N",
                    session.ChargingSelect, // charger_type
                    (uint)(_dspControlService.GetPowerMeter(session.ConnectorId) * 1000),
                    session.PrePaymentInfo != null ? Int32.Parse(session.PrePaymentInfo.TotalCost) : 0,
                    (int)(_dspControlService.GetVoltage(session.ConnectorId) * 10),
                    (int)(_dspControlService.GetCurrent(session.ConnectorId) * 10),
                    chargeTime, // charge_time
                    (uint)ch.FinalPowerMeter,
                    4, // charge_end_type
                    endTime.ToString("yyyyMMddHHmmss"), // end_date
                    actualChargeAmount, // after_cost (실제 사용 금액)
                    session.UserSetChargeAmount > actualChargeAmount ? session.UserSetChargeAmount - actualChargeAmount : 0, // cancel_cost (부분환불 금액)
                    "", // point_kind
                    DateTime.Now.ToString("yyyyMMddHHmmss"), // cancel_date
                    "Y", // cancel_result
                    session.CurrentUserUnitCost.ToString(), // unit_cost
                    "0", // charging_rate
                    session.OrderNo // order_no
                );

                _logger.Info($"[ProcessInterruptedCharging] Channel {session.ConnectorId}: Charging end message sent successfully");

                // 부분환불 처리 (사전 결제가 있고 실제 사용 금액이 설정 금액보다 적은 경우)
                if (session.PrePaymentInfo != null && 
                    session.UserSetChargeAmount > 0 && 
                    actualChargeAmount < session.UserSetChargeAmount)
                {
                    int refundAmount = session.UserSetChargeAmount - actualChargeAmount;
                    
                    _logger.Info($"[ProcessInterruptedCharging] Channel {session.ConnectorId}: Processing partial refund: {refundAmount}");

                    // PaymentInfo 복원
                    var paymentInfo = new PaymentInfo
                    {
                        PayCode = session.PrePaymentInfo.PayCode,
                        AuthNum = session.PrePaymentInfo.AuthNum,
                        TotalCost = session.PrePaymentInfo.TotalCost,
                        PayDate = session.PrePaymentInfo.PayDate,
                        PayTime = session.PrePaymentInfo.PayTime,
                        PgNum = session.PrePaymentInfo.PgNum
                    };

                    // 부분환불 실행
                    bool refundResult = await _paymentService.CancelPay(paymentInfo, refundAmount, $"ECP{session.StationId}{session.ChargerId}");
                    
                    if (refundResult)
                    {
                        _logger.Info($"[ProcessInterruptedCharging] Channel {session.ConnectorId}: Partial refund successful: {refundAmount}");
                    }
                    else
                    {
                        _logger.Error($"[ProcessInterruptedCharging] Channel {session.ConnectorId}: Partial refund failed: {refundAmount}");
                    }
                }

                // 세션 파일 삭제
                ChargingSessionManager.DeleteSession(session.ConnectorId);

                // 충전 결과 화면 표시를 위한 이벤트 발생
                var args = new InterruptedChargingRestoredEventArgs
                {
                    ChannelNo = session.ConnectorId,
                    ChargePower = chargePower,
                    ActualChargeAmount = actualChargeAmount,
                    UserSetChargeAmount = session.UserSetChargeAmount,
                    CancelChargeAmount = session.UserSetChargeAmount > actualChargeAmount ? session.UserSetChargeAmount - actualChargeAmount : 0,
                    StartTime = session.StartTime,
                    EndTime = endTime,
                    ChargeTime = chargeTime
                };

                ch.ChargeAmount = actualChargeAmount;

                _logger.Info($"[ProcessInterruptedCharging] Channel {session.ConnectorId}: Raising InterruptedChargingRestored event");
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _logger.Info($"[ProcessInterruptedCharging] Channel {session.ConnectorId}: Invoking event on UI thread");
                    InterruptedChargingRestored?.Invoke(this, args);
                    _logger.Info($"[ProcessInterruptedCharging] Channel {session.ConnectorId}: Event invoked, handler count: {(InterruptedChargingRestored?.GetInvocationList().Length ?? 0)}");
                });
            }
            catch (Exception ex)
            {
                _logger.Error($"[ProcessInterruptedCharging] Channel {session.ConnectorId}: Error processing interrupted charging: {ex.Message}");
            }
        }

        /// <summary>
        /// 프로그램 종료 시 충전 중인 채널의 세션 저장
        /// </summary>
        public void HandleShutdown()
        {
            try
            {
                _logger.Info("[HandleShutdown] Checking for active charging sessions...");

                foreach (var channel in _channels)
                {
                    if (channel == null)
                        continue;

                    if (ChargingSessionManager.IsCharging(channel))
                    {
                        _logger.Info($"[HandleShutdown] Found active charging on channel {channel.ChannelNo}, saving session");

                        // 현재 전력량 가져오기
                        double currentPowerMeter = _dspControlService.GetPowerMeter(channel.ChannelNo);
                        
                        // 세션 상태 저장 (충전 중 상태로)
                        ChargingSessionManager.SaveSession(channel, currentPowerMeter, "Charging");
                        
                        _logger.Info($"[HandleShutdown] Channel {channel.ChannelNo}: Session saved");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[HandleShutdown] Error: {ex.Message}");
            }
        }

        #region private method
        private void ResetCharger(bool forceReset)
        {

        }


        #endregion


        // 중단된 충전 복구 완료 이벤트
        public event EventHandler<InterruptedChargingRestoredEventArgs> InterruptedChargingRestored;

        #region Emergency

        // 이벤트 정의
        public event EventHandler EmergencyRaised;
        public event EventHandler EmergencyCleared;
        public event EventHandler DspConnectionLost;
        public event EventHandler DspConnectionRestored;
        public event Action<int> EvseStatusChanged;  // EVSE_Status 변경 이벤트 (0:정상, 1:점검중, 2:중지)
        public event Action<bool> PayYNStatusChanged;
        public event Action<bool> TestStatusChanged;

        private bool _isEmergency = false;
        private bool _isDspDisconnected = false;
        private int _lastEvseStatus = -1;  // 초기값 -1로 설정하여 최초 변경도 감지
        private Dictionary<int, string> _channelFaultCodes = new Dictionary<int, string>(); // 채널별 경보 발생 시 FaultCode 저장

        public bool IsEmergency => _isEmergency;
        public bool IsDspDisconnected => _isDspDisconnected;
        public bool IsPmsDisconnected => _dspControlService != null && !_dspControlService.IsPmsConnected();

        // 발생
        private void RaiseEmergency()
        {
            if (_isEmergency) return; // 이미 발생 상태면 무시
            _isEmergency = true;
            OnEmergencyRaised();
#if false
            // Emergency 발생 시 점검중 상태로 설정 (ChargerMode만 변경, EVSE_Status는 관리자 설정값이므로 변경하지 않음)
            if (AppSettingsManager.EvCommSettings.ChargerMode != 3)
            {
                AppSettingsManager.EvCommSettings.ChargerMode = 3;
                AppSettingsManager.Save();
            }
#endif
            // TODO: 모든 채널에 대해 알람 전송
            foreach (var channel in _channels)
            {
                if (channel == null) continue;
     
                string faultCode = "";

                bool emergencyRaised =
                    _isEmergency ||
                    AppSettingsManager.EvCommSettings.EVSE_EmergencyStop == 1;

                if (emergencyRaised)
                {
                    faultCode = "901";
                }
            //         else if (IsPmsDisconnected)
            //         {
            //             faultCode = "0904";
            //         }
            //         // DSP 연결 상태 확인
            //         else if (_dspControlService != null && _dspControlService.IsOpen())
            //         {
            //             faultCode = _dspControlService.GetFaultCode(channel.ChannelNo);
            //         }
            //         else
            //         {
            //             // DSP 연결 이상일 경우 9999로 전송
            //             faultCode = "0501";
            //         }
            //     }
            //     catch
            //     {
            //         // 예외 발생 시 DSP 연결 이상으로 간주
            //         faultCode = "0501";
            //     }
                
                string normalizedFaultCode = NormalizeAlarmCode(faultCode);

                // 경보 발생 시 FaultCode 저장 (해제 시 사용)
                _channelFaultCodes[channel.ChannelNo] = normalizedFaultCode;

                _evCommService.SendAlarmHistory(channel.StationId, channel.ChargerId, "0", DateTime.Now.ToString("yyyyMMddHHmmss"), normalizedFaultCode);
            }
#if false
            if (_channels.Length > 0 && _channels[0] != null)
            {
                _evCommService.SendChargerStatus(_channels[0].StationId, _channels[0].ChargerId, 3,
                                _dspControlService.GetChargingRunStatus(_channels[0].ChannelNo) ? 2 : 1, 0,
                                _dspControlService.GetPlugCheckStatus(_channels[0].ChannelNo) ? 2 : 1,
                                (uint)(_dspControlService.GetPowerMeter(_channels[0].ChannelNo) * 1000), "");
            }
#endif
        }  

        // 해제
        private void ClearEmergency()
        {
            if (!_isEmergency) return; // 이미 해제 상태면 무시
            _isEmergency = false;
            OnEmergencyCleared();
            // Emergency 해제 시 세 조건 모두 확인하여 ChargerMode 업데이트
            CheckAndUpdateChargerMode();
            
            // 모든 채널에 대해 알람 해제 전송 (발생 시 저장된 FaultCode 사용)
            foreach (var channel in _channels)
            {
                if (channel == null) continue;
                
                // 경보 발생 시 저장된 FaultCode 가져오기 (없으면 빈 문자열)
                string savedFaultCode = _channelFaultCodes.ContainsKey(channel.ChannelNo) 
                    ? _channelFaultCodes[channel.ChannelNo] 
                    : "0000";

                _evCommService.SendAlarmHistory(channel.StationId, channel.ChargerId, "1", DateTime.Now.ToString("yyyyMMddHHmmss"), savedFaultCode);
                
                // 전송 후 저장된 FaultCode 제거
                _channelFaultCodes.Remove(channel.ChannelNo);
            }
            
            // 상태 업데이트는 첫 번째 채널만 전송
            if (_channels.Length > 0 && _channels[0] != null)
            {
                int mode = AppSettingsManager.EvCommSettings.ChargerMode;
                _evCommService.SendChargerStatus(_channels[0].StationId, _channels[0].ChargerId, mode,
                                _dspControlService.GetChargingRunStatus(_channels[0].ChannelNo) ? 2 : 1, 0,
                                _dspControlService.GetPlugCheckStatus(_channels[0].ChannelNo) ? 2 : 1,
                                (uint)(_dspControlService.GetPowerMeter(_channels[0].ChannelNo) * 1000), "");
            }
        }

        // 이벤트 발생 트리거
        protected virtual void OnEmergencyRaised()
            => EmergencyRaised?.Invoke(this, EventArgs.Empty);

        protected virtual void OnEmergencyCleared()
            => EmergencyCleared?.Invoke(this, EventArgs.Empty);

        protected virtual void OnDspConnectionLost()
            => DspConnectionLost?.Invoke(this, EventArgs.Empty);

        protected virtual void OnDspConnectionRestored()
            => DspConnectionRestored?.Invoke(this, EventArgs.Empty);

        private string NormalizeAlarmCode(string rawFaultCode)
        {
            if (string.IsNullOrWhiteSpace(rawFaultCode))
            {
                return "0000";
            }

            if (int.TryParse(rawFaultCode, out int code))
            {
                int normalized = Math.Abs(code) % 10000;
                return normalized.ToString("D4");
            }

            string digitsOnly = new string(rawFaultCode.Where(char.IsDigit).ToArray());
            if (digitsOnly.Length == 0)
            {
                return "0000";
            }

            if (digitsOnly.Length > 4)
            {
                digitsOnly = digitsOnly.Substring(digitsOnly.Length - 4);
            }

            return digitsOnly.PadLeft(4, '0');
        }

        /// <summary>
        /// ChargerMode를 업데이트하는 메서드
        /// 네 조건(1. DSP 연결 정상, 2. Emergency 없음, 3. EVSE_Status가 점검중 아님, 4. 네트워크 연결 정상)이 모두 충족되면 운영중(1)으로 복구
        /// EVSE_Status는 관리자 설정값이므로 물리적 문제가 해결되어도 자동으로 복구하지 않음 (관리자가 설정한 점검중은 유지)
        /// </summary>
        public bool CheckAndUpdateChargerMode()
        {
            // 네 조건 확인:
            // 1. DSP 연결이 정상인지
            bool isDspConnected = _dspControlService != null && _dspControlService.IsOpen();
            
            // 2. 비상정지 버튼이 안 걸려있는지
            // DSP 제어 서비스 호출을 동기적으로 처리 (이 메서드는 이미 백그라운드에서 호출되거나 빠른 체크이므로)
            bool isEmergencyClear = !_isEmergency && !(_dspControlService?.GetEmergencyStatus() ?? true);
            
            // 3. EVSE_Status가 점검중(1)이 아닌지
            bool isEvseStatusNormal = AppSettingsManager.EvCommSettings.EVSE_Status != 1;
            
            // 4. 네트워크 연결이 정상인지
            bool isNetworkConnected = AppSettingsManager.EvCommSettings.EVSE_Network_Status == 0;
            
            // 네 조건이 모두 충족되면 운영중(1)으로 복구
            bool allConditionsMet = isDspConnected && isEmergencyClear && isEvseStatusNormal && isNetworkConnected;

#if false
            bool shouldSave = false;
            if (allConditionsMet)
            {
                if (AppSettingsManager.EvCommSettings.ChargerMode != 1)
                {
                    AppSettingsManager.EvCommSettings.ChargerMode = 1;
                    shouldSave = true;
                    _logger.Info("[Charger] CheckAndUpdateChargerMode: All conditions met. ChargerMode set to 1 (운영중)");
                }
            }
            else
            {
                // 조건 중 하나라도 충족되지 않으면 점검중 유지 (ChargerMode만 변경, EVSE_Status는 관리자 설정값이므로 변경하지 않음)
                if (AppSettingsManager.EvCommSettings.ChargerMode != 3)
                {
                    AppSettingsManager.EvCommSettings.ChargerMode = 3;
                    shouldSave = true;
                    _logger.Info($"[Charger] CheckAndUpdateChargerMode: Conditions not met. ChargerMode set to 3 (점검중). DSP:{isDspConnected}, Emergency:{isEmergencyClear}, EVSE:{isEvseStatusNormal}, Network:{isNetworkConnected}");
                }
            }

            if (shouldSave)
            {
                AppSettingsManager.Save();
            }
#endif

            // EVSE_Status는 관리자 설정값이므로 자동으로 복구하지 않음
            // 물리적 문제가 해결되어도 EVSE_Status = 1이면 관리자가 점검중으로 설정한 것으로 간주
            return allConditionsMet;
        }

        #endregion

        private DispatcherTimer _updateCheckTimer;
        private bool _isUpdateInProgress = false; // 업데이트 진행 중 플래그;3

        /// <summary>
        /// 업데이트 파일 확인 및 적용 (비동기)
        /// UI 스레드 블로킹을 방지하기 위해 비동기로 실행됩니다.
        /// </summary>
#if true
        private async void PerformUpdate(object sender, EventArgs e)
        {
            // 중복 실행 방지
            if (_isUpdateInProgress)
            {
                _logger.Debug("[In-Process Update] 이미 업데이트가 진행 중이므로 건너뜁니다.");
                return;
            }

            // 타이머 정지하여 중복 실행 방지
            lock (_timerLock)
            {
                _updateCheckTimer.Stop();
            }

            _logger.Debug("[In-Process Update__PerformUpdate] 시작.");

            try
            {
                // 1. 경로 정의 및 업데이트 폴더 존재 여부 확인
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string updateDir = Path.Combine(baseDir, "update");

                _logger.Debug("[In-Process Update] 경로 체크.");

                bool updateDirExists = await Task.Run(() => Directory.Exists(updateDir));
                if (!updateDirExists)
                {
                    lock (_timerLock)
                    {
                        _updateCheckTimer.Start();
                    }
                    return;
                }

                _logger.Debug("[In-Process Update] 업데이트 구성 요소 존재 여부 확인.");

                // 2. 업데이트 구성 요소 존재 여부 확인
                string newExePath = Path.Combine(updateDir, "EvChargerUI.exe");
                string frontUpdatePath = Path.Combine(updateDir, "UpdateFrontFile");
                string originExePath = Path.Combine(baseDir, "EvChargerUI.exe");

                bool newExeExists = await Task.Run(() => File.Exists(newExePath));
                bool frontUpdateDirExists = await Task.Run(() => Directory.Exists(frontUpdatePath));

                // 업데이트할 내용이 없으면 폴더 정리 후 종료
                _logger.Debug("[In-Process Update] 업데이트할 내용이 없으면 폴더 정리 후 종료.");

                if (!newExeExists && !frontUpdateDirExists)
                {
                    _logger.Info("[업데이트 확인] 업데이트 디렉토리가 비어있거나 유효하지 않습니다. 정리합니다.");
                    Directory.Delete(updateDir, true);
                    lock (_timerLock)
                    {
                        _updateCheckTimer.Start();
                    }
                    return;
                }

                // 3. 충전기 유휴 상태 확인 (모든 업데이트 유형에 필요)
                _logger.Debug("[In-Process Update] 충전기 유휴 상태 확인 (모든 업데이트 유형에 필요).");
                var mainVm = ((App)Application.Current).MainView?.DataContext as ViewModels.MainViewModel;
                if (mainVm == null || (mainVm.LeftChargerView == null))
                {
                    lock (_timerLock)
                    {
                        _updateCheckTimer.Start(); // ViewModel이 아직 준비되지 않았을 수 있으므로 타이머 재시작
                    }
                    return;
                }
                var leftVm = mainVm.LeftChargerView.DataContext as ViewModels.ChargerViewModel;
                var rightVm = mainVm.RightChargerView?.DataContext as ViewModels.ChargerViewModel;

                // 251218 : 충전 완료 업데이트 시도
                bool isLeftIdle = leftVm?.CurrentChargeSequence == Commons.Enum.ChargeSequence.SelectConnector || leftVm?.CurrentChargeSequence == Commons.Enum.ChargeSequence.Completed;
                bool isRightIdle = rightVm == null || rightVm.CurrentChargeSequence == Commons.Enum.ChargeSequence.SelectConnector || rightVm.CurrentChargeSequence == Commons.Enum.ChargeSequence.Completed;

                _logger.Debug($"[In-Process Update] 충전기 유휴 상태 확인: Left={isLeftIdle} (seq={leftVm?.CurrentChargeSequence}), Right={isRightIdle} (seq={rightVm?.CurrentChargeSequence})");
                if (!isLeftIdle || !isRightIdle)
                {
                    _logger.Info("[In-Process Update] 업데이트가 대기 중이지만 충전기가 유휴 상태가 아닙니다. 다음 주기에 다시 확인합니다.");
                    lock (_timerLock)
                    {
                        _updateCheckTimer.Start();
                    }
                    return;
                }

                // 4. 업데이트 수행
                _isUpdateInProgress = true;
                _logger.Info("[In-Process Update] 충전기가 유휴 상태입니다. 업데이트를 시작합니다.");

                await Task.Run(async () =>
                {
                    try
                    {
                        // UpdateFrontFile 폴더가 존재하면 폴더 자체를 복사
                        if (frontUpdateDirExists)
                        {
                            _logger.Info("[In-Process Update] UpdateFrontFile 폴더를 발견했습니다. 폴더를 복사합니다.");
                            string destinationPath = Path.Combine(baseDir, new DirectoryInfo(frontUpdatePath).Name);
                            CopyDirectory(frontUpdatePath, destinationPath, true);
                            _logger.Info("[In-Process Update] UpdateFrontFile 폴더 복사가 완료되었습니다.");
                        }

                        // 새 실행 파일이 있으면 교체 후 프로그램 종료
                        if (newExeExists)
                        {
                            bool originFileExists = File.Exists(originExePath);
                            string backupExePath = Path.Combine(baseDir, "EvChargerUI_.exe");

                            if (originFileExists)
                            {
                                _logger.Info($"[In-Process Update] 실행 중인 EXE 이름 변경: {originExePath}");
                                File.Move(originExePath, backupExePath);
                                _logger.Info("[In-Process Update] 이름 변경 성공.");
                            }
                            else
                            {
                                _logger.Info($"[In-Process Update] 원본 EXE를 찾을 수 없습니다. 복사만 진행합니다.");
                            }

                            _logger.Info($"[In-Process Update] 새 EXE 복사 중: {newExePath}");
                            File.Copy(newExePath, originExePath, true);
                            _logger.Info("[In-Process Update] 복사 성공.");

                            // EXE 업데이트 시에만 프로그램 종료
                            _logger.Info("[In-Process Update] 업데이트 파일 정리 중.");
                            Directory.Delete(updateDir, true);

                            await Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                _logger.Info("[In-Process Update] EXE 업데이트 성공. 프로그램을 종료합니다. [SHUTDOWN_REASON: In-Process EXE Update]");
                                AppSettingsManager.EvCommSettings.LastUiUpdateDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                AppSettingsManager.Save();
                                Application.Current.Shutdown();
                            });
                        }
                        else // UpdateFrontFile만 업데이트된 경우
                        {
                            _logger.Info("[In-Process Update] UpdateFrontFile만 업데이트 완료. 정리합니다.");
                            Directory.Delete(updateDir, true);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"[In-Process Update] 파일 작업 실패: {ex.Message}");
                    }
                    finally
                    {
                        _isUpdateInProgress = false;
                        // EXE 업데이트가 아닌 경우에만 타이머 재시작
                        if (!newExeExists)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                lock (_timerLock)
                                {
                                    _updateCheckTimer.Start();
                                }
                            });
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.Error($"[In-Process Update] 최상위 업데이트 확인 실패: {ex.Message}");
                _isUpdateInProgress = false;
                await Task.Delay(5000);
                lock (_timerLock)
                {
                    _updateCheckTimer.Start();
                }
            }
        }
#else
        private async void PerformUpdate(object sender, EventArgs e)
        {
            // 중복 실행 방지
            if (_isUpdateInProgress)
            {
                // _logger.Debug("[In-Process Update] Update already in progress, skipping check.");
                return;
            }

            // 타이머 정지하여 중복 실행 방지
            _updateCheckTimer.Stop();

            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string updateDir = Path.Combine(baseDir, "update");
                string newExePath = Path.Combine(updateDir, "EvChargerUI.exe");
                string checkbackupExePathOrigin = Path.Combine(baseDir, "EvChargerUI_.exe");
                string checkOriginFile = Path.Combine(baseDir, "EvChargerUI.exe");

                // 파일 시스템 체크를 백그라운드 스레드에서 실행
                bool updateDirExists = await Task.Run(() => Directory.Exists(updateDir));
                bool newExeExists = await Task.Run(() => File.Exists(newExePath));
                bool originFileExists = await Task.Run(() => File.Exists(checkOriginFile));

                // UI 스레드에서 ViewModel 접근
                var mainVm = ((App)Application.Current).MainView?.DataContext as ViewModels.MainViewModel;
                if (!mainVm.ShowInitView) //NormalView 뷰가 활성화 상태가 아니면
                {
                    _logger.Debug("PerformUpdate() ShowInitView false");
                    if (mainVm == null)
                    {
                        _logger.Debug("PerformUpdate() mainVm null");
                        _updateCheckTimer.Start(); // MainView가 아직 준비되지 않았을 수 있으므로 타이머를 재시작
                        return;
                    }
                }

                var leftVm = mainVm.LeftChargerView.DataContext as ViewModels.ChargerViewModel;
                var rightVm = mainVm.RightChargerView?.DataContext as ViewModels.ChargerViewModel;

                bool isLeftIdle = leftVm?.CurrentChargeSequence == Commons.Enum.ChargeSequence.SelectConnector;
                bool isRightIdle = rightVm == null || rightVm.CurrentChargeSequence == Commons.Enum.ChargeSequence.SelectConnector;

                // 1 & 2단계: update 폴더 및 새 실행 파일 존재 여부 확인
                if (updateDirExists && newExeExists && originFileExists)
                {
                    // _logger.Info("[In-Process Update] Pending update found. Checking if charger is idle.");

                    // 모든 채널이 유휴 상태일 때만 업데이트 진행
                    if (isLeftIdle && isRightIdle)
                    {
                        _isUpdateInProgress = true;
                        // _logger.Info("[In-Process Update] Charger is idle. Attempting update now.");

                        // 파일 작업을 백그라운드 스레드에서 실행
                        await Task.Run(async () =>
                        {
                            try
                            {
                                string currentExePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                                string backupExePath = Path.Combine(baseDir, "EvChargerUI_.exe");

                                // 3단계: 현재 실행 중인 파일 이름 변경 시도
                                // _logger.Info($"[In-Process Update] Step 3: Renaming running EXE: {currentExePath}");
                                File.Move(currentExePath, backupExePath);
                                // _logger.Info("[In-Process Update] Step 3: Rename successful.");

                                // UpdateFrontFile 폴더 복사
                                string frontUpdatePath = Path.Combine(updateDir, "UpdateFrontFile");
                                if (Directory.Exists(frontUpdatePath))
                                {
                                    // _logger.Info("[In-Process Update] Found UpdateFrontFile folder. Copying contents.");
                                    CopyDirectory(frontUpdatePath, baseDir, true);
                                    // _logger.Info("[In-Process Update] Finished copying UpdateFrontFile contents.");
                                }

                                // 4단계: 새 파일 복사
                                // _logger.Info($"[In-Process Update] Step 4: Copying new EXE from {newExePath}");
                                File.Copy(newExePath, currentExePath, true);
                                // _logger.Info("[In-Process Update] Step 4: Copy successful.");

                                // 5단계: update 폴더 및 파일 삭제
                                // _logger.Info("[In-Process Update] Step 5: Cleaning up update files.");
                                Directory.Delete(updateDir, true);

                                // 6단계: UI 스레드에서 프로그램 종료
                                await Application.Current.Dispatcher.InvokeAsync(() =>
                                {
                                    // _logger.Info("[In-Process Update] Update appears successful. Shutting down.");
                                    Application.Current.Shutdown();
                                });
                                AppSettingsManager.EvCommSettings.LastUiUpdateDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                AppSettingsManager.Save();
                            }
                            catch (Exception ex)
                            {
                                // _logger.Error($"[In-Process Update] File operation failed: {ex.Message}");
                                _isUpdateInProgress = false;
                                // UI 스레드에서 타이머 재시작
                                Application.Current.Dispatcher.Invoke(() => _updateCheckTimer.Start());
                            }
                        });
                    }
                    else
                    {
                        // _logger.Info("[In-Process Update] Charger is not idle. Will check again on next tick.");
                        _updateCheckTimer.Start(); // 충전기가 바쁘므로 타이머를 재시작
                    }
                }
                else if (!originFileExists && updateDirExists && newExeExists)
                {
                    if (isLeftIdle && isRightIdle)
                    {
                        _isUpdateInProgress = true;
                        // 파일 작업을 백그라운드 스레드에서 실행
                        await Task.Run(async () =>
                        {
                            try
                            {
                                string currentExePath = System.Reflection.Assembly.GetExecutingAssembly().Location;

                                // UpdateFrontFile 폴더 복사
                                string frontUpdatePath = Path.Combine(updateDir, "UpdateFrontFile");
                                if (Directory.Exists(frontUpdatePath))
                                {
                                    // _logger.Info("[else if step In-Process Update] Found UpdateFrontFile folder. Copying contents.");
                                    CopyDirectory(frontUpdatePath, baseDir, true);
                                    // _logger.Info("[else if step In-Process Update] Finished copying UpdateFrontFile contents.");
                                }

                                // 4단계: 새 파일 복사
                                // _logger.Info($"[else if step In-Process Update] Step 4: Copying new EXE from {newExePath}");
                                File.Copy(newExePath, currentExePath, true);
                                // _logger.Info("[else if step In-Process Update] Step 4: Copy successful.");

                                // 5단계: update 폴더 및 파일 삭제
                                // _logger.Info("[else if step In-Process Update] Step 5: Cleaning up update files.");
                                Directory.Delete(updateDir, true);

                                // 6단계: UI 스레드에서 프로그램 종료
                                await Application.Current.Dispatcher.InvokeAsync(() =>
                                {
                                    // _logger.Info("[else if step In-Process Update] Update appears successful. Shutting down.");
                                    Application.Current.Shutdown();
                                });
                                AppSettingsManager.EvCommSettings.LastUiUpdateDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                AppSettingsManager.Save();
                            }
                            catch (Exception ex)
                            {
                                // _logger.Error($"[In-Process Update] File operation failed: {ex.Message}");
                                _isUpdateInProgress = false;
                                Application.Current.Dispatcher.Invoke(() => _updateCheckTimer.Start());
                            }
                        });
                    }
                }
                else
                {
                    // update 폴더나 파일이 없으므로 타이머를 재시작합니다.
                    _updateCheckTimer.Start();
                }
            }
            catch (Exception ex)
            {
                // 예상대로, 실행 중인 파일은 잠겨 있으므로 이 블록이 실행될 가능성이 매우 높습니다.
                // _logger.Error($"[In-Process Update] FAILED. As predicted, the OS likely prevents modifying a running application. This is a fundamental limitation. Error: {ex.Message}");
                _isUpdateInProgress = false;
                // 반복적인 오류를 막기 위해 일정 시간 후 타이머 재시작
                await Task.Delay(5000);
                _updateCheckTimer.Start();
            }
        }
#endif

        private static void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
        {
            // Get information about the source directory
            var dir = new DirectoryInfo(sourceDir);

            // Check if the source directory exists
            if (!dir.Exists)
                return;

            // Cache directories before we start copying
            DirectoryInfo[] dirs = dir.GetDirectories();

            // Create the destination directory
            Directory.CreateDirectory(destinationDir);

            // Get the files in the source directory and copy to the destination directory
            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath, true); // true to overwrite
            }

            // If recursive and copying subdirectories, recursively call this method
            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectory(subDir.FullName, newDestinationDir, true);
                }
            }
        }

        #region ONLY EVSIS
        /// <summary>
        /// ONLY EVSIS
        /// </summary>
        /// <param name="channelNo"></param>
        public void SetChargeReadyForEvsis(int channelNo) 
        {
            int connectorType = _channels[channelNo].ChargingSelect;
            _dspControlService.SetChargeReady(channelNo, connectorType);
        }

        /// <summary>
        /// ONLY EVSIS
        /// </summary>
        /// <param name="channelNo"></param>
        public void SetWaitForConnectorPlugInForEvsis(int channelNo)
        {
            int connectorType = _channels[channelNo].ChargingSelect;
            _dspControlService.SetChargeStart(channelNo, connectorType);
        }

        public bool CheckChargingFinishStatus(int channelNo)
        {
            return _dspControlService.GetChargingFinishStatus(channelNo);
        }
    
        #endregion
    }
}

