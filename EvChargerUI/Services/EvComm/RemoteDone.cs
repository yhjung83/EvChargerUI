// Decompiled with JetBrains decompiler
// Type: EvComm.RemoteDone
// Assembly: EvComm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: ACDC758A-085B-43B8-BB89-A752CE49F7D6
// Assembly location: C:\Users\SJYOON\Downloads\EVCharger_v0216\EvComm.dll


namespace EvChargerUI.Services.EvComm
{
  public class RemoteDone
  {
    public string station_id = "";
    public string charger_id = "";
    public string send_date = "";
    public string create_date = "";
    public string cmd = "";
    public string result = "";
    public string result_msg = "";

    public void InitRemoteDone(string stationId, string chargerId)
    {
      this.station_id = stationId;
      this.charger_id = chargerId;
      this.send_date = "";
      this.create_date = "";
      this.cmd = "";
      this.result = "";
      this.result_msg = "";
    }

    public void SetDataSendRemoteDone(
      string send_date,
      string create_date,
      string cmd,
      string result,
      string result_msg)
    {
      this.send_date = send_date;
      this.create_date = create_date;
      this.cmd = cmd;
      this.result = result;
      this.result_msg = result_msg;
    }
  }
}
