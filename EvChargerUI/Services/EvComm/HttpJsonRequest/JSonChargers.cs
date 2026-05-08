// Decompiled with JetBrains decompiler
// Type: EvComm.HttpJSonRequest.JSonChargers
// Assembly: EvComm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: ACDC758A-085B-43B8-BB89-A752CE49F7D6
// Assembly location: C:\Users\SJYOON\Downloads\EVCharger_v0216\EvComm.dll

using Newtonsoft.Json;

namespace EvChargerUI.Services.EvComm.HttpJsonRequest
{
  public class JSonChargers
  {
    [JsonProperty(PropertyName = "station_id")]
    public string station_id { get; set; }

    [JsonProperty(PropertyName = "charger_id")]
    public string charger_id { get; set; }

    [JsonProperty(PropertyName = "send_date")]
    public string send_date { get; set; }

    [JsonProperty(PropertyName = "create_date")]
    public string create_date { get; set; }

    [JsonProperty(PropertyName = "mode")]
    public string mode { get; set; }

    [JsonProperty(PropertyName = "charger_state")]
    public string charger_state { get; set; }

    [JsonProperty(PropertyName = "charger_door")]
    public string charger_door { get; set; }

    [JsonProperty(PropertyName = "charger_plug")]
    public string charger_plug { get; set; }

    [JsonProperty(PropertyName = "integrated_power")]
    public string integrated_power { get; set; }

    [JsonProperty(PropertyName = "powerbox")]
    public string powerbox { get; set; }
  }
}
