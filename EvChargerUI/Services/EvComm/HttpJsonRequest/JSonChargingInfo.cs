// Decompiled with JetBrains decompiler
// Type: EvComm.HttpJSonRequest.JSonChargingInfo
// Assembly: EvComm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: ACDC758A-085B-43B8-BB89-A752CE49F7D6
// Assembly location: C:\Users\SJYOON\Downloads\EVCharger_v0216\EvComm.dll

using Newtonsoft.Json;

namespace EvChargerUI.Services.EvComm.HttpJsonRequest
{
  public class JSonChargingInfo
  {
    [JsonProperty(PropertyName = "station_id")]
    public string station_id { get; set; }

    [JsonProperty(PropertyName = "charger_id")]
    public string charger_id { get; set; }

    [JsonProperty(PropertyName = "send_date")]
    public string send_date { get; set; }

    [JsonProperty(PropertyName = "create_date")]
    public string create_date { get; set; }

    [JsonProperty(PropertyName = "start_date")]
    public string start_date { get; set; }

    [JsonProperty(PropertyName = "card_num")]
    public string card_num { get; set; }

    [JsonProperty(PropertyName = "previous_trno")]
    public string previous_trno { get; set; }

    [JsonProperty(PropertyName = "previous_date")]
    public string previous_date { get; set; }

    [JsonProperty(PropertyName = "pay_type")]
    public string pay_type { get; set; }

    [JsonProperty(PropertyName = "charger_pay_yn")]
    public string charger_pay_yn { get; set; }

    [JsonProperty(PropertyName = "charger_type")]
    public string charger_type { get; set; }

    [JsonProperty(PropertyName = "integrated_power")]
    public string integrated_power { get; set; }

    [JsonProperty(PropertyName = "before_cost")]
    public string before_cost { get; set; }

    [JsonProperty(PropertyName = "current_V")]
    public string current_V { get; set; }

    [JsonProperty(PropertyName = "current_A")]
    public string current_A { get; set; }

    [JsonProperty(PropertyName = "estimated_charge_time")]
    public string estimated_charge_time { get; set; }

    [JsonProperty(PropertyName = "charge_W")]
    public string charge_W { get; set; }

    [JsonProperty(PropertyName = "charge_cost")]
    public string charge_cost { get; set; }

    [JsonProperty(PropertyName = "unit_cost")]
    public string unit_cost { get; set; }

    [JsonProperty(PropertyName = "charging_rate")]
    public string charging_rate { get; set; }

    [JsonProperty(PropertyName = "order_no")]
    public string order_no { get; set; }


    }
}
