// Decompiled with JetBrains decompiler
// Type: EvComm.HttpJSonRequest.JSonResvStation
// Assembly: EvComm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: ACDC758A-085B-43B8-BB89-A752CE49F7D6
// Assembly location: C:\Users\SJYOON\Downloads\EVCharger_v0216\EvComm.dll

using Newtonsoft.Json;

namespace EvChargerUI.Services.EvComm.HttpJsonRequest
{
  public class JSonResvStation
  {
    [JsonProperty(PropertyName = "station_id")]
    public string station_id { get; set; }
  }
}
