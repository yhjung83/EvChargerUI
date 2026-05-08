// Decompiled with JetBrains decompiler
// Type: EvComm.HttpJSonRequest.DPaymentInfoData
// Assembly: EvComm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: ACDC758A-085B-43B8-BB89-A752CE49F7D6
// Assembly location: C:\Users\SJYOON\Downloads\EVCharger_v0216\EvComm.dll

using Newtonsoft.Json;

namespace EvChargerUI.Services.EvComm.HttpJsonRequest
{
  public class DPaymentInfoData
  {
    [JsonProperty("send_date")]
    public string send_date { get; set; }

    [JsonProperty("create_date")]
    public string create_date { get; set; }

    [JsonProperty("plug_id")]
    public string plug_id { get; set; }

    [JsonProperty("charge_plug_type")]
    public string charge_plug_type { get; set; }

    [JsonProperty("credit_trx_no")]
    public string credit_trx_no { get; set; }

    [JsonProperty("credit_trx_date")]
    public string credit_trx_date { get; set; }

    [JsonProperty("payment_type")]
    public string payment_type { get; set; }

    [JsonProperty("payment")]
    public string payment { get; set; }

    [JsonProperty("cancel_payment")]
    public string cancel_payment { get; set; }

    [JsonProperty("payment_detl")]
    public string payment_detl { get; set; }

    [JsonProperty("pay_fnsh_yn")]
    public string pay_fnsh_yn { get; set; }
  }
}
