// Decompiled with JetBrains decompiler
// Type: EvComm.HttpJSonRequest.JSonSendSMS
// Assembly: EvComm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: ACDC758A-085B-43B8-BB89-A752CE49F7D6
// Assembly location: C:\Users\SJYOON\Downloads\EVCharger_v0216\EvComm.dll

using Newtonsoft.Json;

namespace EvChargerUI.Services.EvComm.HttpJsonRequest
{
  public class JSonSendSMS
  {
    [JsonProperty(PropertyName = "station_id")]
    public string station_id { get; set; }

    [JsonProperty(PropertyName = "charger_id")]
    public string charger_id { get; set; }

    [JsonProperty(PropertyName = "send_date")]
    public string send_date { get; set; }

    [JsonProperty(PropertyName = "create_date")]
    public string create_date { get; set; }

    [JsonProperty(PropertyName = "card_num")]
    public string card_num { get; set; }

    [JsonProperty(PropertyName = "msg")]
    public string msg { get; set; }

    [JsonProperty(PropertyName = "msg_type")]
    public string msg_type { get; set; }

    [JsonProperty(PropertyName = "Data1")]
    public string Data1 { get; set; }

    [JsonProperty(PropertyName = "Data2")]
    public string Data2 { get; set; }

    [JsonProperty(PropertyName = "Data3")]
    public string Data3 { get; set; }

    [JsonProperty(PropertyName = "Data4")]
    public string Data4 { get; set; }

    [JsonProperty(PropertyName = "Data5")]
    public string Data5 { get; set; }
  }
}
