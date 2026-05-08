// Decompiled with JetBrains decompiler
// Type: EvComm.RTimeChargerStatus
// Assembly: EvComm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: ACDC758A-085B-43B8-BB89-A752CE49F7D6
// Assembly location: C:\Users\SJYOON\Downloads\EVCharger_v0216\EvComm.dll


namespace EvChargerUI.Services.EvComm
{
  public class RTimeChargerStatus
  {
    public string station_id = "";
    public string charger_id = "";
    public string response_date = "";
    public string ui_ver = "";
    public string charger_status = "";
    public string rf_status = "";
    public string ic_status = "";
    public string app_start_date = "";
    public string stop_button_status = "";
    public string charging_mode = "";
    public string electricity_meter_mode = "";
    public string ui_mode = "";
    public string power_module = "";
    public string free_space = "";
    public string ava_mem = "";
    public string timelimit_yn = "";
    public string timelimit_value = "";
    public string test_yn = "";
    public string pay_yn = "";
    public string volume_day = "";
    public string volume_night = "";
    public string volumemovie_day = "";
    public string volumemovie_night = "";
    public string charger_firmware = "";
    public string notice_cnt = "1";
    public string system_date = "";
    public string lcd_ip = "";
    public string current_unit_cost = "";

    public void InitRTimeChargerStatus(string stationId, string chargerId)
    {
      this.station_id = stationId;
      this.charger_id = chargerId;
      this.response_date = "";
      this.ui_ver = "";
      this.charger_status = "";
      this.rf_status = "";
      this.ic_status = "";
      this.app_start_date = "";
      this.stop_button_status = "";
      this.charging_mode = "";
      this.electricity_meter_mode = "";
      this.ui_mode = "";
      this.power_module = "";
      this.free_space = "";
      this.ava_mem = "";
      this.timelimit_yn = "";
      this.timelimit_value = "";
      this.test_yn = "";
      this.pay_yn = "";
      this.volume_day = "";
      this.volume_night = "";
      this.volumemovie_day = "";
      this.volumemovie_night = "";
      this.charger_firmware = "";
      this.notice_cnt = "1";
      this.system_date = "";
      this.lcd_ip = "";
      this.current_unit_cost = "";
    }

    public void SetDataSendRTimeChargerStatus(
      string response_date,
      string ui_ver,
      string charger_status,
      string rf_status,
      string ic_status,
      string app_start_date,
      string stop_button_status,
      string charging_mode,
      string electricity_meter_mode,
      string ui_mode,
      string power_module,
      string free_space,
      string ava_mem,
      string timelimit_yn,
      string timelimit_value,
      string test_yn,
      string pay_yn,
      string volume_day,
      string volume_night,
      string volumemovie_day,
      string volumemovie_night,
      string charger_firmware,
      string notice_cnt,
      string system_date,
      string lcd_ip,
      string current_unit_cost)
    {
      this.response_date = response_date;
      this.ui_ver = ui_ver;
      this.charger_status = charger_status;
      this.rf_status = rf_status;
      this.ic_status = ic_status;
      this.app_start_date = app_start_date;
      this.stop_button_status = stop_button_status;
      this.charging_mode = charging_mode;
      this.electricity_meter_mode = electricity_meter_mode;
      this.ui_mode = ui_mode;
      this.power_module = power_module;
      this.free_space = free_space;
      this.ava_mem = ava_mem;
      this.timelimit_yn = timelimit_yn;
      this.timelimit_value = timelimit_value;
      this.test_yn = test_yn;
      this.pay_yn = pay_yn;
      this.volume_day = volume_day;
      this.volume_night = volume_night;
      this.volumemovie_day = volumemovie_day;
      this.volumemovie_night = volumemovie_night;
      this.charger_firmware = charger_firmware;
      this.notice_cnt = notice_cnt;
      this.system_date = system_date;
      this.lcd_ip = lcd_ip;
      this.current_unit_cost = current_unit_cost;
    }
  }
}
