// Decompiled with JetBrains decompiler
// Type: EvComm.HttpJSonRequest.JSonRTimeChargerStatus
// Assembly: EvComm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: ACDC758A-085B-43B8-BB89-A752CE49F7D6
// Assembly location: C:\Users\SJYOON\Downloads\EVCharger_v0216\EvComm.dll

using Newtonsoft.Json;

namespace EvChargerUI.Services.EvComm.HttpJsonRequest
{
  public class JSonRTimeChargerStatus
  {
    [JsonProperty(PropertyName = "station_id")]
    public string station_id { get; set; }

    [JsonProperty(PropertyName = "charger_id")]
    public string charger_id { get; set; }

    [JsonProperty(PropertyName = "response_date")]
    public string response_date { get; set; }

    [JsonProperty(PropertyName = "ui_ver")]
    public string ui_ver { get; set; }

    [JsonProperty(PropertyName = "charger_status")]
    public string charger_status { get; set; }

    [JsonProperty(PropertyName = "rf_status")]
    public string rf_status { get; set; }

    [JsonProperty(PropertyName = "ic_status")]
    public string ic_status { get; set; }

    [JsonProperty(PropertyName = "app_start_date")]
    public string app_start_date { get; set; }

    [JsonProperty(PropertyName = "stop_button_status")]
    public string stop_button_status { get; set; }

    [JsonProperty(PropertyName = "charging_mode")]
    public string charging_mode { get; set; }

    [JsonProperty(PropertyName = "electricity_meter_mode")]
    public string electricity_meter_mode { get; set; }

    [JsonProperty(PropertyName = "ui_mode")]
    public string ui_mode { get; set; }

    [JsonProperty(PropertyName = "power_module")]
    public string power_module { get; set; }

    [JsonProperty(PropertyName = "free_space")]
    public string free_space { get; set; }

    [JsonProperty(PropertyName = "ava_mem")]
    public string ava_mem { get; set; }

    [JsonProperty(PropertyName = "timelimit_yn")]
    public string timelimit_yn { get; set; }

    [JsonProperty(PropertyName = "timelimit_value")]
    public string timelimit_value { get; set; }

    [JsonProperty(PropertyName = "test_yn")]
    public string test_yn { get; set; }

    [JsonProperty(PropertyName = "pay_yn")]
    public string pay_yn { get; set; }

    [JsonProperty(PropertyName = "volume_day")]
    public string volume_day { get; set; }

    [JsonProperty(PropertyName = "volume_night")]
    public string volume_night { get; set; }

    [JsonProperty(PropertyName = "volumemovie_day")]
    public string volumemovie_day { get; set; }

    [JsonProperty(PropertyName = "volumemovie_night")]
    public string volumemovie_night { get; set; }

    [JsonProperty(PropertyName = "charger_firmware")]
    public string charger_firmware { get; set; }

    [JsonProperty(PropertyName = "notice_cnt")]
    public string notice_cnt { get; set; }

    [JsonProperty(PropertyName = "system_date")]
    public string system_date { get; set; }

    [JsonProperty(PropertyName = "lcd_ip")]
    public string lcd_ip { get; set; }

    [JsonProperty(PropertyName = "current_unit_cost")]
    public string current_unit_cost { get; set; }
  }
}
