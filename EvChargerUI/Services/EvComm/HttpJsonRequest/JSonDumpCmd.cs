// Decompiled with JetBrains decompiler
// Type: EvComm.HttpJSonRequest.JSonDumpCmd
// Assembly: EvComm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: ACDC758A-085B-43B8-BB89-A752CE49F7D6
// Assembly location: C:\Users\SJYOON\Downloads\EVCharger_v0216\EvComm.dll

using Newtonsoft.Json;

namespace EvChargerUI.Services.EvComm.HttpJsonRequest
{
  public class JSonDumpCmd
  {
    [JsonProperty(PropertyName = "station_id")]
    public string station_id { get; set; }

    [JsonProperty(PropertyName = "charger_id")]
    public string charger_id { get; set; }

    [JsonProperty(PropertyName = "send_date")]
    public string send_date { get; set; }

    [JsonProperty(PropertyName = "create_date")]
    public string create_date { get; set; }

    [JsonProperty(PropertyName = "dump_type")]
    public string dump_type { get; set; }

    [JsonProperty(PropertyName = "dump_start_time")]
    public string dump_start_time { get; set; }

    [JsonProperty(PropertyName = "dump_end_type")]
    public string dump_end_type { get; set; }
  }
}
