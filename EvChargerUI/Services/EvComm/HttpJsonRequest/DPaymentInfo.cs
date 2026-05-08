// Decompiled with JetBrains decompiler
// Type: EvComm.HttpJSonRequest.DPaymentInfo
// Assembly: EvComm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: ACDC758A-085B-43B8-BB89-A752CE49F7D6
// Assembly location: C:\Users\SJYOON\Downloads\EVCharger_v0216\EvComm.dll

using System.Collections.Generic;
using Newtonsoft.Json;

namespace EvChargerUI.Services.EvComm.HttpJsonRequest
{
  public class DPaymentInfo
  {
    [JsonProperty("uuid")]
    public string uuid { get; set; }

    [JsonProperty("send_type")]
    public string send_type { get; set; }

    [JsonProperty("action_type")]
    public string action_type { get; set; }

    [JsonProperty("data")]
    public List<DPaymentInfoData> data { get; set; }
  }
}
