using System;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using EvChargerUI.Commons.Settings;
using EvChargerUI.Commons.Util;
using EvChargerUI.Services.EvComm.HttpJsonRequest;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Application = System.Windows.Application;

namespace EvChargerUI.Services.EvComm
{
  public class EvCommForm : Form
  {
    private object lockObject = new object();
    private string serverURL = "";
    private WebHelper webHelper = new WebHelper();
    public JSonParser jSonParser = new JSonParser();
    public DataParser dataParser = new DataParser();
    public ResponseServer responseServer = new ResponseServer();
    public bool IsShowing = false;
    private JSonChargers jSonChargers;
    private JSonChargingStart jSonChargingStart;
    private JSonChargingInfo jSonChargingInfo;
    private JSonChargingEnd jSonChargingEnd;
    public JSonUser jSonUser;
    private JSonAlarmHistory jSonAlarmHistory;
    private JSonRTimeChargerStatus jSonRTimeChargerStatus;
    private JSonCheckCurrentUnitCost jSonCheckCurrentUnitCost;
    private JSonCheckUpdate jSonCheckUpdate;
    private JSonRemoteDone jSonRemoteDone;
    private IContainer components = (IContainer) null;
    private RichTextBox rtbServerComm;
    private Button button1;
    private Panel panel1;
    private FileLogger _logger; // 디자인 타임 보호: 생성자에서 지연 설정
    private Action<string,string,string,string,bool> _txLogCallback; // (addUrl, stationId, chargerId, requestJson, success)
    public EvCommForm() : this(string.Empty, null) { }

    public EvCommForm(string svrUrl, Action<string,string,string,string,bool> txLogCallback = null)
    {
      this.serverURL = svrUrl;
      this._txLogCallback = txLogCallback;
      // 디자인 타임에서는 WPF App 컨텍스트가 없을 수 있음
      try { _logger = (Application.Current as App)?.AppLogger; } catch { _logger = null; }
      this.InitializeComponent();
      this.rtbServerComm.ScrollToCaret();
      this.StartPosition = FormStartPosition.Manual;
      this.Location = new Point(10, 100);
    }

    public string GetKoreaTime(string format)
    {
      return TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Korea Standard Time").ToString(format);
    }

    public string GetKoreaTime(DateTime time) => time.ToString("yyyyMMddHHmmss");

    public string GetKoreaTimeDisp(DateTime time) => time.ToString("yyyy-MM-dd HH:mm:ss");

    public string GetKoreaTimeHourMin(DateTime time) => time.ToString("HHmm");

    public string GetKoreaTimeHour(DateTime time) => time.Hour.ToString("00");

    public string GetKoreaTimeMin(DateTime time) => time.Minute.ToString("00");

    public string GetKoreaTimeSec(DateTime time) => time.Second.ToString("00");

    public string GetKoreaTimeYear(DateTime time) => time.Year.ToString("0000");

    public string GetKoreaTimeMonth(DateTime time) => time.Month.ToString("00");

    public string GetKoreaTimeDay(DateTime time) => time.Day.ToString("00");

    private void EvCommForm_Load(object sender, EventArgs e)
    {
    }

    public string GetServerURL() => this.serverURL;

    public JSonParser GetJSonParser() => this.jSonParser;

    public DataParser GetDataParser() => this.dataParser;

    public ResponseServer GetResponseServer() => this.responseServer;

    public JObject httpPostResponse(
      string request,
      string addURL,
      string stationId,
      string chargerId,
      string delimiter,
      int timeout)
    {
      string str = Encoding.UTF8.GetString(Encoding.Default.GetBytes("{" + request + "}"));
      Uri url;
      if (chargerId != null)
      {
        try
        {
          url = new Uri(string.Format(this.serverURL + addURL + stationId + "/" + chargerId + "?param=" + str));
        }
        catch (UriFormatException ex)
        {
          return (JObject) null;
        }
      }
      else
      {
        try
        {
          url = new Uri(string.Format(this.serverURL + addURL + stationId + "?param=" + str));
        }
        catch (UriFormatException ex)
        {
          return (JObject) null;
        }
      }
      
      // /station으로 시작하는 엔드포인트인지 확인
      bool isStationEndpoint = addURL != null && addURL.StartsWith("station/");
      bool mockMode = AppSettingsManager.EvCommSettings.MockMode;
      
      // MockMode가 true이면 네트워크 응답 검증 로직 패스
      if (mockMode)
      {
        JObject jobject = this.webHelper.Post(delimiter, url, request, timeout);
        this.CheckTopMost();
        return jobject;
      }
      
      // /station 엔드포인트이고 MockMode가 false인 경우에만 재시도 로직 적용
      if (isStationEndpoint)
      {
        const int maxRetries = 3;
        JObject result = null;
        bool success = false;
        
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
          result = this.webHelper.Post(delimiter, url, request, timeout);
          
          if (result != null)
          {
            success = true;
            // 성공 시 네트워크 상태를 정상(0)으로 복구
            if (AppSettingsManager.EvCommSettings.EVSE_Network_Status != 0)
            {
              AppSettingsManager.EvCommSettings.EVSE_Network_Status = 0;
              AppSettingsManager.Save();
              _logger.Info($"[httpPostResponse] Network connection restored. EVSE_Network_Status set to 0");
            }
            break;
          }
          
          if (attempt < maxRetries)
          {
            _logger.Warn($"[httpPostResponse] Attempt {attempt}/{maxRetries} failed for {addURL}. Retrying...");
            Thread.Sleep(100); // 재시도 전 짧은 대기
          }
        }
        
        // 모든 시도 실패 시 네트워크 상태를 1로 설정
        if (!success)
        {
          _logger.Error($"[httpPostResponse] All {maxRetries} attempts failed for {addURL}. Setting EVSE_Network_Status to 1");
          AppSettingsManager.EvCommSettings.EVSE_Network_Status = 1;
          AppSettingsManager.Save();
        }
        
        // 콜백 호출하여 DB에 로그 기록
        _txLogCallback?.Invoke(addURL, stationId, chargerId, str, success);

        this.CheckTopMost();
        return result;
      }
      else
      {
        JObject jobject = this.webHelper.Post(delimiter, url, request, timeout);
        bool success = jobject != null;
        _txLogCallback?.Invoke(addURL, stationId, chargerId, str, success);

        this.CheckTopMost();
        return jobject;
      }
    }

    public bool httpUpdatePatch(
      string addURL,
      string stationId,
      string chargerId,
      string delimiter,
      string patch_id,
      string patch_file,
      string ver_kind)
    {
      Uri url = (Uri) null;
      if (delimiter.Equals("UPDATE"))
      {
        try
        {
          url = new Uri(string.Format(this.serverURL + addURL + stationId + "/" + chargerId + "/" + patch_id));
        }
        catch (UriFormatException ex)
        {
          return false;
        }
      }
      object[] objArray = this.webHelper.Download_Update(url, patch_file, ver_kind);
      string str = Convert.ToString(objArray[0]);
      bool boolean = Convert.ToBoolean(objArray[1]);
      try
      {
        if (boolean)
        {
          this.LogForUpdate(ver_kind, " " + str + " bytes 다운로드 성공");
          this.EvCommLogUpdateComm(ver_kind, " " + str + " bytes 다운로드 성공", DateTime.Now);
        }
        else
        {
          this.LogForUpdate(ver_kind, " " + str + " byte 다운로드 실패");
          this.EvCommLogUpdateComm(ver_kind, " " + str + " byte 다운로드 실패", DateTime.Now);
        }
      }
      catch (Exception ex)
      {
      }
      this.CheckTopMost();
      return boolean;
    }

    private void CheckTopMost()
    {
      try
      {
        if (!this.IsShowing)
          return;
        this.TopMost = true;
      }
      catch (Exception ex)
      {
      }
    }

    public JSonChargers GetJSonChargers() => this.jSonChargers;

    public JSonChargingStart GetJSonChargingStart() => this.jSonChargingStart;

    public JSonChargingInfo GetJSonChargingInfo() => this.jSonChargingInfo;

    public JSonChargingEnd GetJSonChargingEnd() => this.jSonChargingEnd;

    public JSonAlarmHistory GetJSonAlarmHistory() => this.jSonAlarmHistory;

    public JSonRTimeChargerStatus GetJSonRTimeChargerStatus() => this.jSonRTimeChargerStatus;

    public JObject SendChargerStatus(ChargingSession session, DateTime time)
    {
      this.jSonChargers = (JSonChargers) null;
      this.jSonChargers = new JSonChargers()
      {
        station_id = session.station_id,
        charger_id = session.charger_id,
        send_date = this.GetKoreaTime(time),
        create_date = this.GetKoreaTime(time),
        mode = session.mode.ToString(),
        charger_state = session.charger_state.ToString(),
        charger_door = session.charger_door.ToString(),
        charger_plug = session.charger_plug.ToString(),
        integrated_power = session.integrated_power.ToString(),
        powerbox = session.powerbox
      };
      string request = JsonConvert.SerializeObject((object) this.jSonChargers);
      this.LogForSend("STATUS", request);
      this.EvCommLogSendServerComm("STATUS", request, time);
      return this.httpPostResponse(request, "station/chargers/", session.station_id, session.charger_id, "STATUS", 1000);
    }

    public void EvCommLogSendServerComm(string delimeter, string request, DateTime time)
    {
      if (this.InvokeRequired)
        this.Invoke(new Action(() => this.LogSendServerComm(delimeter, request, time)));
      else
        this.LogSendServerComm(delimeter, request, time);
    }

    public void EvCommLogUpdateComm(string delimeter, string request, DateTime time)
    {
      if (this.InvokeRequired)
        this.Invoke(new Action(() => this.LogUpdateComm(delimeter, request, time)));
      else
        this.LogUpdateComm(delimeter, request, time);
    }

    public JObject SendChargingStart(ChargingSession session, DateTime time)
    {
      this.jSonChargingStart = (JSonChargingStart) null;
      this.jSonChargingStart = new JSonChargingStart()
      {
        station_id = session.station_id,
        charger_id = session.charger_id,
        send_date = this.GetKoreaTime(time),
        create_date = session.create_date,
        start_date = session.start_date,
        card_num = session.card_num,
        previous_trno = session.previous_trno,
        previous_date = session.previous_date,
        pay_type = session.pay_type,
        charger_pay_yn = session.charger_pay_yn,
        charger_type = session.charger_type.ToString(),
        integrated_power = session.integrated_power.ToString(),
        before_cost = session.before_cost.ToString(),
        current_V = session.current_V.ToString(),
        current_A = session.current_A.ToString(),
        estimated_charge_time = session.estimated_charge_time,
        unit_cost = session.unit_cost.ToString(),
        charging_rate = session.charging_rate,
        order_no = session.order_no
      };
      string request = JsonConvert.SerializeObject((object) this.jSonChargingStart);
      this.LogForSend("START", request);
      this.EvCommLogSendServerComm("START", request, time);
      return this.httpPostResponse(request, "station/chargingStart/", session.station_id, session.charger_id, "START", 1000);
    }

    public JObject SendChargingInfo(ChargingSession session, DateTime time)
    {
      this.jSonChargingInfo = (JSonChargingInfo) null;
      this.jSonChargingInfo = new JSonChargingInfo()
      {
        station_id = session.station_id,
        charger_id = session.charger_id,
        send_date = this.GetKoreaTime(time),
        create_date = this.GetKoreaTime(time),
        start_date = session.start_date,
        card_num = session.card_num,
        previous_trno = session.previous_trno,
        previous_date = session.previous_date,
        pay_type = session.pay_type,
        charger_pay_yn = session.charger_pay_yn,
        charger_type = session.charger_type.ToString(),
        integrated_power = session.integrated_power.ToString(),
        before_cost = session.before_cost.ToString(),
        current_V = session.current_V.ToString(),
        current_A = session.current_A.ToString(),
        estimated_charge_time = session.estimated_charge_time,
        charge_W = session.charge_W.ToString(),
        charge_cost = session.charge_cost.ToString(),
        unit_cost = session.unit_cost.ToString(),
        charging_rate = session.charging_rate,
        order_no = session.order_no

      };
      string request = JsonConvert.SerializeObject((object) this.jSonChargingInfo);
      this.LogForSend("INFO", request);
      this.EvCommLogSendServerComm("INFO", request, time);
      return this.httpPostResponse(request, "station/chargingInfo/", session.station_id, session.charger_id, "PROCEED", 1000);
    }

    public JObject SendChargingEnd(ChargingSession session, DateTime time)
    {
      this.jSonChargingEnd = (JSonChargingEnd) null;
      this.jSonChargingEnd = new JSonChargingEnd()
      {
        station_id = session.station_id,
        charger_id = session.charger_id,
        send_date = this.GetKoreaTime(time),
        create_date = session.create_date,
        start_date = session.start_date,
        card_num = session.card_num,
        previous_trno = session.previous_trno,
        previous_date = session.previous_date,
        pay_type = session.pay_type,
        charger_pay_yn = session.charger_pay_yn,
        charger_type = session.charger_type.ToString(),
        integrated_power = session.integrated_power.ToString(),
        before_cost = session.before_cost.ToString(),
        current_V = session.current_V.ToString(),
        current_A = session.current_A.ToString(),
        charge_time = session.charge_time.ToString(),
        charge_W = session.charge_W.ToString(),
        charge_end_type = session.charge_end_type.ToString(),
        end_date = session.end_date,
        after_cost = session.after_cost.ToString(),
        cancel_cost = session.cancel_cost.ToString(),
        point_kind = session.point_kind,
        cancel_date = session.cancel_date,
        cancel_result = session.cancel_result,
        unit_cost = session.unit_cost.ToString(),
        charging_rate = session.charging_rate,
        order_no = session.order_no
      };
      string request = JsonConvert.SerializeObject((object) this.jSonChargingEnd);
      this.LogForSend("END", request);
      this.EvCommLogSendServerComm("END", request, time);
      return this.httpPostResponse(request, "station/chargingEnd/", session.station_id, session.charger_id, "END", 1000);
    }

    public JObject SendUser(ChargingSession session, DateTime time)
    {
      this.jSonUser = (JSonUser) null;
      this.jSonUser = new JSonUser()
      {
        station_id = session.station_id,
        charger_id = session.charger_id,
        send_date = this.GetKoreaTime(time),
        create_date = this.GetKoreaTime(time),
        card_num = session.card_num,
        Data = "",
        authtype = session.authtype
      };
      string request = JsonConvert.SerializeObject((object) this.jSonUser);
      this.LogForSend("USER", request);
      this.EvCommLogSendServerComm("USER", request, time);
      return this.httpPostResponse(request, "station/user/", session.station_id, session.charger_id, "USER", 1000);
    }

    public JObject SendAlarmHistory(AlarmHistory alarm, DateTime time)
    {
      this.jSonAlarmHistory = new JSonAlarmHistory()
      {
        station_id = alarm.station_id,
        charger_id = alarm.charger_id,
        send_date = this.GetKoreaTime(time),
        create_date = this.GetKoreaTime(time),
        alarm_type = alarm.alarm_type.ToString(),
        alarm_date = alarm.alarm_date,
        alarm_code = alarm.alarm_code
      };
      string request = JsonConvert.SerializeObject((object) this.jSonAlarmHistory);
      this.LogForSend("ALARM", request);
      this.EvCommLogSendServerComm("ALARM", request, time);
      return this.httpPostResponse(request, "station/alarmHistory/", alarm.station_id, alarm.charger_id, "ALARM", 1000);
    }

    public JObject SendDumpCmd(ChargingSession session, string delimiter, DateTime time)
    {
      JSonDumpCmd jsonDumpCmd = new JSonDumpCmd()
      {
        station_id = session.station_id,
        charger_id = session.charger_id,
        send_date = this.GetKoreaTime(time),
        create_date = this.GetKoreaTime(time),
        dump_type = session.dump_type.ToString(),
        dump_start_time = session.dump_start_time,
        dump_end_type = session.dump_end_type
      };
      string str = "";
      switch (delimiter)
      {
        case "STATUS":
          str = "dChargers/";
          break;
        case "START":
          str = "dChargingStart/";
          break;
        case "INFO":
          str = "dChargingInfo/";
          break;
        case "END":
          str = "dChargingEnd/";
          break;
        case "ALARM":
          str = "dAlarmHistory/";
          break;
      }
      string request = JsonConvert.SerializeObject((object) jsonDumpCmd);
      this.LogForSend("DUMP", request);
      this.EvCommLogSendServerComm("DUMP", request, time);
      return this.httpPostResponse(request, "station/" + str, session.station_id, session.charger_id, "DUMP", 1000);
    }

    public JObject SendInsertResv(ReserveSession resvSession, DateTime time)
    {
      string request = JsonConvert.SerializeObject((object) new JSonInsertResv()
      {
        station_id = resvSession.station_id,
        charger_id = resvSession.charger_id,
        send_date = this.GetKoreaTime(time),
        create_date = resvSession.create_date,
        card_num = resvSession.card_num,
        resv_stat = resvSession.resv_stat
      });
      this.LogForSend("INSERTRESV", request);
      this.EvCommLogSendServerComm("INSERTRESV", request, time);
      return this.httpPostResponse(request, "station/resv/insertresv/", resvSession.station_id, (string) null, "INSERTRESV", 1000);
    }

    public JObject SendSendSMS(ReserveSession resvSession, DateTime time)
    {
      string request = JsonConvert.SerializeObject((object) new JSonSendSMS()
      {
        station_id = resvSession.station_id,
        charger_id = resvSession.charger_id,
        send_date = this.GetKoreaTime(time),
        create_date = this.GetKoreaTime(time),
        card_num = resvSession.card_num,
        msg = resvSession.msg,
        msg_type = resvSession.msg_type.ToString(),
        Data1 = resvSession.Data1,
        Data2 = resvSession.Data2,
        Data3 = resvSession.Data3,
        Data4 = resvSession.Data4,
        Data5 = resvSession.Data5
      });
      this.LogForSend("SENDSMS", request);
      this.EvCommLogSendServerComm("SENDSMS", request, time);
      return this.httpPostResponse(request, "station/resv/sendsms/", resvSession.station_id, (string) null, "SENDSMS", 1000);
    }

    public JObject SendResvCnt(ReserveSession resvSession, DateTime time)
    {
      lock (this.lockObject)
      {
        string request = JsonConvert.SerializeObject((object) new JSonResvCnt()
        {
          station_id = resvSession.station_id
        });
        this.LogForSend("RESVCNT", request);
        this.EvCommLogSendServerComm("RESVCNT", request, time);
        return this.httpPostResponse(request, "station/resv/resvcnt/", resvSession.station_id, (string) null, "RESVCNT", 1000);
      }
    }

    public JObject SendResvStation(ReserveSession resvSession, DateTime time)
    {
      string request = JsonConvert.SerializeObject((object) new JSonResvStation()
      {
        station_id = resvSession.station_id
      });
      this.LogForSend("RESVSTATION", request);
      this.EvCommLogSendServerComm("RESVSTATION", request, time);
      return this.httpPostResponse(request, "station/resv/resvstation/", resvSession.station_id, (string) null, "RESVSTATION", 1000);
    }

    public JObject SendAuthResv(ReserveSession resvSession, DateTime time)
    {
      string request = JsonConvert.SerializeObject((object) new JSonAuthResv()
      {
        station_id = resvSession.station_id,
        send_date = this.GetKoreaTime(time),
        create_date = this.GetKoreaTime(time),
        card_num = resvSession.card_num,
        resv_stat = resvSession.resv_stat
      });
      this.LogForSend("AUTHRESV", request);
      this.EvCommLogSendServerComm("AUTHRESV", request, time);
      return this.httpPostResponse(request, "station/resv/authResv/", resvSession.station_id, (string) null, "AUTHRESV", 1000);
    }

    public JObject SendCancelResv(ReserveSession resvSession, DateTime time)
    {
      string request = JsonConvert.SerializeObject((object) new JSonCancelResv()
      {
        station_id = resvSession.station_id,
        send_date = this.GetKoreaTime(time),
        create_date = resvSession.create_date,
        card_num = resvSession.card_num
      });
      this.LogForSend("CANCELRESV", request);
      this.EvCommLogSendServerComm("CANCELRESV", request, time);
      return this.httpPostResponse(request, "station/resv/cancelResv/", resvSession.station_id, (string) null, "CANCELRESV", 1000);
    }

    public JObject SendRTimeChargerStatus(RTimeChargerStatus session, DateTime time)
    {
      this.jSonRTimeChargerStatus = (JSonRTimeChargerStatus) null;
      this.jSonRTimeChargerStatus = new JSonRTimeChargerStatus()
      {
        station_id = session.station_id,
        charger_id = session.charger_id,
        response_date = session.response_date,
        ui_ver = session.ui_ver,
        charger_status = session.charger_status,
        rf_status = session.rf_status,
        ic_status = session.ic_status,
        app_start_date = session.app_start_date,
        stop_button_status = session.stop_button_status,
        charging_mode = session.charging_mode,
        electricity_meter_mode = session.electricity_meter_mode,
        ui_mode = session.ui_mode,
        power_module = session.power_module,
        free_space = session.free_space,
        ava_mem = session.ava_mem,
        timelimit_yn = session.timelimit_yn,
        timelimit_value = session.timelimit_value,
        test_yn = session.test_yn,
        pay_yn = session.pay_yn,
        volume_day = session.volume_day,
        volume_night = session.volume_night,
        volumemovie_day = session.volumemovie_day,
        volumemovie_night = session.volumemovie_night,
        charger_firmware = session.charger_firmware,
        notice_cnt = session.notice_cnt,
        system_date = session.system_date,
        lcd_ip = session.lcd_ip,
        current_unit_cost = session.current_unit_cost
      };
      string request = JsonConvert.SerializeObject((object) this.jSonRTimeChargerStatus);
      this.LogForSend("RTIMECHGRSTAT", request);
      this.EvCommLogSendServerComm("RTIMECHGRSTAT", request, time);
      return this.httpPostResponse(request, "station/status/", session.station_id, session.charger_id, "RTIMECHGRSTAT", 1000);
    }

    public JObject SendCheckCurrentUnitCost(CheckCurrentUnitCost session, DateTime time)
    {
      this.jSonCheckCurrentUnitCost = (JSonCheckCurrentUnitCost) null;
      this.jSonCheckCurrentUnitCost = new JSonCheckCurrentUnitCost()
      {
        station_id = session.station_id,
        charger_id = session.charger_id,
        send_date = this.GetKoreaTime(time),
        create_date = this.GetKoreaTime(time)
      };
      string request = JsonConvert.SerializeObject((object) this.jSonCheckCurrentUnitCost);
      this.LogForSend("CHECK_CURRENT_UNIT_COST", request);
      this.EvCommLogSendServerComm("CHECK_CURRENT_UNIT_COST", request, time);
      return this.httpPostResponse(request, "station/iccard/", session.station_id, session.charger_id, "CHECK_CURRENT_UNIT_COST", 1000);
    }

    public JObject SendCheckUpdate(
      CheckUpdate session,
      DateTime time,
      string ver_kind,
      string new_ver)
    {
      this.jSonCheckUpdate = (JSonCheckUpdate) null;
      this.jSonCheckUpdate = new JSonCheckUpdate()
      {
        station_id = session.station_id,
        charger_id = session.charger_id,
        send_date = this.GetKoreaTime(time),
        create_date = this.GetKoreaTime(time),
        ver_kind = ver_kind,
        ver_no = new_ver
      };
      string request = JsonConvert.SerializeObject((object) this.jSonCheckUpdate);
      this.LogForSend("CHECK_UPDATE", request);
      this.EvCommLogSendServerComm("CHECK_UPDATE", request, time);
      return this.httpPostResponse(request, "station/update/", session.station_id, session.charger_id, "CHECK_UPDATE", 1000);
    }

    public bool SendPathUpdate(
      string patch_id,
      string ver_kind,
      string ver_no,
      string patch_file,
      string md5,
      DateTime time,
      string station_id,
      string charger_id)
    {
      this.LogForSend("UPDATE", "");
      this.EvCommLogSendServerComm("UPDATE", "", time);
      return this.httpUpdatePatch("updater/patch/", station_id, charger_id, "UPDATE", patch_id, patch_file, ver_kind);
    }

    public JObject SendRemoteDone(
      RemoteDone session,
      DateTime time,
      string cmd,
      string result,
      string result_msg,
      string tid)
    {
      this.jSonRemoteDone = (JSonRemoteDone) null;
      this.jSonRemoteDone = new JSonRemoteDone()
      {
        station_id = session.station_id,
        charger_id = session.charger_id,
        send_date = this.GetKoreaTime(time),
        create_date = this.GetKoreaTime(time),
        cmd = cmd,
        result = result,
        result_msg = result_msg
      };
      string request = JsonConvert.SerializeObject((object) this.jSonRemoteDone);
      this.LogForSend("REMOTEDONE", request);
      this.EvCommLogSendServerComm("REMOTEDONE", request, time);
      return this.httpPostResponse(request, "station/remotedone/", session.station_id, session.charger_id + "/" + tid, "REMOTEDONE", 1000);
    }

    public void LogForSend(string delimiter, string request)
    {
        _logger.Info("[SEND] "+ delimiter +" : " + request);
    }

    public void LogForUpdate(string delimiter, string request)
    {
        _logger.Info("[UPDATE] " + delimiter + " : " + request);
    }

    public void LogSendServerComm(string delimiter, string request, DateTime time)
    {
      try
      {
        if (!this.IsShowing)
          return;
        if (this.rtbServerComm.Lines.Length >= 500)
          this.rtbServerComm.Clear();
        string str = "SEND[" + this.GetKoreaTimeDisp(time) + "] " + delimiter + " |" + request;
        if (this.rtbServerComm.Text.Length > 0)
          this.rtbServerComm.Text = this.rtbServerComm.Text + "\n" + str;
        else
          this.rtbServerComm.Text = str;
        this.SetLogLastLine();
      }
      catch (Exception ex)
      {
      }
    }

    public void LogUpdateComm(string delimiter, string request, DateTime time)
    {
      try
      {
        if (!this.IsShowing)
          return;
        if (this.rtbServerComm.Lines.Length >= 500)
          this.rtbServerComm.Clear();
        string str = "UPDATE[" + this.GetKoreaTimeDisp(time) + "] " + delimiter + " |" + request;
        if (this.rtbServerComm.Text.Length > 0)
          this.rtbServerComm.Text = this.rtbServerComm.Text + "\n" + str;
        else
          this.rtbServerComm.Text = str;
        this.SetLogLastLine();
      }
      catch (Exception ex)
      {
      }
    }

    public void LogRecvServerComm(string delimiter, JObject jobj, DateTime time)
    {
      try
      {
        if (!this.IsShowing)
          return;
        if (this.rtbServerComm.Lines.Length >= 500)
          this.rtbServerComm.Clear();
        string str1 = jobj.ToString().Replace("\r\n", "").Trim();
        string str2 = "RECV[" + this.GetKoreaTimeDisp(time) + "] " + delimiter + " |" + str1;
        if (this.rtbServerComm.Text.Length > 0)
          this.rtbServerComm.Text = this.rtbServerComm.Text + "\n" + str2;
        else
          this.rtbServerComm.Text = str2;
        this.SetLogLastLine();
        this.Text = "EvCommForm (서버통신정상)";
      }
      catch (Exception ex)
      {
      }
    }

    private void SetLogLastLine()
    {
      this.rtbServerComm.SelectionStart = this.rtbServerComm.Text.Length;
      this.rtbServerComm.ScrollToCaret();
    }

    private void button1_Click(object sender, EventArgs e)
    {
      this.rtbServerComm.Text = "";
      this.IsShowing = false;
      this.Hide();
    }

    protected override void Dispose(bool disposing)
    {
      if (disposing && this.components != null)
        this.components.Dispose();
      base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
      this.rtbServerComm = new RichTextBox();
      this.button1 = new Button();
      this.panel1 = new Panel();
      this.panel1.SuspendLayout();
      this.SuspendLayout();
      this.rtbServerComm.Dock = DockStyle.Top;
      this.rtbServerComm.Location = new Point(0, 0);
      this.rtbServerComm.Name = "rtbServerComm";
      this.rtbServerComm.Size = new Size(984, 877);
      this.rtbServerComm.TabIndex = 0;
      this.rtbServerComm.Text = "";
      this.button1.BackColor = Color.Maroon;
      this.button1.ForeColor = Color.White;
      this.button1.Location = new Point(3, 5);
      this.button1.Name = "button1";
      this.button1.Size = new Size(978, 35);
      this.button1.TabIndex = 1;
      this.button1.Text = "종료";
      this.button1.UseVisualStyleBackColor = false;
      this.button1.Click += new EventHandler(this.button1_Click);
      this.panel1.BackColor = Color.LightGray;
      this.panel1.Controls.Add((Control) this.button1);
      this.panel1.Dock = DockStyle.Bottom;
      this.panel1.Location = new Point(0, 878);
      this.panel1.Name = "panel1";
      this.panel1.Size = new Size(984, 43);
      this.panel1.TabIndex = 2;
      this.AutoScaleDimensions = new SizeF(7f, 12f);
      this.AutoScaleMode = AutoScaleMode.Font;
      this.ClientSize = new Size(984, 921);
      this.ControlBox = false;
      this.Controls.Add((Control) this.panel1);
      this.Controls.Add((Control) this.rtbServerComm);
      this.MaximizeBox = false;
      this.MinimizeBox = false;
      this.Name = nameof (EvCommForm);
      this.Text = nameof (EvCommForm);
      this.TopMost = true;
      this.Load += new EventHandler(this.EvCommForm_Load);
      this.panel1.ResumeLayout(false);
      this.ResumeLayout(false);
    }
  }
}
