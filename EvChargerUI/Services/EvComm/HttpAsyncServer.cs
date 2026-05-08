// Decompiled with JetBrains decompiler
// Type: EvComm.HttpAsyncServer
// Assembly: EvComm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: ACDC758A-085B-43B8-BB89-A752CE49F7D6
// Assembly location: C:\Users\SJYOON\Downloads\EVCharger_v0216\EvComm.dll

using EvChargerUI.Commons.Settings;
using EvChargerUI.Commons.Util;
using JoasUtils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows;

namespace EvChargerUI.Services.EvComm
{
  public class HttpAsyncServer
  {
    public delegate void DataSentEventHandler(string delimeter, string requestJson);
    public event DataSentEventHandler DataSent;

    public DataGetEventHandler DataSendEventReset;
    public DataGetEventHandler DataSendEventPrices;
    public DataGetEventHandler DataSendEventDisplayBrightness;
    public DataGetEventHandler DataSendEventSound;
    public DataGetEventHandler DataSendEventUpdate;
    public DataGetEventHandler DataSendEventStatus;
    public DataGetEventHandler DataSendEventDump;
    public DataGetEventHandler DataSendEventNotice;
    public DataGetEventHandler DataSendEventCheckStatus;
    public DataGetEventHandler DataSendEventLimit;
    public DataGetEventHandler DataSendEventTest;
    public DataGetEventHandler DataSendEventPayYn;
    public DataGetEventHandler DataSendEventAuth;
    public DataGetEventHandler DataSendEventStop;
    


    private string[] listenedAddresses;
    private bool isWorked = false;
    private HttpListener listener;
    private ResponseServer responseServer;
    private string delimeter;
    private EvCommForm evComm;
    private Socket serverSock;

    private FileLogger _logger = ((App)Application.Current).AppLogger;


    public HttpAsyncServer(
      string[] listenedAddresses,
      ResponseServer responseServer,
      string delimeter,
      EvCommForm evComm)
    {
      this.evComm = evComm;
      this.listenedAddresses = listenedAddresses;
      this.responseServer = responseServer;
      this.delimeter = delimeter;
    }

    private void work()
    {
      this.listener = new HttpListener();
      string originalPrefix = this.listenedAddresses[0];
      try
      {
        Uri originalUri = new Uri(originalPrefix);
        string modifiedPrefix = string.Format("{0}://+:{1}{2}", originalUri.Scheme, originalUri.Port, originalUri.PathAndQuery);
        this.listener.Prefixes.Add(modifiedPrefix);
        _logger.Info($"HttpAsyncServer listening on: {modifiedPrefix}");
      }
      catch (UriFormatException ex)
      {
        _logger.Error($"HttpAsyncServer UriFormatException: {ex.Message}. Falling back to original prefix: {originalPrefix}");
        this.listener.Prefixes.Add(originalPrefix); // Fallback to original if format is wrong
      }
      try
      {
        this.listener.Start();
        Console.WriteLine("Listener Start");
      }
      catch (HttpListenerException ex)
      {
                return;
      }
      while (this.isWorked)
      {
        try
        {
            Console.WriteLine("Listening...");
          HttpListenerContext context = this.listener.GetContext();
          HttpListenerResponse response = context.Response;

          string param = this.ParseUrlGetData(context.Request.Url.ToString());
          this.LogForRecv(delimeter, param);

          this.DataSend(this.delimeter, param, context);
        }
        catch (Exception ex)
        {
                    Console.WriteLine(ex.ToString());
        }
      }
      this.stop();
    }

    private string ParseUrlGetData(string url)
    {
      return url.Split(new string[1]{ "?param=" }, StringSplitOptions.None)[1].Replace("%2C", ",");
    }

    public IPAddress GetLocalIP()
    {
      IPAddress localIp = (IPAddress) null;
      try
      {
        foreach (IPAddress address in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
        {
          if (address.AddressFamily == AddressFamily.InterNetwork)
          {
            localIp = address;
            break;
          }
        }
      }
      catch (Exception ex)
      {
        localIp = (IPAddress) null;
      }
      return localIp;
    }

    private void DataSend(string delimeter, string text, HttpListenerContext context)
    {
      try
      {
        _logger.Info("DataSend Enter");

        _logger.Info(delimeter);

        switch (delimeter)
        {
          case "AUTH":
                        _logger.Info($"[DataSend] AUTH 이벤트 호출");
                        this.DataSendEventAuth(text, context);            
            break;
          case "CHECKSTATUS":
                        _logger.Info($"[DataSend] CHECKSTATUS 이벤트 호출");
                        this.DataSendEventCheckStatus(text, context);
            break;
          case "DISPLAYBRIGHTNESS":
                        _logger.Info($"[DataSend] DISPLAYBRIGHTNESS 이벤트 호출");
                        this.DataSendEventDisplayBrightness(text, context);
            break;
          case "DUMP":
                        _logger.Info($"[DataSend] DUMP 이벤트 호출");
                        this.DataSendEventDump(text, context);
            break;
          case "LIMIT":
                        _logger.Info($"[DataSend] LIMIT 이벤트 호출");
                        this.DataSendEventLimit(text, context);
            break;
          case "NOTICE":
                        _logger.Info($"[DataSend] NOTICE 이벤트 호출");
                        this.DataSendEventNotice(text, context);
            break;
          case "PAYYN":
                        _logger.Info($"[DataSend] PAYYN 이벤트 호출");
                        this.DataSendEventPayYn(text, context);
            break;
          case "PRICES":
                        _logger.Info($"[DataSend] PRICES 이벤트 호출");
                        this.DataSendEventPrices(text, context);
            break;
          case "RESET":
            _logger.Info($"[DataSend] RESET 이벤트 호출");
            this.DataSendEventReset(text, context);
            break;
          case "SOUND":
            _logger.Info($"[DataSend] SOUND 이벤트 호출");
            this.DataSendEventSound(text, context);
            break;
          case "STATUS":
            _logger.Info($"[DataSend] STATUS 이벤트 호출");
            this.DataSendEventStatus(text, context);
            break;
          case "STOP":
            _logger.Info($"[DataSend] STOP 이벤트 호출");
            this.DataSendEventStop(text, context);
            break;
          case "TEST":
            _logger.Info($"[DataSend] TEST 이벤트 호출");
            this.DataSendEventTest(text, context);
            break;
          case "UPDATE":
            _logger.Info($"[DataSend] UPDATE 이벤트 호출");
            this.DataSendEventUpdate(text, context);
            break;
          default:
            _logger.Info($"[DataSend] default");
            break;
        }
        this.DataSent?.Invoke(delimeter, text); 
      }
      catch (HttpListenerException ex)
      {
        Console.WriteLine(ex.Message);
      }
      catch (Exception ex)
      {
        Console.WriteLine(ex.Message);
      }
    }

    public void LogForRecv(string delimiter, string msg)
    {
        _logger.Info("[RECV] " + delimiter + " : " + msg);
    }

    private string parseUrl(string delimeter, string[] data, string response, string responseJson)
    {
      string url = "";
      switch (delimeter)
      {
        case "RESET":
          var anonymousTypeObject1 = new
          {
            station_id = "",
            charger_id = "",
            send_date = "",
            create_date = ""
          };
          var data1 = JsonConvert.DeserializeAnonymousType(responseJson, anonymousTypeObject1);
          url = "{\"station_id\":\"" + data1.station_id + "\",\"charger_id\":\"" + data1.charger_id + "\",\"response_date\":\"" + DateTime.Now.ToString("yyyyMMddHHmmss") + "\",\"create_date\":\"" + data1.create_date + "\",\"response_receive\":\"" + response + "\"}";
          break;
        case "PRICES":
          var anonymousTypeObject2 = new
          {
            station_id = "",
            charger_id = "",
            send_date = "",
            create_date = "",
            apply_date = "",
            end_date = "",
            H00 = "",
            H01 = "",
            H02 = "",
            H03 = "",
            H04 = "",
            H05 = "",
            H06 = "",
            H07 = "",
            H08 = "",
            H09 = "",
            H10 = "",
            H11 = "",
            H12 = "",
            H13 = "",
            H14 = "",
            H15 = "",
            H16 = "",
            H17 = "",
            H18 = "",
            H19 = "",
            H20 = "",
            H21 = "",
            H22 = "",
            H23 = ""
          };
          var data2 = JsonConvert.DeserializeAnonymousType(responseJson, anonymousTypeObject2);
          url = "{\"station_id\":\"" + data2.station_id + "\",\"charger_id\":\"" + data2.charger_id + "\",\"response_date\":\"" + DateTime.Now.ToString("yyyyMMddHHmmss") + "\",\"create_date\":\"" + data2.create_date + "\",\"response_receive\":\"" + response + "\"}";
          break;
        case "DISPLAYBRIGHTNESS":
          var anonymousTypeObject3 = new
          {
            station_id = "",
            charger_id = "",
            send_date = "",
            create_date = "",
            day = "",
            night = ""
          };
          var data3 = JsonConvert.DeserializeAnonymousType(responseJson, anonymousTypeObject3);
          url = "{\"station_id\":\"" + data3.station_id + "\",\"charger_id\":\"" + data3.charger_id + "\",\"response_date\":\"" + DateTime.Now.ToString("yyyyMMddHHmmss") + "\",\"create_date\":\"" + data3.create_date + "\",\"response_receive\":\"" + response + "\"}";
          break;
        case "SOUND":
          var anonymousTypeObject4 = new
          {
            station_id = "",
            charger_id = "",
            send_date = "",
            create_date = "",
            day = "",
            night = ""
          };
          var data4 = JsonConvert.DeserializeAnonymousType(responseJson, anonymousTypeObject4);
          url = "{\"station_id\":\"" + data4.station_id + "\",\"charger_id\":\"" + data4.charger_id + "\",\"response_date\":\"" + DateTime.Now.ToString("yyyyMMddHHmmss") + "\",\"create_date\":\"" + data4.create_date + "\",\"response_receive\":\"" + response + "\"}";
          break;
                    /*
        case "UPDATE":
          var anonymousTypeObject5 = new
          {
            station_id = "",
            charger_id = "",
            send_date = "",
            create_date = "",
            patch_id = "",
            ver_kind = "",
            patch_file = "",
            md5 = ""
          };
          var data5 = JsonConvert.DeserializeAnonymousType(responseJson, anonymousTypeObject5);
          url = "{\"station_id\":\"" + data5.station_id + "\",\"charger_id\":\"" + data5.charger_id + "\",\"response_date\":\"" + DateTime.Now.ToString("yyyyMMddHHmmss") + "\",\"create_date\":\"" + data5.create_date + "\",\"response_receive\":\"" + response + "\"}";
          break;
                    */
        case "STATUS":
            _logger.Info($"[parseUrl] STATUS --------------------------------");
          var anonymousTypeObject6 = new
          {
            station_id = "",
            charger_id = "",
            send_date = "",
            create_date = "",
            change_status = ""
          };
          var data6 = JsonConvert.DeserializeAnonymousType(responseJson, anonymousTypeObject6);

            // 1. 변수 선언 및 현재 상태 로드
            int currentEvseStatus = AppSettingsManager.EvCommSettings.EVSE_Status;
            int newEvseStatus;
            // response 초기값 설정 (성공 시 "1"로 변경됨)
            response = "0";

            // 2. 새로운 상태 값 파싱 (오류 처리 포함)
            if (!int.TryParse(data6.change_status, out newEvseStatus))
            {
                _logger.Error($"[HttpAsyncServer] STATUS: Failed to parse change_status value '{data6.change_status}' :: 파싱 실패");

                newEvseStatus = currentEvseStatus;
                response = "fail"; // 응답 결과도 실패로 변경
            }

            // 3. 이전/이후 상태 기록
            int before_status = currentEvseStatus;
            int after_status = newEvseStatus;

            // 4. 정상(0)으로 설정하려는 경우, 상태 변경 여부와 관계없이 조건 체크
            if (newEvseStatus == 0)
            {
                // EVSE_Status를 임시로 0으로 설정하여 조건 체크 (체크 후 롤백 가능)
                int originalEvseStatus = AppSettingsManager.EvCommSettings.EVSE_Status;
                AppSettingsManager.EvCommSettings.EVSE_Status = 0; // 임시 설정

                bool canChangeToNormal = CheckAndUpdateChargerMode();
                
                if (!canChangeToNormal)
                {
                    // 조건 미충족: DSP 연결 문제 또는 Emergency 상태
                    // EVSE_Status 원래 값으로 복구 및 실패 응답
                    AppSettingsManager.EvCommSettings.EVSE_Status = before_status;
                    response = "0"; // 실패 응답
                    _logger.Warn($"[HttpAsyncServer] STATUS: Cannot change to normal status. DSP/Emergency issue. Response set to 0, EVSE_Status rolled back to {before_status} :: 미조건");
                }
                else
                {
                    // 조건 충족: 정상 변경 가능
                    response = "1"; // 성공 응답
                    if (before_status != after_status)
                    {
                        // 상태가 실제로 변경된 경우에만 저장
                        AppSettingsManager.EvCommSettings.EVSE_Status = newEvseStatus;
                        AppSettingsManager.EvCommSettings.ChargerMode = 1; // 상태 변경 

                        AppSettingsManager.Save();
                        _logger.Info($"[HttpAsyncServer] STATUS: Successfully changed to normal status. EVSE_Status: {newEvseStatus} :: 상태변경");
                    }
                    else
                    {
                        // 상태 변경 없지만 조건 충족
                        AppSettingsManager.EvCommSettings.EVSE_Status = before_status; // 원래 값 복구 (변경 없음)
                        _logger.Info($"[HttpAsyncServer] STATUS: Already in normal status. Condition check passed. Response set to '{before_status}' / '{after_status}' :: 상태 동일");
                    }
                }
            }
            else if (before_status != after_status)
            {
                // 점검중(1) 또는 중지(2)로 변경하는 경우는 항상 허용 (상태가 실제로 변경될 때만)
                response = "1"; // 성공 응답
                AppSettingsManager.EvCommSettings.EVSE_Status = newEvseStatus;
                // EVSE_Status 변경 시 ChargerMode도 함께 업데이트
                // change_status: 0=정상, 1=점검중, 2=중지
                // ChargerMode: 0=알 수 없음, 1=운영중, 2=운영중지, 3=점검중
                if (newEvseStatus == 1) // 점검중
                {
                    AppSettingsManager.EvCommSettings.ChargerMode = 3;
                }
                else if (newEvseStatus == 2) // 중지
                {
                    AppSettingsManager.EvCommSettings.ChargerMode = 2;
                }
                AppSettingsManager.Save();
                _logger.Info($"[HttpAsyncServer] STATUS: Successfully changed to status {newEvseStatus}. EVSE_Status: {newEvseStatus}, ChargerMode: {AppSettingsManager.EvCommSettings.ChargerMode} :: 응답 성공");
            }

            // 5. 응답 URL 생성 (롤백된 경우 after_status를 원래 값으로 표시)
            int final_status = AppSettingsManager.EvCommSettings.EVSE_Status;
            url = "{\"station_id\":\"" + data6.station_id + "\",\"charger_id\":\"" + data6.charger_id + "\",\"response_date\":\"" + DateTime.Now.ToString("yyyyMMddHHmmss") + "\",\"create_date\":\"" + data6.create_date + "\",\"before_status\":\"" + before_status + "\",\"after_status\":\"" + final_status + "\",\"response_receive\":\"" + response + "\"}";
          break;
        case "DUMP":
          var anonymousTypeObject7 = new
          {
            station_id = "",
            charger_id = "",
            send_date = "",
            create_date = "",
            dump_type = "",
            dump_start_time = "",
            dump_end_type = ""
          };
          var data7 = JsonConvert.DeserializeAnonymousType(responseJson, anonymousTypeObject7);
          url = "{\"station_id\":\"" + data7.station_id + "\",\"charger_id\":\"" + data7.charger_id + "\",\"response_date\":\"" + DateTime.Now.ToString("yyyyMMddHHmmss") + "\",\"create_date\":\"" + data7.create_date + "\",\"response_receive\":\"" + response + "\"}";
          break;
        case "NOTICE":
          var anonymousTypeObject8 = new
          {
            station_id = "",
            charger_id = "",
            send_date = "",
            create_date = "",
            card_num = "",
            msg = "",
            msg_type = "",
            Data1 = "",
            Data2 = "",
            Data3 = "",
            Data4 = "",
            Data5 = ""
          };
          var data8 = JsonConvert.DeserializeAnonymousType(responseJson, anonymousTypeObject8);
          url = "{\"station_id\":\"" + data8.station_id + "\",\"charger_id\":\"" + data8.charger_id + "\",\"response_date\":\"" + DateTime.Now.ToString("yyyyMMddHHmmss") + "\",\"create_date\":\"" + data8.create_date + "\",\"response_receive\":\"" + response + "\"}";
          break;
        case "CHECKSTATUS":
          url = response;
          break;
        case "LIMIT":
          var anonymousTypeObject9 = new
          {
            station_id = "",
            charger_id = "",
            send_date = "",
            create_date = "",
            timelimit_yn = "",
            timelimit_value = ""
          };
          var data9 = JsonConvert.DeserializeAnonymousType(responseJson, anonymousTypeObject9);
          url = "{\"station_id\":\"" + data9.station_id + "\",\"charger_id\":\"" + data9.charger_id + "\",\"response_date\":\"" + DateTime.Now.ToString("yyyyMMddHHmmss") + "\",\"create_date\":\"" + data9.create_date + "\",\"response_receive\":\"" + response + "\"}";
          break;
        case "TEST":
          var anonymousTypeObject10 = new
          {
            station_id = "",
            charger_id = "",
            send_date = "",
            create_date = "",
            test_yn = ""
          };
          var data10 = JsonConvert.DeserializeAnonymousType(responseJson, anonymousTypeObject10);
            /////////////////////////////////////
            // 1. 변수 선언 및 현재 상태 로드
            var currentEvseTest = AppSettingsManager.EvCommSettings.EVSE_Test;
            var newEvseTest= data10.test_yn;
            // 2. 이전/이후 상태 기록
            var before_TestYn = currentEvseTest;
            var after_TestYn = newEvseTest;

            // 3. 설정 업데이트 및 저장 (상태가 실제로 변경되었을 때만 저장)
            if (before_TestYn != newEvseTest)
            {
                AppSettingsManager.EvCommSettings.EVSE_Test = newEvseTest;
                AppSettingsManager.Save();
            }
            // 4. 응답 URL 생성            

            url = "{\"station_id\":\"" + data10.station_id + "\",\"charger_id\":\"" + data10.charger_id + "\",\"response_date\":\"" + DateTime.Now.ToString("yyyyMMddHHmmss") + "\",\"create_date\":\"" + data10.create_date + "\",\"response_receive\":\"" + response + "\"}";
          break;
        case "PAYYN":
          var anonymousTypeObject11 = new
          {
            station_id = "",
            charger_id = "",
            send_date = "",
            create_date = "",
            pay_yn = ""
          };

          var data11 = JsonConvert.DeserializeAnonymousType(responseJson, anonymousTypeObject11);
            //////////////////////////
            /////////////////////////////////////
            // 1. 변수 선언 및 현재 상태 로드
            var currentEvsePayYN = AppSettingsManager.EvCommSettings.EVSE_PayYN;
            var newEvsePayYN = data11.pay_yn;
            // 2. 이전/이후 상태 기록
            var before_PayYN = currentEvsePayYN;
            var after_PayYN = newEvsePayYN;

            // 3. 설정 업데이트 및 저장 (상태가 실제로 변경되었을 때만 저장)
            if (before_PayYN != newEvsePayYN)
            {
                AppSettingsManager.EvCommSettings.EVSE_PayYN = newEvsePayYN;
                AppSettingsManager.Save();
            }
            // 4. 응답 URL 생성   
            //////////////////////////
            url = "{\"station_id\":\"" + data11.station_id + "\",\"charger_id\":\"" + data11.charger_id + "\",\"response_date\":\"" + DateTime.Now.ToString("yyyyMMddHHmmss") + "\",\"create_date\":\"" + data11.create_date + "\",\"response_receive\":\"" + response + "\"}";
          break;
        case "AUTH":
          var anonymousTypeObject12 = new
          {
            station_id = "",
            charger_id = "",
            send_date = "",
            create_date = "",
            tid = "",
            chargetype = ""
          };
          var data12 = JsonConvert.DeserializeAnonymousType(responseJson, anonymousTypeObject12);
          url = "{\"station_id\":\"" + data12.station_id + "\",\"charger_id\":\"" + data12.charger_id + "\",\"response_date\":\"" + DateTime.Now.ToString("yyyyMMddHHmmss") + "\",\"create_date\":\"" + data12.create_date + "\",\"response_receive\":\"" + response + "\"}";
          break;
        case "STOP":
          var anonymousTypeObject13 = new
          {
            station_id = "",
            charger_id = "",
            send_date = "",
            create_date = "",
            tid = ""
          };
          var data13 = JsonConvert.DeserializeAnonymousType(responseJson, anonymousTypeObject13);
          url = "{\"station_id\":\"" + data13.station_id + "\",\"charger_id\":\"" + data13.charger_id + "\",\"response_date\":\"" + DateTime.Now.ToString("yyyyMMddHHmmss") + "\",\"create_date\":\"" + data13.create_date + "\",\"response_receive\":\"" + response + "\"}";
          break;
        case "UPDATE":
            var anonymousTypeObject14 = new
            {
                station_id = "",
                charger_id = "",
                send_date = "",
                create_date = ""         
            };
            var data14 = JsonConvert.DeserializeAnonymousType(responseJson, anonymousTypeObject14);           
            
            url = "{\"station_id\":\"" + data14.station_id + "\",\"charger_id\":\"" + data14.charger_id + "\",\"response_date\":\"" + DateTime.Now.ToString("yyyyMMddHHmmss") + "\",\"create_date\":\"" + data14.create_date + "\",\"response_receive\":\"" + response + "\"}";
            break;
            }
      return url;
    }
        
    public void SetResponse(
      string delimeter,
      string[] data,
      string response,
      string jSon,
      HttpListenerContext httpListenerContext)
    {
      string url = this.parseUrl(delimeter, data, response, jSon);
      byte[] bytes = Encoding.UTF8.GetBytes(url);
      try
      {
        httpListenerContext.Response.ContentLength64 = (long) bytes.Length;
        Stream outputStream = httpListenerContext.Response.OutputStream;
        outputStream.Write(bytes, 0, bytes.Length);
        outputStream.Close();
        this.LogSendFileAndWindows(url);
        
        // 원격명령 처리 후 RTIMECHGRSTAT 요청
        SendRTimeChargerStatusAfterRemoteCommand();
      }
      catch (HttpListenerException ex)
      {
        Console.WriteLine(ex.Message);
      }
      catch (Exception ex)
      {
        Console.WriteLine(ex.Message);
      }
    }

    private void SendRTimeChargerStatusAfterRemoteCommand()
    {
      try
      {
        App app = ((App)Application.Current);
        if (app?.Charger != null)
        {
          // UI 스레드에서 실행
          Application.Current.Dispatcher.BeginInvoke(new Action(() =>
          {
            try
            {
              app.Charger.SendRTimeChargerStatus();
              _logger.Info("[HttpAsyncServer] RTIMECHGRSTAT sent after remote command");
            }
            catch (Exception ex)
            {
              _logger.Error($"[HttpAsyncServer] Failed to send RTIMECHGRSTAT after remote command: {ex.Message}");
            }
          }));
        }
      }
      catch (Exception ex)
      {
        _logger.Error($"[HttpAsyncServer] Error in SendRTimeChargerStatusAfterRemoteCommand: {ex.Message}");
      }
    }

    private void LogSendFileAndWindows(string responseJson)
    {
      JObject jobj = JObject.Parse(responseJson);
      string jsonData = this.evComm.GetJSonParser().GetJSonData(jobj, "response_date");
      this.evComm.LogForSend(this.ConvertDelimeter(), responseJson);
      this.evComm.EvCommLogSendServerComm(this.ConvertDelimeter(), responseJson, DateTime.ParseExact(jsonData, "yyyyMMddHHmmss", (IFormatProvider) null));
    }

    private string ConvertDelimeter()
    {
      return this.delimeter.Equals("STATUS") ? "CHANGE_STATUS" : this.delimeter;
    }

    private bool CheckAndUpdateChargerMode()
    {
        try
        {
            // App에서 Charger 인스턴스 가져오기
            App app = ((App)Application.Current);
            if (app?.Charger == null)
            {
                _logger.Warn("[HttpAsyncServer] CheckAndUpdateChargerMode: Charger instance not available");
                return false;
            }

            // Charger 클래스의 CheckAndUpdateChargerMode 메서드 호출
            // 세 조건(1. DSP 연결 정상, 2. Emergency 없음, 3. EVSE_Status가 점검중 아님) 확인 후 ChargerMode 업데이트
            bool result = app.Charger.CheckAndUpdateChargerMode();
            _logger.Info($"[HttpAsyncServer] CheckAndUpdateChargerMode: ChargerMode updated. Result: {result}");
            return result;

            }
        catch (Exception ex)
        {
            _logger.Error($"[HttpAsyncServer] CheckAndUpdateChargerMode error: {ex.Message}");
            return false;
        }
    }

    public void stop()
    {
      this.isWorked = false;
      try
      {
        this.listener.Stop();
      }
      catch (ObjectDisposedException ex)
      {
        Console.Write(ex.Message);
      }
    }

    public void RunServer()
    {
      this.isWorked = !this.isWorked ? true : throw new Exception("server alredy started");
      new System.Threading.Timer((TimerCallback) (thread => this.work())).Change(1, -1);
      Thread.Sleep(10);
    }
  }
}
