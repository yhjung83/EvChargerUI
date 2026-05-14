using EvChargerUI.Commons.Enum;
using EvChargerUI.Commons.Settings;
using EvChargerUI.Commons.Settings;
using EvChargerUI.Commons.Util;
using EvChargerUI.Commons.Util;
using EvChargerUI.Models;
using EvChargerUI.Models;
using EvChargerUI.Services.EvComm;
using EvChargerUI.Services.EvComm;
using EvChargerUI.Services.EvComm.HttpJsonRequest;
using EvChargerUI.ViewModels;
using EvChargerUI.Views.Popup;
using EvChargerUI.Commons.Enum;
using EvChargerUI.Services.EvComm.HttpJsonRequest;
using JoasUtils;
using JoasUtils;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using static System.Net.WebRequestMethods;


namespace EvChargerUI.Services
{
    public delegate void DataPushEventHandler(string delimeter, string[] data, string reponse, string jSon, HttpListenerContext httpListenerContext);

    public class EvCommService : IEvCommService
    {
        private readonly EvCommForm _evCommForm;
        private readonly IEvCommToChargerDelegate _chargerDelegate;
        private readonly ISystemSettingsService _systemSettingsService;
        private FileLogger _logger = ((App)Application.Current).AppLogger;

        private bool _isServerConnected;
        private volatile bool _isTimeSyncedOnStartup = false; // 프로그램 시작 시 1회만 시간 동기화하기 위한 플래그
        
        /// <summary>
        /// 서버와의 통신 연결 상태
        /// </summary>
        public bool IsServerConnected 
        { 
            get { return _isServerConnected; }
            private set 
            { 
                if (_isServerConnected != value)
                {
                    _isServerConnected = value;
                    _logger.Info($"[SERVER CONNECTION] Status changed to: {value}");
                }
            }
        }


        private DataPushEventHandler _dataSendEventPrices;
        private DataPushEventHandler _dataSendEventDisplayBrightness;
        private DataPushEventHandler _dataSendEventSound;
        private DataPushEventHandler _dataSendEventUpdate;
        private DataPushEventHandler _dataSendEventStatus;
        private DataPushEventHandler _dataSendEventCheckStatus;
        private DataPushEventHandler _dataSendEventLimit;
        private DataPushEventHandler _dataSendEventTest;
        private DataPushEventHandler _dataSendEventReset;
        private DataPushEventHandler _dataSendEventPayYn;
        private DataPushEventHandler _dataSendEventAuth;
        private DataPushEventHandler _dataSendEventStop;
        private DataPushEventHandler _dataSendEventDump;



        private readonly EvChargerUI.Services.Database.OfflineTxRepository _txRepo;
        private readonly System.Windows.Threading.DispatcherTimer _retryTimer;
        private readonly System.Windows.Threading.DispatcherTimer _purgeTimer;
        private readonly string _dbPath;
        private volatile bool _isRetrying = false; // 재시도 중복 실행 방지 플래그

        public EvCommService(string serverUrl, IEvCommToChargerDelegate chargerDelegate)
        {
            try
            {
                _logger.Info("[EvCommService] *** Initialization START ***");
                _logger.Info($"[EvCommService] Server URL Parameter: {serverUrl}");

                //EvComm.ResponseData.clientUrl = "http://192.168.1.10:5050/";
                string settingsUrl = AppSettingsManager.EvCommSettings.ClientBaseUrl;
                _logger.Info($"[EvCommService] ClientBaseUrl from Settings: {settingsUrl}");

                EvComm.ResponseData.clientUrl = settingsUrl;
                _logger.Info($"[EvCommService] EvComm.ResponseData.clientUrl assigned: {EvComm.ResponseData.clientUrl}");

                // offline queue 준비
                _dbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "evcharger.db");
                _logger.Info($"[EvCommService] Database path: {_dbPath}");

                var sqlite = new EvChargerUI.Services.Database.SqliteService(_dbPath);
                sqlite.Initialize();
                _logger.Info("[EvCommService] SQLite initialized successfully");

                _txRepo = new EvChargerUI.Services.Database.OfflineTxRepository(sqlite);
                _logger.Info("[EvCommService] OfflineTxRepository created successfully");

                _evCommForm = new EvCommForm(serverUrl, (addUrl, stationId, chargerId, json, success) =>
                {
                    try
                    {
                        string type = InferMessageType(addUrl);
                        string status = (success && IsServerConnected) ? "sent" : "pending";
                        _txRepo.Insert(type, addUrl, stationId ?? string.Empty, chargerId, json, status);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"[EvCommService] Database insert failed: {ex.Message}");
                    }
                });
                _logger.Info("[EvCommService] EvCommForm created successfully");

                _systemSettingsService = new SystemSettingsService();

                _chargerDelegate = chargerDelegate;

                _retryTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
                _retryTimer.Tick += (s, e) => _ = RetryPendingAsync(); // 비동기 실행 (fire-and-forget)
                _retryTimer.Start();
                _logger.Info("[EvCommService] RetryTimer started (15 seconds interval)");

                _purgeTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromHours(24) };//삭제 타이머
                _purgeTimer.Tick += (s, e) =>
                {
                    try { _txRepo.PurgeOlderThanDays(60); } catch { }
                };
                _purgeTimer.Start();
                _logger.Info("[EvCommService] PurgeTimer started (24 hours interval)");

                _logger.Info("[EvCommService] *** Initialization COMPLETE ***");
            }
            catch (Exception ex)
            {
                _logger.Fatal("[EvCommService] *** INITIALIZATION FAILED ***");
                _logger.Fatal($"[EvCommService] Exception Type: {ex.GetType().Name}");
                _logger.Fatal($"[EvCommService] Message: {ex.Message}");
                _logger.Fatal($"[EvCommService] StackTrace: {ex.StackTrace}");
                throw;
            }
        }

        private string InferMessageType(string addUrl)
        {
            if (string.IsNullOrEmpty(addUrl)) return "UNKNOWN";
            string u = addUrl.ToLowerInvariant();
            if (u.Contains("chargingstart")) return "chargingStart";
            if (u.Contains("charginginfo")) return "chargingInfo";
            if (u.Contains("chargingend")) return "chargingEnd";
            if (u.Contains("chargers")) return "chargers";
            if (u.Contains("alarmHistory")) return "alarmHistory";
            
            return "other";
        }

        private async Task RetryPendingAsync()
        {            
            // 이미 재시도 중이면 중복 실행 방지
            if (_isRetrying)
            {
                _logger.Debug("[RetryPendingAsync] Already retrying, skipping this cycle");
                return;
            }

            if (!IsServerConnected)
            {
                _logger.Debug("[RetryPendingAsync] Server not connected, skipping retry");
                return;
            }

            // 모든 채널이 유휴 상태일 때만 재전송 수행
            try
            {
                var app = System.Windows.Application.Current as App;
                var charger = app?.Charger;
                if (charger?.Channels != null)
                {
                    foreach (var ch in charger.Channels)
                    {
                        if (ch == null) continue;
                        if (ch.CurrentSequence != ChargeSequence.SelectConnector)
                        {
                            // 채널 중 하나라도 유휴가 아니면 이번 주기에는 재전송하지 않음
                            _logger.Debug($"[RetryPendingAsync] Channel busy, skipping retry cycle. Sequence: {ch.CurrentSequence}");
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[RetryPendingAsync] Error checking channel status: {ex.Message}");
                _logger.Debug($"[RetryPendingAsync] StackTrace: {ex.StackTrace}");
                return;
            }

            // 재시도 시작 플래그 설정
            _isRetrying = true;
            _logger.Debug("[RetryPendingAsync] Starting retry cycle");

            try
            {
                // 백그라운드 스레드에서 HTTP 요청 및 DB 작업 수행하여 UI 블로킹 방지
                await Task.Run(async () =>
                {
                    try
                    {
                        var pendingList = _txRepo.GetPending(20);
                        _logger.Debug($"[RetryPendingAsync] Found {pendingList.Count()} pending transactions to retry");

                        foreach (var row in pendingList)
                        {
                            // addUrl은 "station/.../" 형태이어야 함
                            string delimiter = row.MessageType.ToUpperInvariant();

                            _logger.Debug($"[RetryPendingAsync] Attempting retry for: URL={row.AddUrl}, Type={row.MessageType}");

                            // HTTP 요청을 백그라운드에서 실행 (동기 메서드를 Task.Run으로 래핑)
                            var res = await Task.Run(() => _evCommForm.httpPostResponse(
                                request: TrimJsonBraces(row.RequestJson),
                                addURL: row.AddUrl,
                                stationId: row.StationId,
                                chargerId: row.ChargerId,
                                delimiter: delimiter,
                                timeout: 5000));

                            _logger.Info($"[RetrySEND] URL: {row.AddUrl} stationId:{row.StationId}, chargerId:{row.ChargerId}, Data: {row.RequestJson}");

                            if (res != null)
                            {
                                _txRepo.MarkSent(row.Id);
                                _logger.Info($"[RetrySUCCESS] URL: {row.AddUrl} stationId:{row.StationId}, chargerId:{row.ChargerId}");
                            }
                            else
                            {
                                _txRepo.BumpRetry(row.Id);
                                _logger.Error($"[RetryFAILED] URL: {row.AddUrl} stationId:{row.StationId}, chargerId:{row.ChargerId}");
                            }

                            // 각 요청 후 짧은 지연 (비동기로 변경하여 UI 블로킹 방지)
                            await Task.Delay(500); // 500ms
                        }

                        _logger.Debug("[RetryPendingAsync] Retry cycle completed");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"[RetryPendingAsync] Exception during retry execution: {ex.Message}");
                        _logger.Error($"[RetryPendingAsync] StackTrace: {ex.StackTrace}");
                    }
                }).ConfigureAwait(false); // UI 스레드로 돌아올 필요 없음
            }
            catch (Exception ex)
            {
                _logger.Error($"[RetryPendingAsync] Unhandled exception in retry task: {ex.Message}");
                _logger.Error($"[RetryPendingAsync] StackTrace: {ex.StackTrace}");
            }
            finally
            {
                // 재시도 완료 후 플래그 해제
                _isRetrying = false;
                _logger.Debug("[RetryPendingAsync] Retry cycle flag released");
            }
        }

        private string TrimJsonBraces(string json)
        {
            if (string.IsNullOrEmpty(json)) return string.Empty;
            json = json.Trim();
            if (json.StartsWith("{") && json.EndsWith("}"))
                return json.Substring(1, json.Length - 2);
            return json;
        }


        public void Open()
        {
            try
            {
                _logger.Info("[EvCommService.Open] *** Opening communication server ***");

                ResponseServer responseServer = _evCommForm.GetResponseServer();
                _logger.Info("[EvCommService.Open] ResponseServer obtained");

                responseServer.OpenServer(_evCommForm);
                _logger.Info("[EvCommService.Open] OpenServer called");

                responseServer.GetInstanceServerReset().DataSendEventReset = new DataGetEventHandler(this.CallBackResponseDataReset);
                responseServer.GetInstanceServerPrices().DataSendEventPrices = new DataGetEventHandler(this.CallBackResponseDataPrices);
                responseServer.GetInstanceServerDisplayBrightness().DataSendEventDisplayBrightness = new DataGetEventHandler(this.CallBackResponseDataDisplayBrightness);
                responseServer.GetInstanceServerSound().DataSendEventSound = new DataGetEventHandler(this.CallBackResponseDataSound);
                responseServer.GetInstanceServerUpdate().DataSendEventUpdate = new DataGetEventHandler(this.CallBackResponseDataUpdate);
                responseServer.GetInstanceServerStatus().DataSendEventStatus = new DataGetEventHandler(this.CallBackResponseDataStatus);
                responseServer.GetInstanceServerCheckStatus().DataSendEventCheckStatus = new DataGetEventHandler(this.CallBackResponseDataCheckStatus);
                responseServer.GetInstanceServerLimit().DataSendEventLimit = new DataGetEventHandler(this.CallBackResponseDataLimit);
                responseServer.GetInstanceServerTest().DataSendEventTest = new DataGetEventHandler(this.CallBackResponseDataTest);
                responseServer.GetInstanceServerPayYn().DataSendEventPayYn = new DataGetEventHandler(this.CallBackResponseDataPayYn);
                responseServer.GetInstanceServerAuth().DataSendEventAuth = new DataGetEventHandler(this.CallBackResponseDataAuth);
                responseServer.GetInstanceServerStop().DataSendEventStop = new DataGetEventHandler(this.CallBackResponseDataStop);
                responseServer.GetInstanceServerDump().DataSendEventDump = new DataGetEventHandler(this.CallBackResponseDataDump);

                // Initialize local delegate fields to point to HttpAsyncServer.SetResponse
                _dataSendEventReset = responseServer.GetInstanceServerReset().SetResponse;
                _dataSendEventPrices = responseServer.GetInstanceServerPrices().SetResponse;
                _dataSendEventDisplayBrightness = responseServer.GetInstanceServerDisplayBrightness().SetResponse;
                _dataSendEventSound = responseServer.GetInstanceServerSound().SetResponse;
                _dataSendEventUpdate = responseServer.GetInstanceServerUpdate().SetResponse;
                _dataSendEventStatus = responseServer.GetInstanceServerStatus().SetResponse;
                _dataSendEventCheckStatus = responseServer.GetInstanceServerCheckStatus().SetResponse;
                _dataSendEventLimit = responseServer.GetInstanceServerLimit().SetResponse;
                _dataSendEventTest = responseServer.GetInstanceServerTest().SetResponse;
                _dataSendEventPayYn = responseServer.GetInstanceServerPayYn().SetResponse;
                _dataSendEventAuth = responseServer.GetInstanceServerAuth().SetResponse;
                _dataSendEventStop = responseServer.GetInstanceServerStop().SetResponse;
                _dataSendEventDump = responseServer.GetInstanceServerDump().SetResponse;

                _logger.Debug("[EvCommService.Open] Data send event handlers registered");

                responseServer.GetInstanceServerReset().DataSent += new EvComm.HttpAsyncServer.DataSentEventHandler(this.HandleServerDataSent);
                responseServer.GetInstanceServerPrices().DataSent += new EvComm.HttpAsyncServer.DataSentEventHandler(this.HandleServerDataSent);
                responseServer.GetInstanceServerDisplayBrightness().DataSent += new EvComm.HttpAsyncServer.DataSentEventHandler(this.HandleServerDataSent);
                responseServer.GetInstanceServerSound().DataSent += new EvComm.HttpAsyncServer.DataSentEventHandler(this.HandleServerDataSent);
                responseServer.GetInstanceServerUpdate().DataSent += new EvComm.HttpAsyncServer.DataSentEventHandler(this.HandleServerDataSent);
                responseServer.GetInstanceServerStatus().DataSent += new EvComm.HttpAsyncServer.DataSentEventHandler(this.HandleServerDataSent);
                responseServer.GetInstanceServerCheckStatus().DataSent += new EvComm.HttpAsyncServer.DataSentEventHandler(this.HandleServerDataSent);
                responseServer.GetInstanceServerLimit().DataSent += new EvComm.HttpAsyncServer.DataSentEventHandler(this.HandleServerDataSent);
                responseServer.GetInstanceServerTest().DataSent += new EvComm.HttpAsyncServer.DataSentEventHandler(this.HandleServerDataSent);
                responseServer.GetInstanceServerPayYn().DataSent += new EvComm.HttpAsyncServer.DataSentEventHandler(this.HandleServerDataSent);
                responseServer.GetInstanceServerAuth().DataSent += new EvComm.HttpAsyncServer.DataSentEventHandler(this.HandleServerDataSent);
                responseServer.GetInstanceServerStop().DataSent += new EvComm.HttpAsyncServer.DataSentEventHandler(this.HandleServerDataSent);
                responseServer.GetInstanceServerDump().DataSent += new EvComm.HttpAsyncServer.DataSentEventHandler(this.HandleServerDataSent);
                _logger.Debug("[EvCommService.Open] Data sent event handlers registered");

                _logger.Info("[EvCommService.Open] *** Communication server opened successfully ***");
            }
            catch (Exception ex)
            {
                _logger.Fatal("[EvCommService.Open] *** FAILED to open communication server ***");
                _logger.Fatal($"[EvCommService.Open] Exception Type: {ex.GetType().Name}");
                _logger.Fatal($"[EvCommService.Open] Message: {ex.Message}");
                _logger.Fatal($"[EvCommService.Open] StackTrace: {ex.StackTrace}");
                throw;
            }
        }

        public void Close()
        {
            try
            {
                _logger.Info("[EvCommService.Close] Closing communication server...");
                _evCommForm.GetResponseServer().CloseServer();
                _logger.Info("[EvCommService.Close] Communication server closed successfully");
            }
            catch (Exception ex)
            {
                _logger.Error($"[EvCommService.Close] Error closing communication server: {ex.Message}");
                _logger.Error($"[EvCommService.Close] StackTrace: {ex.StackTrace}");
            }
        }

        #region Callback Method

        private void CallBackResponseDataPayYn(string requestJson, HttpListenerContext httpListenerContext)
        {
            string response = "0";
            try
            {
                JSonParser jsonParser = _evCommForm.GetJSonParser();
                JObject reqJObject = JObject.Parse(requestJson);

                string stationId = jsonParser.GetJSonData(reqJObject, "station_id");
                string chargerId = jsonParser.GetJSonData(reqJObject, "charger_id");
                string payYn = jsonParser.GetJSonData(reqJObject, "pay_yn");

                string errorCode = null;
                if (_chargerDelegate.ChangePaymentRequiredFlag(stationId, chargerId, !payYn.Equals("N"), out errorCode))
                {
                    response = "1";
                }
            }
            catch (Exception e)
            {
                response = "0";
            }
            finally
            {
                _dataSendEventPayYn("PAYYN", (string[]) null, response, requestJson, httpListenerContext);
            }
        }
        
        private void CallBackResponseDataTest(string requestJson, HttpListenerContext httpListenerContext)
        {
            string response = "0";
            try
            {
                JSonParser jsonParser = _evCommForm.GetJSonParser();
                JObject reqJObject = JObject.Parse(requestJson);

                string stationId = jsonParser.GetJSonData(reqJObject, "station_id");
                string chargerId = jsonParser.GetJSonData(reqJObject, "charger_id");
                string testYn = jsonParser.GetJSonData(reqJObject, "test_yn");

                string errorCode = null;
                if (_chargerDelegate.ChangeTestMode(stationId, chargerId, testYn.Equals("Y"), out errorCode))
                {
                    response = "1";
                }
            }
            catch (Exception e)
            {
                response = "0";
            }
            finally
            {
                _dataSendEventTest("TEST", (string[])null, response, requestJson, httpListenerContext);
            }
        }
        
        private void CallBackResponseDataLimit(string requestJson, HttpListenerContext httpListenerContext)
        {
            string response = "0";
            try
            {
                JSonParser jsonParser = _evCommForm.GetJSonParser();
                JObject reqJObject = JObject.Parse(requestJson);

                string stationId = jsonParser.GetJSonData(reqJObject, "station_id");
                string chargerId = jsonParser.GetJSonData(reqJObject, "charger_id");
                string timeLimitYn = jsonParser.GetJSonData(reqJObject, "timelimit_yn");
                string timeLimitValue = jsonParser.GetJSonData(reqJObject, "timelimit_value");

                // 충전 중인지 확인
                bool isCharging = false;
                try
                {
                    App app = ((App)Application.Current);
                    if (app?.Charger != null)
                    {
                        foreach (var channel in app.Charger.Channels)
                        {
                            if (channel != null && 
                                channel.StationId == stationId && 
                                channel.ChargerId == chargerId &&
                                channel.CurrentSequence == ChargeSequence.Charging)
                            {
                                isCharging = true;
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"[CallBackResponseDataLimit] Error checking charging status: {ex.Message}");
                }

                // 충전 중이면 설정 변경하지 않고 response를 0으로 반환
                if (isCharging)
                {
                    _logger.Info($"[CallBackResponseDataLimit] Charging in progress. Settings update rejected for stationId: {stationId}, chargerId: {chargerId}");
                    response = "0";
                }
                else
                {
                    var currentTimeLimitYn = AppSettingsManager.ChargerOperationSettings.IsChargeTimeLimited ? "Y" : "N";
                    var currentTimeLimitValue = AppSettingsManager.ChargerOperationSettings.ChargeLimitTime.ToString();
                    var newTimeLimitYn = timeLimitYn;
                    var newTimeLimitValue = timeLimitValue;

                    var before_TimeLimitYn = currentTimeLimitYn;
                    var after_TimeLimitYn = newTimeLimitYn;
                    var before_TimeLimitValue = currentTimeLimitValue;
                    var after_TimeLimitValue = newTimeLimitValue;

                    bool valueChanged = (before_TimeLimitYn != newTimeLimitYn) || (before_TimeLimitValue != newTimeLimitValue);
                    if (valueChanged)
                    {
                        AppSettingsManager.ChargerOperationSettings.IsChargeTimeLimited = newTimeLimitYn.Equals("Y");
                        
                        if (int.TryParse(newTimeLimitValue, out int limitValue))
                        {
                            AppSettingsManager.ChargerOperationSettings.ChargeLimitTime = limitValue;
                        }
                        
                        AppSettingsManager.Save();
                        _logger.Info($"[CallBackResponseDataLimit] Settings updated - TimeLimitYn: {before_TimeLimitYn} -> {after_TimeLimitYn}, TimeLimitValue: {before_TimeLimitValue} -> {after_TimeLimitValue}");
                    }

                    string errorCode = null;
                    if (_chargerDelegate.ChangeChargingTimeLimitInfo(stationId, chargerId, !timeLimitYn.Equals("N"), int.Parse(timeLimitValue), out errorCode))
                    {
                        response = "1";
                    }
                }
            }
            catch (Exception e)
            {
                response = "0";
                _logger.Error($"[CallBackResponseDataLimit] Error: {e.Message}");
            }
            finally
            {
                _dataSendEventLimit("LIMIT", (string[])null, response, requestJson, httpListenerContext);
            }
        }
        
        private void CallBackResponseDataCheckStatus(string requestJson, HttpListenerContext httpListenerContext)
        {
            string response = "0";
            try
            {
                JSonParser jsonParser = _evCommForm.GetJSonParser();
                JObject reqJObject = JObject.Parse(requestJson);

                string stationId = jsonParser.GetJSonData(reqJObject, "station_id");
                string chargerId = jsonParser.GetJSonData(reqJObject, "charger_id");
                string errorCode = null;

                JObject responseJObject = _chargerDelegate.GetChargerInfo(stationId, chargerId, out errorCode);
                
                if(responseJObject != null)
                {
                    response = responseJObject.ToString();
                }

            }
            catch (Exception e)
            {
                response = "0";
            }
            finally
            {
                _dataSendEventCheckStatus("CHECKSTATUS", (string[])null, response, requestJson, httpListenerContext);
            }
        }
        
        private void CallBackResponseDataStatus(string requestJson, HttpListenerContext httpListenerContext)
        {
            string response = "0";
            try
            {
                JSonParser jsonParser = _evCommForm.GetJSonParser();
                JObject reqJObject = JObject.Parse(requestJson);

                string stationId = jsonParser.GetJSonData(reqJObject, "station_id");
                string chargerId = jsonParser.GetJSonData(reqJObject, "charger_id");
                string status = jsonParser.GetJSonData(reqJObject, "change_status");

                string errorCode = null;
                if (_chargerDelegate.ChangeChargerStatus(stationId, chargerId, status, out errorCode))
                {
                    response = "1";
                }
            }
            catch (Exception e)
            {
                response = "0";
            }
            finally
            {
                _dataSendEventStatus("STATUS", (string[])null, response, requestJson, httpListenerContext);
            }
        }
        
        private async void CallBackResponseDataUpdate(string requestJson, HttpListenerContext httpListenerContext)
        {

            string response = "0";
            try
            {
               // response = "1";
               // _dataSendEventUpdate("UPDATE", (string[])null, response, requestJson, httpListenerContext);

                Thread.Sleep(500); 

                JSonParser jsonParser = _evCommForm.GetJSonParser();
                JObject reqJObject = JObject.Parse(requestJson);

                string stationId = jsonParser.GetJSonData(reqJObject, "station_id");
                string chargerId = jsonParser.GetJSonData(reqJObject, "charger_id");
                string patchId = jsonParser.GetJSonData(reqJObject, "patch_id");
                string verkind = jsonParser.GetJSonData(reqJObject, "ver_kind");

                string verNo = jsonParser.GetJSonData(reqJObject, "ver_no");
                string patchFilename = jsonParser.GetJSonData(reqJObject, "patch_file");
                string md5 = jsonParser.GetJSonData(reqJObject, "md5");
                md5 = patchFilename;

                
                string patchFile = AppSettingsManager.EvCommSettings.ServerBaseUrl + "updater/patch/" + stationId + "/" + chargerId + "/" + patchId;

                _logger.Info($"[CallBackResponseDataUpdate] requestJson: {requestJson}");
                _logger.Info($"[CallBackResponseDataUpdate] stationId: {stationId}, chargerId: {chargerId}, patchId: {patchId}, verKind: {verkind}, verNo: {verNo}, patchFileName: {patchFilename}, md5: {md5}, patchFileUrl: {patchFile}");

                ///updater/patch/{충전소ID}/{충전기ID}/{패치ID}               


                bool updateResult = false;
                string errorCode = null;

                if (string.Equals(verkind, "IM", StringComparison.OrdinalIgnoreCase))
                {
                    updateResult = _chargerDelegate.UpdateImageFile(stationId, chargerId, verNo, patchFile, out errorCode);
                    if (!updateResult)
                    {
                        _logger.Error($"[CallBackResponseDataUpdate] IM update failed. errorCode: {errorCode}");
                    }
                }
                else
                {
                    updateResult = await _chargerDelegate.UpdateUIProgram(stationId, chargerId, verNo, patchFile, md5);
                    if (updateResult)
                    {
                        // UI 업데이트 성공 시 LastUiUpdateDate 업데이트
                        try
                        {
                            string updateDate = DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");

                            // 설정 파일에 직접 저장
                            AppSettingsManager.EvCommSettings.LastUiUpdateDate = updateDate;
                            AppSettingsManager.Save();

                            // AdminWindow가 열려있다면 ViewModel도 업데이트
                            var app = Application.Current as App;
                            if (app?.AdminWindow != null)
                            {
                                var adminViewModel = app.AdminWindow.ViewModel;
                                if (adminViewModel != null)
                                {
                                    // UI 스레드에서 실행
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        adminViewModel.UpdateUiUpdateDate();
                                    });
                                }
                            }

                            _logger.Info($"[CallBackResponseDataUpdate] LastUiUpdateDate updated to: {updateDate}");
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"[CallBackResponseDataUpdate] Failed to update UI update date: {ex.Message}");
                        }
                    }
                }

                response = updateResult ? "1" : "0";

            }
            catch (Exception e)
            {
                response = "0";
            }
            finally
            {
               _dataSendEventUpdate("UPDATE", (string[])null, response, requestJson, httpListenerContext);
            }           
        }
        
        private void CallBackResponseDataSound(string requestJson, HttpListenerContext httpListenerContext)
        {
            string response = "0";
            try
            {
                JSonParser jsonParser = _evCommForm.GetJSonParser();
                JObject reqJObject = JObject.Parse(requestJson);

                string stationId = jsonParser.GetJSonData(reqJObject, "station_id");
                string chargerId = jsonParser.GetJSonData(reqJObject, "charger_id");
                string day = jsonParser.GetJSonData(reqJObject, "day");
                string night = jsonParser.GetJSonData(reqJObject, "night");

                if (_systemSettingsService.SetSoundVolume(int.Parse(day), int.Parse(night)))
                {
                    response = "1";
                }
            }
            catch (Exception e)
            {
                response = "0";
                _logger.Error($"[CallBackResponseDataSound] Error: {e.Message}");
            }
            finally
            {
                _dataSendEventSound("SOUND", (string[])null, response, requestJson, httpListenerContext);
            }
        }
        
        private void CallBackResponseDataDisplayBrightness(string requestJson, HttpListenerContext httpListenerContext)
        {
            string response = "0";
            try
            {
                JSonParser jsonParser = _evCommForm.GetJSonParser();
                JObject reqJObject = JObject.Parse(requestJson);

                string stationId = jsonParser.GetJSonData(reqJObject, "station_id");
                string chargerId = jsonParser.GetJSonData(reqJObject, "charger_id");
                string day = jsonParser.GetJSonData(reqJObject, "day");
                string night = jsonParser.GetJSonData(reqJObject, "night");

                if (_systemSettingsService.SetDisplayBrightness(int.Parse(day), int.Parse(night)))
                {
                    response = "1";
                }
            }
            catch (Exception e)
            {
                response = "0";
            }
            finally
            {
                _dataSendEventDisplayBrightness("DISPLAYBRIGHTNESS", (string[])null, response, requestJson, httpListenerContext);
            }
        }
        
        private void CallBackResponseDataPrices(string requestJson, HttpListenerContext httpListenerContext)
        {
            string response = "0";
            try
            {
                JSonParser jsonParser = _evCommForm.GetJSonParser();
                JObject reqJObject = JObject.Parse(requestJson);

                string stationId  = jsonParser.GetJSonData(reqJObject, "station_id");
                string chargerId  = jsonParser.GetJSonData(reqJObject, "charger_id");
                double[] prices   = GetDoublueValuesForHour(reqJObject, 347.2);
                string applyDate  = jsonParser.GetJSonData(reqJObject, "apply_date");
                string endDate    = jsonParser.GetJSonData(reqJObject, "end_date");
                string createDate = jsonParser.GetJSonData(reqJObject, "create_date");
                string errorCode  = null;

                _logger.Info($"[CallBackResponseDataPrices] Received price update. stationId: {stationId}, chargerId: {chargerId}, applyDate: {applyDate}, endDate: {endDate}, createDate: {createDate}, H00: {prices[0]}, H12: {prices[12]}");

                // DB에 스케줄 저장 (apply_date/end_date 기반 스케줄링)
                try
                {
                    string dbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "evcharger.db");
                    var sqlite = new EvChargerUI.Services.Database.SqliteService(dbPath);
                    sqlite.Initialize();
                    var priceRepo = new EvChargerUI.Services.Database.PriceScheduleRepository(sqlite);
                    priceRepo.Upsert(stationId, chargerId, createDate, applyDate, endDate, prices);
                    _logger.Info($"[CallBackResponseDataPrices] Price schedule saved to DB. createDate: {createDate}");
                }
                catch (Exception ex)
                {
                    _logger.Error($"[CallBackResponseDataPrices] Failed to save price schedule to DB: {ex.Message}");
                }

                // apply_date <= now < end_date 범위일 때만 즉시 INI 반영
                // end_date > now: end_date 시점에 종료
                string nowStr = DateTime.Now.ToString("yyyyMMddHHmmss");
                bool applyNow = (string.IsNullOrEmpty(applyDate) || string.Compare(applyDate, nowStr) <= 0)
                             && (string.IsNullOrEmpty(endDate)   || string.Compare(endDate,   nowStr) > 0);

                if (applyNow && _chargerDelegate.ChangeChargingUnitPrices(stationId, chargerId, prices, applyDate, endDate, createDate, out errorCode))
                {
                    // ========== 변경: 충전 중에도 현재 시간대 단가를 즉시 반영 ==========
                    // 현재 시간의 단가를 가져와서 충전 중인 채널에 즉시 적용
                    int currentHour = DateTime.Now.Hour;
                    double currentUnitCost = prices[currentHour];

                    _logger.Info($"[CallBackResponseDataPrices] Current hour: {currentHour}, Current unit cost: {currentUnitCost}");

                    try
                    {
                        App app = ((App)Application.Current);
                        _logger.Info($"[CallBackResponseDataPrices] App is null: {app == null}, Charger is null: {app?.Charger == null}");

                        if (app?.Charger != null)
                        {
                            bool channelFound = false;
                            foreach (var channel in app.Charger.Channels)
                            {
                                if (channel != null)
                                {
                                    _logger.Info($"[CallBackResponseDataPrices] Checking channel - StationId: {channel.StationId}, ChargerId: {channel.ChargerId}, Sequence: {channel.CurrentSequence}");

                                    if (channel.StationId == stationId && 
                                        channel.ChargerId == chargerId)
                                    {
                                        channelFound = true;
                                        float oldUnitCost = channel.CurrentUserUnitCost;

                                        // ========== 구간별 과금: 충전 중 단가 변경 시 구간 저장 ==========
                                        if (channel.CurrentSequence == ChargeSequence.Charging && 
                                            Math.Abs(oldUnitCost - currentUnitCost) > 0.01) // 단가가 실제로 변경된 경우만
                                        {
                                            _logger.Info($"[CallBackResponseDataPrices] Price changed during charging. Saving segment. ChannelNo: {channel.ChannelNo}");

                                            // UI 스레드에서 현재 PowerMeter 값을 가져와 구간 저장
                                            Application.Current.Dispatcher.Invoke(() =>
                                            {
                                                try
                                                {
                                                    var mainView = ((App)Application.Current).MainView;
                                                    if (mainView?.DataContext is MainViewModel mainViewModel)
                                                    {
                                                        ChargerViewModel chargerVM = null;

                                                        if (channel.ChannelNo == 0 && mainViewModel.LeftChargerView?.DataContext is ChargerViewModel leftVm)
                                                        {
                                                            chargerVM = leftVm;
                                                        }
                                                        else if (channel.ChannelNo == 1 && mainViewModel.RightChargerView?.DataContext is ChargerViewModel rightVm)
                                                        {
                                                            chargerVM = rightVm;
                                                        }

                                                        if (chargerVM != null)
                                                        {
                                                            double currentPowerMeter = chargerVM.PowerMeter;

                                                            // 현재 구간의 금액 계산
                                                            int currentSegmentCost = MoneyUtil.TruncateWonUnit(
                                                                (int)((currentPowerMeter - channel.CurrentSegmentStartPowerMeter) * oldUnitCost)
                                                            );
                                                            int totalAccumulatedCost = channel.AccumulatedCostBeforeCurrentSegment + currentSegmentCost;

                                                            // 구간 이력에 추가
                                                            channel.UnitCostChangeHistory.Add(new UnitCostChangeRecord
                                                            {
                                                                PowerMeter = currentPowerMeter,
                                                                UnitCost = oldUnitCost,
                                                                AccumulatedCost = totalAccumulatedCost
                                                            });

                                                            // 새 구간 시작
                                                            channel.CurrentSegmentStartPowerMeter = currentPowerMeter;
                                                            channel.AccumulatedCostBeforeCurrentSegment = totalAccumulatedCost;

                                                            _logger.Info($"[CallBackResponseDataPrices] Segment saved. PowerMeter: {currentPowerMeter}, " +
                                                                       $"OldUnitCost: {oldUnitCost}, SegmentCost: {currentSegmentCost}, " +
                                                                       $"TotalAccumulated: {totalAccumulatedCost}");
                                                        }
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    _logger.Error($"[CallBackResponseDataPrices] Error saving segment: {ex.Message}");
                                                }
                                            });
                                        }
                                        // ========== 구간별 과금: 구간 저장 끝 ==========

                                        // 충전 중이든 아니든 CurrentUserUnitCost를 업데이트 (다음 충전부터 적용)
                                        channel.CurrentUserUnitCost = (float)currentUnitCost;
                                        _logger.Info($"[CallBackResponseDataPrices] Updated CurrentUserUnitCost from {oldUnitCost} to {currentUnitCost} for channel. IsCharging: {channel.CurrentSequence == ChargeSequence.Charging}");

                                        // 충전 중이면 UI도 즉시 업데이트하기 위해 PropertyChanged 이벤트 발생
                                        if (channel.CurrentSequence == ChargeSequence.Charging)
                                        {
                                            _logger.Info($"[CallBackResponseDataPrices] Channel is charging. Triggering UI update. ChannelNo: {channel.ChannelNo}");
                                            // UI 스레드에서 실행
                                            Application.Current.Dispatcher.Invoke(() =>
                                            {
                                                try
                                                {
                                                    var mainView = ((App)Application.Current).MainView;
                                                    if (mainView?.DataContext is MainViewModel mainViewModel)
                                                    {
                                                        ChargerViewModel chargerVM = null;

                                                        // 채널 번호로 Left/Right 구분 (0=Left, 1=Right)
                                                        if (channel.ChannelNo == 0 && mainViewModel.LeftChargerView?.DataContext is ChargerViewModel leftVm)
                                                        {
                                                            chargerVM = leftVm;
                                                            _logger.Info($"[CallBackResponseDataPrices] Found left ChargerViewModel for channel {channel.ChannelNo}");
                                                        }
                                                        else if (channel.ChannelNo == 1 && mainViewModel.RightChargerView?.DataContext is ChargerViewModel rightVm)
                                                        {
                                                            chargerVM = rightVm;
                                                            _logger.Info($"[CallBackResponseDataPrices] Found right ChargerViewModel for channel {channel.ChannelNo}");
                                                        }

                                                        if (chargerVM != null)
                                                        {
                                                            // PowerMeter를 현재 값으로 재설정하여 _chargingCost 재계산 트리거
                                                            double currentPower = chargerVM.PowerMeter;
                                                            chargerVM.PowerMeter = currentPower;
                                                            _logger.Info($"[CallBackResponseDataPrices] UI updated. PowerMeter: {currentPower}, ChargingCost will be recalculated with new unit cost: {currentUnitCost}");
                                                        }
                                                        else
                                                        {
                                                            _logger.Warn($"[CallBackResponseDataPrices] ChargerViewModel not found for channel {channel.ChannelNo}");
                                                        }
                                                    }
                                                    else
                                                    {
                                                        _logger.Warn($"[CallBackResponseDataPrices] MainView or MainViewModel not found");
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    _logger.Error($"[CallBackResponseDataPrices] Error updating UI: {ex.Message}");
                                                }
                                            });
                                        }
                                        break;
                                    }
                                }
                            }

                            if (!channelFound)
                            {
                                _logger.Warn($"[CallBackResponseDataPrices] Channel not found for stationId: {stationId}, chargerId: {chargerId}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"[CallBackResponseDataPrices] Error updating current unit cost: {ex.Message}, StackTrace: {ex.StackTrace}");
                    }
                    // ========== 변경 끝 ==========

                    response = "1";
                }
                else if (!applyNow)
                {
                    // 미래 적용 스케줄: DB에 저장 완료, INI는 apply_date 도래 시 반영
                    _logger.Info($"[CallBackResponseDataPrices] Future schedule stored. applyDate: {applyDate}. INI will be updated when apply_date is reached.");
                    response = "1";
                }
                else
                {
                    _logger.Error($"[CallBackResponseDataPrices] ChangeChargingUnitPrices failed. ErrorCode: {errorCode}");
                }
            }
            catch (Exception e)
            {
                response = "0";
                _logger.Error($"[CallBackResponseDataPrices] Error: {e.Message}, StackTrace: {e.StackTrace}");
            }
            finally
            {
                _dataSendEventPrices("PRICES", (string[])null, response, requestJson, httpListenerContext);
            }
        }

        private void CallBackResponseDataReset(string requestJson, HttpListenerContext httpListenerContext)
        {
            string response = "0";
            try
            {
                JSonParser jsonParser = _evCommForm.GetJSonParser();
                JObject reqJObject = JObject.Parse(requestJson);

                string stationId = jsonParser.GetJSonData(reqJObject, "station_id");
                string chargerId = jsonParser.GetJSonData(reqJObject, "charger_id");
                string errorCode = null;
                if (_chargerDelegate.ResetCharger(stationId, chargerId, out errorCode))
                {
                    response = "1";
                    
                    // 충전기 리셋 성공 후 Windows 재부팅
                    _logger.Info($"[CallBackResponseDataReset] Charger reset successful. Rebooting Windows... stationId: {stationId}, chargerId: {chargerId}");
                    try
                    {
                        Process.Start("shutdown.exe", "-r -f -t 00");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"[CallBackResponseDataReset] Failed to reboot Windows: {ex.Message}");
                    }
                }
            }
            catch (Exception e)
            {
                response = "0";
                _logger.Error($"[CallBackResponseDataReset] Error: {e.Message}");
            }
            finally
            {
                _dataSendEventReset("RESET", (string[])null, response, requestJson, httpListenerContext);
            }
        }

        private void CallBackResponseDataAuth(string recvJson, HttpListenerContext httpListenerContext)
        {
            string reponse = "0";

            string stationId = null;
            string chargerId = null;
            string tid = null;
            string chargerType = null;
            string errorCode = null;
            bool isQrPaymentAllowed = false; // QR 결제 허용 여부 (QR팝업 OR 커넥터선택 OR 결제방법선택)

            try
            {
                JSonParser jsonParser = _evCommForm.GetJSonParser();
                JObject reqJObject = JObject.Parse(recvJson);

                stationId = jsonParser.GetJSonData(reqJObject, "station_id");
                chargerId = jsonParser.GetJSonData(reqJObject, "charger_id");
                tid = jsonParser.GetJSonData(reqJObject, "tid");
                chargerType = jsonParser.GetJSonData(reqJObject, "chargetype");

                // QR 결제 허용 화면 여부 확인: QR 팝업, 커넥터 선택, 결제 방법 선택 화면
                try
                {
                    isQrPaymentAllowed = Application.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            var mainView = ((App)Application.Current).MainView;
                            if (mainView == null)
                            {
                                _logger.Info($"[CallBackResponseDataAuth] MainView is null");
                                return false;
                            }

                            if (!(mainView.DataContext is MainViewModel mainViewModel))
                            {
                                _logger.Info($"[CallBackResponseDataAuth] MainView.DataContext is not MainViewModel. Type: {mainView.DataContext?.GetType().FullName}");
                                return false;
                            }

                            _logger.Info($"[CallBackResponseDataAuth] MainViewModel found. PopupView: {mainViewModel.PopupView?.GetType().Name ?? "null"}");

                            // 1. QR 팝업이 열려있으면 허용
                            if (mainViewModel.PopupView is QrCodePopupView)
                            {
                                _logger.Info($"[CallBackResponseDataAuth] QR popup is open. Allowing.");
                                return true;
                            }

                            // 2. 다른 팝업이 열려있으면 거부
                            if (mainViewModel.PopupView != null)
                            {
                                _logger.Info($"[CallBackResponseDataAuth] Other popup is open: {mainViewModel.PopupView.GetType().Name}. Rejecting.");
                                return false;
                            }

                            // 3. 팝업 없음: 해당 채널이 커넥터 선택 또는 결제 방법 선택 화면인지 확인
                            var charger = ((App)Application.Current).Charger;
                            if (charger != null)
                            {
                                for (int i = 0; i < charger.Channels.Length; i++)
                                {
                                    var channel = charger.Channels[i];
                                    if (channel != null && channel.StationId == stationId && channel.ChargerId == chargerId)
                                    {
                                        var seq = channel.CurrentSequence;
                                        _logger.Info($"[CallBackResponseDataAuth] Channel {i} CurrentSequence: {seq}");
                                        if (seq == ChargeSequence.SelectConnector || seq == ChargeSequence.SelectPaymentMethod)
                                        {
                                            _logger.Info($"[CallBackResponseDataAuth] Channel {i} is on {seq} screen. Allowing.");
                                            return true;
                                        }
                                        else
                                        {
                                            _logger.Info($"[CallBackResponseDataAuth] Channel {i} is on {seq} screen. Rejecting.");
                                            return false;
                                        }
                                    }
                                }
                            }

                            _logger.Info($"[CallBackResponseDataAuth] No matching channel found. stationId: {stationId}, chargerId: {chargerId}");
                            return false;
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"[CallBackResponseDataAuth] Error in Dispatcher.Invoke: {ex.Message}, StackTrace: {ex.StackTrace}");
                            return false;
                        }
                    });
                }
                catch (Exception ex)
                {
                    _logger.Error($"[CallBackResponseDataAuth] Error checking QR payment allowed status: {ex.Message}, StackTrace: {ex.StackTrace}");
                }

                if (isQrPaymentAllowed)
                {
                    // QR 결제 시 DSP 상태 이상, 비상정지, 점검중 중 하나일 경우 거부
                    int dspStatus = AppSettingsManager.EvCommSettings.EVSE_DSP_Status;
                    int emergencyStatus = AppSettingsManager.EvCommSettings.EVSE_EmergencyStop;
                    int evseStatus = AppSettingsManager.EvCommSettings.EVSE_Status;
                    
                    // DSP 상태 이상(1) 또는 비상정지(1) 또는 점검중(1 또는 2)
                    if (dspStatus == 1 || emergencyStatus == 1 || evseStatus == 1 || evseStatus == 2)
                    {
                        reponse = "0";
                        _logger.Info($"[CallBackResponseDataAuth] QR payment rejected due to abnormal status. DSP: {dspStatus}, Emergency: {emergencyStatus}, EVSE: {evseStatus}");
                    }
                    else
                    {
                        reponse = "1";
                        _logger.Info($"[CallBackResponseDataAuth] QR payment allowed. Accepting auth request. stationId: {stationId}, chargerId: {chargerId}, tid: {tid}");
                    }
                }
                else
                {
                    reponse = "0";
                    _logger.Info($"[CallBackResponseDataAuth] QR payment not allowed on current screen. Rejecting auth request. stationId: {stationId}, chargerId: {chargerId}, tid: {tid}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex.StackTrace);
                reponse = "0";
            }
            _dataSendEventAuth("AUTH", (string[])null, reponse, recvJson, httpListenerContext);

            // 성공 여부와 관계없이 원격 명령 수행 결과 전송
            // stationId와 chargerId가 있으면 무조건 SendRemoteDone 호출
            if(!string.IsNullOrEmpty(stationId) && !string.IsNullOrEmpty(chargerId))
            {
                bool success = reponse == "1";
                string resultMsg = "03"; // 기본값: 기타
                
                // QR 결제 시 DSP 상태 이상, 비상정지, 점검중 중 하나일 경우 result_msg 02 (QR 팝업 여부와 관계없이)
                int dspStatus = AppSettingsManager.EvCommSettings.EVSE_DSP_Status;
                int emergencyStatus = AppSettingsManager.EvCommSettings.EVSE_EmergencyStop;
                int evseStatus = AppSettingsManager.EvCommSettings.EVSE_Status;
                
                // DSP 상태 이상(1) 또는 비상정지(1) 또는 점검중(1 또는 2)
                if (dspStatus == 1 || emergencyStatus == 1 || evseStatus == 1 || evseStatus == 2)
                {
                    resultMsg = "02";
                    _logger.Info($"[CallBackResponseDataAuth] QR payment result_msg set to 02 due to abnormal status. DSP: {dspStatus}, Emergency: {emergencyStatus}, EVSE: {evseStatus}");
                }
                // QR 결제 허용 화면이 아닌 경우에도 해당 충전기가 충전 중이면 result_msg 01
                else if (!isQrPaymentAllowed)
                {
                    bool isCharging = false;
                    try
                    {
                        var charger = ((App)Application.Current).Charger;
                        if (charger != null)
                        {
                            for (int i = 0; i < charger.Channels.Length; i++)
                            {
                                var channel = charger.Channels[i];
                                if (channel != null && channel.StationId == stationId && channel.ChargerId == chargerId)
                                {
                                    isCharging = channel.CurrentSequence == ChargeSequence.Charging || 
                                                charger.CheckChargingRun(i);
                                    break;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"[CallBackResponseDataAuth] Error checking charging status: {ex.Message}");
                    }
                    
                    if (isCharging)
                    {
                        resultMsg = "01";
                        _logger.Info($"[CallBackResponseDataAuth] QR payment result_msg set to 01 because charger is charging. stationId: {stationId}, chargerId: {chargerId}");
                    }
                }
                else if (success)
                {
                    resultMsg = "11"; // 정상 상태: 성공
                }
                
                SendRemoteDone(stationId, chargerId, tid ?? "", "/charger/auth", reponse, resultMsg);
                
                // 성공한 경우에만 StartChargingAndRemoteDone 호출 (chargerType 필요)
                if (success && !string.IsNullOrEmpty(chargerType))
                {
                    _chargerDelegate.StartChargingAndRemoteDone(stationId, chargerId, tid, chargerType, out errorCode);
                }
            }
            else
            {
                // stationId나 chargerId가 없어도 SendRemoteDone 호출 시도 (빈 값으로)
                // 단, 실제로는 SendRemoteDone 내부에서도 검증할 수 있으므로 로그만 남기고 호출하지 않을 수도 있음
                _logger.Warn($"[CallBackResponseDataAuth] Cannot send REMOTEDONE: stationId={stationId}, chargerId={chargerId}");
            }
        }

        private void CallBackResponseDataStop(string recvJson, HttpListenerContext httpListenerContext)
        {
            string reponse = "0";

            string stationId = null;
            string chargerId = null;
            string tid = null;
            string errorCode = null;

            try
            {
                JSonParser jsonParser = _evCommForm.GetJSonParser();
                JObject reqJObject = JObject.Parse(recvJson);

                stationId = jsonParser.GetJSonData(reqJObject, "station_id");
                chargerId = jsonParser.GetJSonData(reqJObject, "charger_id");
                tid = jsonParser.GetJSonData(reqJObject, "tid");

                // 충전 중 상태 확인
                bool isCharging = false;
                try
                {
                    var charger = ((App)Application.Current).Charger;
                    if (charger != null)
                    {
                        for (int i = 0; i < charger.Channels.Length; i++)
                        {
                            var channel = charger.Channels[i];
                            if (channel != null && channel.StationId == stationId && channel.ChargerId == chargerId)
                            {
                                isCharging = channel.CurrentSequence == ChargeSequence.Charging || 
                                            charger.CheckChargingRun(i);
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"[CallBackResponseDataStop] Error checking charging status: {ex.Message}");
                }

                if (isCharging)
                {
                    reponse = "1";
                }
                else
                {
                    reponse = "0";
                    _logger.Info($"[CallBackResponseDataStop] Charger is not charging. Rejecting stop request. stationId: {stationId}, chargerId: {chargerId}, tid: {tid}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                _logger.Error($"[CallBackResponseDataStop] Error: {ex.Message}");
                reponse = "0";
            }
            _dataSendEventStop("STOP", (string[])null, reponse, recvJson, httpListenerContext);

            // 성공 여부와 관계없이 원격 명령 수행 결과 전송
            // stationId와 chargerId가 있으면 무조건 SendRemoteDone 호출
            if (!string.IsNullOrEmpty(stationId) && !string.IsNullOrEmpty(chargerId))
            {
                bool success = reponse == "1";
                string resultMsg = success ? "11" : "03"; // 11: 성공, 03: 기타
                SendRemoteDone(stationId, chargerId, tid ?? "", "/charger/stop", reponse, resultMsg);
                
                if (success)
                {
                    _chargerDelegate.StopChargingAndRemoteDone(stationId, chargerId, tid, out errorCode);
                }
            }
            else
            {
                // stationId나 chargerId가 없어도 SendRemoteDone 호출 시도 (빈 값으로)
                // 단, 실제로는 SendRemoteDone 내부에서도 검증할 수 있으므로 로그만 남기고 호출하지 않을 수도 있음
                _logger.Warn($"[CallBackResponseDataStop] Cannot send REMOTEDONE: stationId={stationId}, chargerId={chargerId}");
            }

        }

        private void CallBackResponseDataDump(string recvJson, HttpListenerContext httpListenerContext)
        { 
            string reponse = "0";

            string stationId = null;
            string chargerId = null;
            string dumpType = null;
            string dumpStartTime = null;
            string dumpEndTime = null;
            string errorCode = null;

            try
            {
                JSonParser jsonParser = _evCommForm.GetJSonParser();
                JObject reqJObject = JObject.Parse(recvJson);

                stationId = jsonParser.GetJSonData(reqJObject, "station_id");
                chargerId = jsonParser.GetJSonData(reqJObject, "charger_id");
                dumpType = jsonParser.GetJSonData(reqJObject, "dump_type");
                dumpStartTime = jsonParser.GetJSonData(reqJObject, "dump_start_time");
                dumpEndTime = jsonParser.GetJSonData(reqJObject, "dump_end_type");


                reponse = "1";
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                reponse = "0";
            }
            _dataSendEventDump("DUMP", (string[])null, reponse, recvJson, httpListenerContext);

            if (!string.IsNullOrEmpty(stationId) && !string.IsNullOrEmpty(chargerId))
                _chargerDelegate.DumpReq(stationId, chargerId, dumpType, dumpStartTime, dumpEndTime, out errorCode);
        }





        #endregion

        #region Send to Server Method
        public bool SendUser(ref ChargerChannel chargerChannel)
        {
            JSonParser jSonParser = _evCommForm.GetJSonParser();
            DataParser dataParser = _evCommForm.GetDataParser();

            ChargingSession session = new ChargingSession();
            session.InitChargingInfo(chargerChannel.StationId, chargerChannel.ChargerId);
            session.card_num = chargerChannel.MembershipNo;
            session.authtype = "1";

            bool result = false;

            try
            {
                JObject retJObject = _evCommForm.SendUser(session, DateTime.Now);

                if (retJObject != null)
                {
                    LogForRecv("USER", retJObject);
                    _evCommForm.LogRecvServerComm("USER", retJObject, DateTime.Now);

                    chargerChannel.MembershipNoValidationCode = "-1";

                    string responseReceiveStr = jSonParser.GetJSonData(retJObject, "response_receive");
                    if (int.TryParse(responseReceiveStr, out int responseReceive) && responseReceive == 1)
                    {
                        chargerChannel.MembershipNoValidationCode = jSonParser.GetJSonData(retJObject, "valid");
                        chargerChannel.IsChargerPaymentRequired = dataParser.CheckYN(jSonParser.GetJSonData(retJObject, "charger_pay_yn"));

                        result = "1" == chargerChannel.MembershipNoValidationCode;
                    }

                    IsServerConnected = true;

                    if (result)
                    {
                        chargerChannel.MemberCompanyCode = jSonParser.GetJSonData(retJObject, "member_company");
                        string unitCostStr = jSonParser.GetJSonData(retJObject, "current_unit_cost");
                        chargerChannel.CurrentUserUnitCost = float.TryParse(unitCostStr, out float unitCost)
                            ? unitCost
                            : ChargerChannel.DefaultUnitCost;
                    }
                    else
                    {
                        chargerChannel.MemberCompanyCode = "";
                        chargerChannel.CurrentUserUnitCost = ChargerChannel.DefaultUnitCost;
                    }
                }
                else
                {
                    _logger.Error("[RECV] USER : no response!!!");
                    IsServerConnected = false;
                    chargerChannel.MemberCompanyCode = "";
                    chargerChannel.CurrentUserUnitCost = ChargerChannel.DefaultUnitCost;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[SendUser] 예외 발생: {ex.Message}");
                IsServerConnected = false;
                chargerChannel.MemberCompanyCode = "";
                chargerChannel.CurrentUserUnitCost = ChargerChannel.DefaultUnitCost;
            }

            return result;
        }

        public bool SendChargerStatus(string stationId, string chargerId, int mode, int chargerState, int chargerDoor, int chargerPlug, uint integratedPower, string powerbox)
        {
            ChargingSession session = new ChargingSession();
            session.InitChargingInfo(stationId, chargerId);
            session.mode = mode;
            session.charger_state = chargerState;
            session.charger_door = chargerDoor;
            session.charger_plug = chargerPlug;
            session.integrated_power = integratedPower;
            session.powerbox = powerbox;

            JSonParser jSonParser = _evCommForm.GetJSonParser();
            bool result = false;

            JObject retJObject = _evCommForm.SendChargerStatus(session, DateTime.Now);

            if (retJObject != null)
            {
                LogForRecv("STATUS", retJObject);
                _evCommForm.LogRecvServerComm("STATUS", retJObject, DateTime.Now);
                result = Convert.ToInt32(jSonParser.GetJSonData(retJObject, "response_receive")) == 1;
                IsServerConnected = true;
            }
            else
            {
                _logger.Error("[RECV] STATUS : no response!!!");
                IsServerConnected = false;
                result = false;
            }

            return result;
        }

        public bool SendChargingStart(string stationId, string chargerId, string createDate, string startDate, string cardNumber,
            string previousTrno, string previousDate, string payType, string chargerPayYn, int chargerType, uint integratedPower, 
            int beforeCost, int currentV, int currentA, string estimatedChargeTime, string unitCost, string chargingRate, string orderNo )
        {
            ChargingSession session = new ChargingSession();
            session.InitChargingInfo(stationId, chargerId);

            session.create_date = createDate;
            session.start_date = startDate;
            session.card_num = cardNumber;
            session.previous_trno = previousTrno;
            session.previous_date = previousDate;
            session.pay_type = payType;
            session.charger_pay_yn = chargerPayYn;
            session.charger_type = chargerType;
            session.integrated_power = integratedPower;
            session.before_cost = beforeCost;
            session.current_V = currentV;
            session.current_A = currentA;
            session.estimated_charge_time = estimatedChargeTime;
            session.unit_cost = unitCost;
            session.charging_rate = chargingRate;
            session.order_no = orderNo;


            JSonParser jSonParser = _evCommForm.GetJSonParser();
            bool result = false;

            JObject retJObject = _evCommForm.SendChargingStart(session, DateTime.Now);

            if (retJObject != null)
            {
                LogForRecv("START", retJObject);
                _evCommForm.LogRecvServerComm("START", retJObject, DateTime.Now);
                result = Convert.ToInt32(jSonParser.GetJSonData(retJObject, "response_receive")) == 1;
                IsServerConnected = true;
            }
            else
            {
                _logger.Error("[RECV] START : no response!!!");
                IsServerConnected = false;
                result = false;
            }

            return result;
        }

        public bool SendChargingInfo(string stationId, string chargerId, string createDate, string startDate, string cardNumber,
            string previousTrno, string previousDate, string payType, string chargerPayYn, int chargerType, uint integratedPower,
            int beforeCost, int currentV, int currentA, string estimatedChargeTime, uint chargeW, int chargeCost, string unitCost, string chargingRate, string orderNo)
        {
            ChargingSession session = new ChargingSession();
            session.InitChargingInfo(stationId, chargerId); 

            session.create_date = createDate;
            session.start_date = startDate;
            session.card_num = cardNumber;
            session.previous_trno = previousTrno;
            session.previous_date = previousDate;
            session.pay_type = payType;
            session.charger_pay_yn = chargerPayYn;
            session.charger_type = chargerType;
            session.integrated_power = integratedPower;
            session.before_cost = beforeCost;
            session.current_V = currentV;
            session.current_A = currentA;
            session.estimated_charge_time = estimatedChargeTime;
            session.charge_W = chargeW;
            session.charge_cost = chargeCost;
            session.unit_cost = unitCost;
            session.charging_rate = chargingRate;
            session.order_no = orderNo;


            JSonParser jSonParser = _evCommForm.GetJSonParser();
            bool result = false;

            JObject retJObject = _evCommForm.SendChargingInfo(session, DateTime.Now);

            if (retJObject != null)
            {
                LogForRecv("INFO", retJObject);
                _evCommForm.LogRecvServerComm("INFO", retJObject, DateTime.Now);
                result = Convert.ToInt32(jSonParser.GetJSonData(retJObject, "response_receive")) == 1;
                IsServerConnected = true;
            }
            else
            {
                _logger.Error("[RECV] INFO : no response!!!");
                IsServerConnected = false;
                result = false;
            }

            return result;

        }

        public bool SendChargingEnd(string stationId, string chargerId, string createDate, string startDate, string cardNumber,
            string previousTrno, string previousDate, string payType, string chargerPayYn, int chargerType, uint integratedPower,
            int beforeCost, int currentV, int currentA, int chargeTime, uint chargeW, int chargeEndType, string endDate, 
            int afterCost, int cancelCost, string pointKind, string cancelDate, string cancelResult, string unitCost, string chargingRate, string orderNo)
        {
            ChargingSession session = new ChargingSession();
            session.InitChargingInfo(stationId, chargerId);

            session.create_date = createDate;
            session.start_date = startDate;
            session.card_num = cardNumber;
            session.previous_trno = previousTrno;
            session.previous_date = previousDate;
            session.pay_type = payType;
            session.charger_pay_yn = chargerPayYn;
            session.charger_type = chargerType;
            session.integrated_power = integratedPower;
            session.before_cost = beforeCost;
            session.current_V = currentV;
            session.current_A = currentA;
            session.charge_time = chargeTime;
            session.charge_W = chargeW;
            session.charge_end_type = chargeEndType;
            session.end_date = endDate;
            session.after_cost = afterCost;
            session.cancel_cost = cancelCost;
            session.point_kind = pointKind;
            session.cancel_date = cancelDate;
            session.cancel_result = cancelResult;
            session.unit_cost = unitCost;
            session.charging_rate = chargingRate;
            session.order_no = orderNo;

            

            JSonParser jSonParser = _evCommForm.GetJSonParser();
            bool result = false;

            JObject retJObject = _evCommForm.SendChargingEnd(session, DateTime.Now);

            if (retJObject != null)
            {
                LogForRecv("END", retJObject);
                _evCommForm.LogRecvServerComm("END", retJObject, DateTime.Now);
                result = Convert.ToInt32(jSonParser.GetJSonData(retJObject, "response_receive")) == 1;
                IsServerConnected = true;
            }
            else
            {
                _logger.Error("[RECV] END : no response!!!");
                IsServerConnected = false;
                result = false;
            }

            return result;
        }

        public bool SendAlarmHistory(string stationId, string chargerId, string alarmType, string alarmDate, string alarmCode)
        {
            // alarm_code=0000(=0)은 서버로 전송하지 않는다. (스팸/오탐 방지)
            if (string.IsNullOrWhiteSpace(alarmCode) || alarmCode == "0000")
            {
                _logger?.Debug($"[SendAlarmHistory] Skipped sending alarm with code=0000. stationId={stationId}, chargerId={chargerId}, alarmType={alarmType}");
                return true;
            }

            AlarmHistory alarm = new AlarmHistory();
            alarm.InitAlarmInfo(stationId, chargerId);

            alarm.alarm_type = alarmType;
            alarm.alarm_date = alarmDate;
            alarm.alarm_code = alarmCode;

            JSonParser jSonParser = _evCommForm.GetJSonParser();
            bool result = false;

            JObject retJObject = _evCommForm.SendAlarmHistory(alarm, DateTime.Now);

            if (retJObject != null)
            {
                LogForRecv("ALARM", retJObject);
                _evCommForm.LogRecvServerComm("ALARM", retJObject, DateTime.Now);
                result = Convert.ToInt32(jSonParser.GetJSonData(retJObject, "response_receive")) == 1;
                IsServerConnected = true;
            }
            else
            {
                _logger.Error("[RECV] ALARM : no response!!!");
                IsServerConnected = false;
                result = false;
            }

            return result;
        }

        public JObject SendDumpChargerStatus(string stationId, string chargerId, int dumpType, string dumpStartTime, string dumpEndType)
        {
            ChargingSession session = new ChargingSession();
            session.InitChargingInfo(stationId, chargerId);
            session.dump_type = dumpType;
            session.dump_start_time = dumpStartTime;
            session.dump_end_type = dumpEndType;

            return _evCommForm.SendDumpCmd(session, "STATUS", DateTime.Now);
        }

        public JObject SendDumpChargingStart(string stationId, string chargerId, int dumpType, string dumpStartTime, string dumpEndType)
        {
            ChargingSession session = new ChargingSession();
            session.InitChargingInfo(stationId, chargerId);
            session.dump_type = dumpType;
            session.dump_start_time = dumpStartTime;
            session.dump_end_type = dumpEndType;

            return _evCommForm.SendDumpCmd(session, "START", DateTime.Now);
        }

        public JObject SendDumpChargingInfo(string stationId, string chargerId, int dumpType, string dumpStartTime, string dumpEndType)
        {
            ChargingSession session = new ChargingSession();
            session.InitChargingInfo(stationId, chargerId);
            session.dump_type = dumpType;
            session.dump_start_time = dumpStartTime;
            session.dump_end_type = dumpEndType;

            return _evCommForm.SendDumpCmd(session, "INFO", DateTime.Now);
        }

        public JObject SendDumpChargingEnd(string stationId, string chargerId, int dumpType, string dumpStartTime, string dumpEndType)
        {
            ChargingSession session = new ChargingSession();
            session.InitChargingInfo(stationId, chargerId);
            session.dump_type = dumpType;
            session.dump_start_time = dumpStartTime;
            session.dump_end_type = dumpEndType;

            return _evCommForm.SendDumpCmd(session, "END", DateTime.Now);
        }

        public JObject SendDumpAlarmHistory(string stationId, string chargerId, int dumpType, string dumpStartTime, string dumpEndType)
        {
            ChargingSession session = new ChargingSession();
            session.InitChargingInfo(stationId, chargerId);
            session.dump_type = dumpType;
            session.dump_start_time = dumpStartTime;
            session.dump_end_type = dumpEndType;

            return _evCommForm.SendDumpCmd(session, "ALARM", DateTime.Now);
        }

        public bool SendInsertResv(string stationId, string chargerId, string cardNumber, out string reservationNo)
        {
            ReserveSession session = new ReserveSession();
            session.InitReserveSession(stationId, chargerId);
            session.create_date = DateTime.Now.ToString("yyyyMMddHHmmss");
            session.card_num = cardNumber;
            session.resv_stat = "0";

            JSonParser jSonParser = _evCommForm.GetJSonParser();

            bool result = false;
            reservationNo = null;

            JObject retJObject = _evCommForm.SendInsertResv(session, DateTime.Now);

            if (retJObject != null)
            {
                LogForRecv("INSERTRESV", retJObject);
                _evCommForm.LogRecvServerComm("INSERTRESV", retJObject, DateTime.Now);
                IsServerConnected = true;
            }
            else
            {
                _logger.Error("[RECV] INSERTRESV : no response!!!");
                IsServerConnected = false;
            }

            if (retJObject != null)
            {
                // error_code 확인
                string errorCodeStr = jSonParser.GetJSonData(retJObject, "error_code");
                if (!string.IsNullOrEmpty(errorCodeStr) && Convert.ToInt32(errorCodeStr) == 1)
                {
                    // error_code가 1이면 예약 실패
                    result = false;
                }
                else if (Convert.ToInt32(jSonParser.GetJSonData(retJObject, "ret")) == 1)
                {
                    reservationNo = jSonParser.GetJSonData(retJObject, "resv_seq");
                    result = true;
                }
            }


            return result;
        }

        public bool SendSendSMS(string stationId, string chargerId, string cardNumber, string msg, string msgType, string data1, string data2, string data3, string data4, string data5 )
        {
            ReserveSession session = new ReserveSession();
            session.InitReserveSession(stationId, chargerId);

            session.card_num = cardNumber;
            session.msg = msg;
            session.msg_type = msgType;
            session.Data1 = data1;
            session.Data2 = data2;
            session.Data3 = data3;
            session.Data4 = data4;
            session.Data5 = data5;

            

            JSonParser jSonParser = _evCommForm.GetJSonParser();
            bool result = false;

            JObject retJObject = _evCommForm.SendSendSMS(session, DateTime.Now);

            if (retJObject != null)
            {
                LogForRecv("SENDSMS", retJObject);
                _evCommForm.LogRecvServerComm("SENDSMS", retJObject, DateTime.Now);
                result = Convert.ToInt32(jSonParser.GetJSonData(retJObject, "response_receive")) == 1;
                Debug.WriteLine($"SendSendSMS result: {result}");
                IsServerConnected = true;
            }
            else
            {
                _logger.Error("[RECV] SENDSMS : no response!!!");
                IsServerConnected = false;
                result = false;
            }

            return result;
        }

        public JObject SendResvCnt(string stationId)
        {
            ReserveSession session = new ReserveSession();
            session.station_id = stationId;
            return _evCommForm.SendResvCnt(session, DateTime.Now);
        }

        public bool SendResvStation(string stationId, out string phoneNo, out string reservationNo)
        {
            ReserveSession session = new ReserveSession();
            session.station_id = stationId;

            JSonParser jSonParser = _evCommForm.GetJSonParser();

            bool result = false;
            phoneNo = null;
            reservationNo = null;

            JObject retJObject = _evCommForm.SendResvStation(session, DateTime.Now);

            if (retJObject != null)
            {
                LogForRecv("RESVSTATION", retJObject);
                _evCommForm.LogRecvServerComm("RESVSTATION", retJObject, DateTime.Now);

                bool responseReceiveOk = Convert.ToInt32(jSonParser.GetJSonData(retJObject, "response_receive")) == 1;
                IsServerConnected = true;

                if (responseReceiveOk)
                {
                    TrySyncWindowsTimeOnce(retJObject);

                    string ret = jSonParser.GetJSonData(retJObject, "ret");

                    if (!string.IsNullOrEmpty(ret) && ret.Contains(":"))
                    {
                        string[] retArr = ret.Split(':');

                        phoneNo = retArr[0];    
                        reservationNo = retArr[1];
                        result = true;
                    }
                }
            }
            else
            {
                _logger.Error("[RECV] RESVSTATION : no response!!!");
                IsServerConnected = false;
            }

            return result;
        }

        public bool SendAuthResv(string stationId, string cardNumber)
        {
            ReserveSession session = new ReserveSession();
            session.station_id = stationId;
            session.card_num = cardNumber;
            session.resv_stat = "1";

           
            JSonParser jSonParser = _evCommForm.GetJSonParser();
            bool result = false;

            JObject retJObject = _evCommForm.SendAuthResv(session, DateTime.Now);

            if (retJObject != null)
            {
                LogForRecv("AUTHRESV", retJObject);
                _evCommForm.LogRecvServerComm("AUTHRESV", retJObject, DateTime.Now);
                IsServerConnected = true;
            }
            else
            {
                _logger.Error("[RECV] AUTHRESV : no response!!!");
                IsServerConnected = false;
            }

            if(retJObject != null && Convert.ToInt32(jSonParser.GetJSonData(retJObject, "response_receive")) == 1)
            {
                result = Convert.ToInt32(jSonParser.GetJSonData(retJObject, "ret")) == 1;
            }

            return result;

        }

        public bool SendCancelResv(string stationId, string createDate, string cardNumber)
        {
            ReserveSession session = new ReserveSession();
            session.station_id = stationId;
            session.create_date = createDate;
            session.card_num = cardNumber;

            JSonParser jSonParser = _evCommForm.GetJSonParser();
            bool result = false;

            JObject retJObject = _evCommForm.SendCancelResv(session, DateTime.Now);

            if (retJObject != null)
            {
                LogForRecv("CANCELRESV", retJObject);
                _evCommForm.LogRecvServerComm("CANCELRESV", retJObject, DateTime.Now);
                IsServerConnected = true;
            }
            else
            {
                _logger.Error("[RECV] CANCELRESV : no response!!!");
                IsServerConnected = false;
            }

            if (retJObject != null && Convert.ToInt32(jSonParser.GetJSonData(retJObject, "response_receive")) == 1)
            {
                result = Convert.ToInt32(jSonParser.GetJSonData(retJObject, "ret")) == 1;
            }

            return result;

        }

        public JObject SendRTimeChargerStatus(
            string stationId,
            string chargerId,
            string responseDate,
            string uiVer,
            string chargerStatus,
            string rfStatus,
            string icStatus,
            string appStartDate,
            string stopButtonStatus,
            string chargingMode,
            string electricityMeterMode,
            string uiMode,
            string powerModule,
            string freeSpace,
            string avaMem,
            string timelimitYn,
            string timelimitValue,
            string testYn,
            string payYn,
            string volumeDay,
            string volumeNight,
            string volumemovieDay,
            string volumemovieNight,
            string chargerFirmware,
            string noticeCnt,
            string systemDate,
            string lcdIp,
            string currentUnitCost
            )
        {
            RTimeChargerStatus session = new RTimeChargerStatus();

            session.InitRTimeChargerStatus(stationId, chargerId);
            session.SetDataSendRTimeChargerStatus(responseDate, uiVer, chargerStatus, rfStatus, icStatus,
                appStartDate, stopButtonStatus, chargingMode, electricityMeterMode,uiMode,
                powerModule, freeSpace, avaMem, timelimitYn, timelimitValue, testYn, payYn,volumeDay, volumeNight,
                volumemovieDay, volumemovieNight, chargerFirmware, noticeCnt, systemDate, lcdIp, currentUnitCost);

            JObject retJObject = _evCommForm.SendRTimeChargerStatus(session, DateTime.Now);

            if (retJObject != null)
            {
                LogForRecv("RTIMESTATUS", retJObject);
                _evCommForm.LogRecvServerComm("RTIMESTATUS", retJObject, DateTime.Now);
                IsServerConnected = true;

                // ⚠️ [BUG FIX] RTIMESTATUS 응답에는 response_date 필드가 없음!
                // 시간 동기화는 response_date가 있는 응답(STATUS, USER 등)에서 수행되어야 함
                // TrySyncWindowsTimeOnce(retJObject);  // ✗ 제거됨 - 불필요한 경고 로그 제거
                _logger.Debug("[RTIMESTATUS] response_date 필드 없음 → 시간 동기화 건너뜀");
            }
            else
            {
                _logger.Error("[RECV] RTIMESTATUS : no response!!!");
                IsServerConnected = false;
            }

            return retJObject;
        }

        public JObject SendCheckCurrentUnitCost(string stationId, string chargerId)
        {
            Console.WriteLine("SendCheckCurrentUnitCost called");
            CheckCurrentUnitCost session = new CheckCurrentUnitCost();
            session.InitCheckCurrentUnitCost(stationId, chargerId);

            JObject retJObject = _evCommForm.SendCheckCurrentUnitCost(session, DateTime.Now);

            if (retJObject != null)
            {
                LogForRecv("CHECKCURRENTUNITCOST", retJObject);
                _evCommForm.LogRecvServerComm("CHECKCURRENTUNITCOST", retJObject, DateTime.Now);
                IsServerConnected = true;
            }
            else
            {
                _logger.Error("[RECV] CHECKCURRENTUNITCOST : no response!!!");
                IsServerConnected = false;
            }

            return retJObject;
        }

        public JObject SendCheckUpdate(string stationId, string chargerId, string versiongKind, string newVersion)
        {
            CheckUpdate session = new CheckUpdate();
            session.InitCheckUpdate(stationId, chargerId);

            JObject retJObject = _evCommForm.SendCheckUpdate(session, DateTime.Now, versiongKind, newVersion);

            if (retJObject != null)
            {
                LogForRecv("CHECKUPDATE", retJObject);
                _evCommForm.LogRecvServerComm("CHECKUPDATE", retJObject, DateTime.Now);
                IsServerConnected = true;
            }
            else
            {
                _logger.Error("[RECV] CHECKUPDATE : no response!!!");
                IsServerConnected = false;
            }

            return retJObject;
        }

        public bool SendPathUpdate(string patchId, string versionKind, string versionNo, string patchFile, string md5, string stationId, string chargerId)
        {
            bool result = _evCommForm.SendPathUpdate(patchId, versionKind, versionNo, patchFile, md5, DateTime.Now, stationId, chargerId);
            
            if (result)
            {
                IsServerConnected = true;
            }
            else
            {
                _logger.Error("[RECV] PATHUPDATE : no response or failed!!!");
                IsServerConnected = false;
            }

            return result;
        }

        public bool SendRemoteDone(string stationId, string chargerId, string tid, string cmd, string result, string resultMsg)
        {
            try
            {
                RemoteDone session = new RemoteDone();
                session.InitRemoteDone(stationId, chargerId);

                JObject retJObject = _evCommForm.SendRemoteDone(session, DateTime.Now, cmd, result, resultMsg, tid);

                if (retJObject != null)
                {
                    LogForRecv("REMOTEDONE", retJObject);
                    _evCommForm.LogRecvServerComm("REMOTEDONE", retJObject, DateTime.Now);
                    IsServerConnected = true;
                    return true;
                }
                else
                {
                    _logger.Error("[SEND] REMOTEDONE : no response!!!");
                    IsServerConnected = false;
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[SendRemoteDone] Error: {ex.Message}");
                return false;
            }
        }
        #endregion

        private void HandleServerDataSent(string delimeter, string requestJson)
        {
            try
            {
                // CHECKSTATUS에 대한 응답으로 다시 상태를 보내면 무한 루프가 발생할 수 있으므로 제외합니다.
                if (delimeter == "CHECKSTATUS" || string.IsNullOrEmpty(requestJson))
                {
                    return;
                }

                JSonParser jsonParser = _evCommForm.GetJSonParser();
                JObject reqJObject = JObject.Parse(requestJson);

                string stationId = jsonParser.GetJSonData(reqJObject, "station_id");
                string chargerId = jsonParser.GetJSonData(reqJObject, "charger_id");

                if (string.IsNullOrEmpty(stationId) || string.IsNullOrEmpty(chargerId))
                {
                    _logger.Warn($"[HandleServerDataSent] Could not get stationId or chargerId from request for delimeter: {delimeter}");
                    return;
                }

                string errorCode = null;
                // 이 메서드를 호출하면 Charger.cs 내부에서 SendRTimeChargerStatus()가 실행됩니다.
                _chargerDelegate.GetChargerInfo(stationId, chargerId, out errorCode);

                if (!string.IsNullOrEmpty(errorCode))
                {
                    _logger.Error($"[HandleServerDataSent] GetChargerInfo returned an error: {errorCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[HandleServerDataSent] Exception: {ex.Message}");
            }
        }

        #region Private Method
        private double[] GetDoublueValuesForHour(JObject jObject, double defaultValue)
        {
            double[] values = new double[24];
            JSonParser jsonParser = _evCommForm.GetJSonParser();

            for (int i = 0; i < values.Length; i++)
            {
                string key = string.Format("H{0:D2}", i);
                string strValue = jsonParser.GetJSonData(jObject, key);
                double value = defaultValue;
                if (strValue != null)
                {
                    value = Convert.ToDouble(strValue);
                    if (value <= 0) value = defaultValue;
                }
                values[i] = value;

            }

            return values;
        }

        private void LogForRecv(string delimiter, JObject jobj)
        {
            string str = jobj.ToString().Replace("\r\n", "").Trim();
            _logger.Info("[RECV] "+delimiter+" : " + str);
        }

        /// <summary>
        /// 프로그램 실행 중 최초 1회만 시간 동기화 시도
        /// </summary>
        private void TrySyncWindowsTimeOnce(JObject retJObject)
        {
            // 이미 동기화를 시도한 경우 skip
            if (_isTimeSyncedOnStartup)
            {
                _logger.Info("[TIME_SYNC] 시작 시 동기화 시도는 이미 실행되었습니다. 건너뜁니다.");
                return;
            }

            // 성공/실패와 무관하게 앱 실행 중 1회만 시도
            _isTimeSyncedOnStartup = true;

            _logger.Info("[TIME_SYNC] ===== 시간 동기화 시작 (앱 실행 중 1회 시도) =====");

            try
            {
                // 파싱 전 로컬 시각 캡처 (파싱 소요 시간 오차 최소화)
                DateTime localTime = DateTime.Now;

                JSonParser jSonParser = _evCommForm.GetJSonParser();
                string responseDateStr = jSonParser.GetJSonData(retJObject, "response_date");

                _logger.Info($"[TIME_SYNC] 서버 응답 원본값 (response_date) = '{responseDateStr}'");

                if (string.IsNullOrEmpty(responseDateStr))
                {
                    _logger.Info("[TIME_SYNC] response_date가 없는 응답입니다. 1회 시도 정책에 따라 재시도하지 않습니다.");
                    return;
                }

                // 다양한 형식으로 파싱 시도
                DateTime serverTime;
                if (!TryParseServerDateTime(responseDateStr, out serverTime))
                {
                    _logger.Warn($"[TIME_SYNC] response_date 파싱 실패: '{responseDateStr}'. 1회 시도 정책에 따라 재시도하지 않습니다.");
                    return;
                }

                _logger.Info($"[TIME_SYNC] 파싱된 서버 시각 = {serverTime:yyyy-MM-dd HH:mm:ss}");

                TimeSpan diff = serverTime - localTime;
                bool isAhead = diff.TotalSeconds > 0;
                TimeSpan absDiff = diff.Duration();

                _logger.Info($"[TIME_SYNC] 로컬={localTime:yyyy-MM-dd HH:mm:ss.fff}, 서버={serverTime:yyyy-MM-dd HH:mm:ss}, 차이={absDiff.TotalSeconds:F1}초 ({(isAhead ? "서버가 빠름" : "서버가 느림")})");

                // 5초 이상 차이 나는 경우에만 동기화
                if (absDiff.TotalSeconds >= 5.0)
                {
                    _logger.Info($"[TIME_SYNC] 차이 5초 이상 → SetSystemTime 호출 중...");
                    bool success = _systemSettingsService.SetSystemTime(serverTime);
                    if (success)
                    {
                        _logger.Info($"[TIME_SYNC] ✓ Windows 시간 동기화 완료. 변경 전={localTime:yyyy-MM-dd HH:mm:ss}, 변경 후={serverTime:yyyy-MM-dd HH:mm:ss}");
                    }
                    else
                    {
                        _logger.Warn($"[TIME_SYNC] ✗ SetSystemTime 실패. 관리자 권한을 확인하세요. (Win32 오류코드는 SetSystemTime 로그 참고)");
                    }
                }
                else
                {
                    _logger.Info($"[TIME_SYNC] 차이 5초 미만 ({absDiff.TotalSeconds:F1}초). 동기화가 필요하지 않습니다.");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[TIME_SYNC] TrySyncWindowsTimeOnce 처리 중 예외 발생: {ex.Message}", ex);
            }
            finally
            {
                _logger.Info("[TIME_SYNC] ===== 시간 동기화 시도 완료 =====");
            }
        }

        /// <summary>
        /// 서버 응답의 response_date를 다양한 형식으로 파싱
        /// </summary>
        private bool TryParseServerDateTime(string dateStr, out DateTime result)
        {
            result = DateTime.MinValue;

            // 1) yyyyMMddHHmmss (14자리 숫자 문자열)
            if (dateStr.Length == 14 && dateStr.All(char.IsDigit))
            {
                try
                {
                    int year = int.Parse(dateStr.Substring(0, 4));
                    int month = int.Parse(dateStr.Substring(4, 2));
                    int day = int.Parse(dateStr.Substring(6, 2));
                    int hour = int.Parse(dateStr.Substring(8, 2));
                    int minute = int.Parse(dateStr.Substring(10, 2));
                    int second = int.Parse(dateStr.Substring(12, 2));
                    result = new DateTime(year, month, day, hour, minute, second);
                    _logger.Info($"[TIME_SYNC] [yyyyMMddHHmmss] 형식으로 파싱 성공: {result:yyyy-MM-dd HH:mm:ss}");
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.Warn($"[TIME_SYNC] [yyyyMMddHHmmss] 형식 파싱 오류: {ex.Message}, 입력값='{dateStr}'");
                }
            }

            // 2) yyyy-MM-dd HH:mm:ss
            if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd HH:mm:ss",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out result))
            {
                _logger.Info($"[TIME_SYNC] [yyyy-MM-dd HH:mm:ss] 형식으로 파싱 성공: {result:yyyy-MM-dd HH:mm:ss}");
                return true;
            }

            // 3) ISO 8601 (with or without 'T')
            if (DateTime.TryParse(dateStr, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeLocal, out result))
            {
                _logger.Info($"[TIME_SYNC] [ISO 8601] 형식으로 파싱 성공: {result:yyyy-MM-dd HH:mm:ss}");
                return true;
            }

            _logger.Warn($"[TIME_SYNC] 모든 날짜 형식 파싱 실패. 입력값='{dateStr}'");
            return false;
        }


        #endregion
    }
}
