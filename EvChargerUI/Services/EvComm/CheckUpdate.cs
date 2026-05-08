// Decompiled with JetBrains decompiler
// Type: EvComm.CheckUpdate
// Assembly: EvComm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: ACDC758A-085B-43B8-BB89-A752CE49F7D6
// Assembly location: C:\Users\SJYOON\Downloads\EVCharger_v0216\EvComm.dll


namespace EvChargerUI.Services.EvComm
{
  public class CheckUpdate
  {
    public string station_id = "";
    public string charger_id = "";
    public string send_date = "";
    public string create_date = "";
    public string ver_kind = "";
    public string ver_no = "";

    public void InitCheckUpdate(string stationId, string chargerId)
    {
      this.station_id = stationId;
      this.charger_id = chargerId;
      this.send_date = "";
      this.create_date = "";
      this.ver_kind = "";
      this.ver_no = "";
    }

    public void SetDataSendCheckUpdate(
      string send_date,
      string create_date,
      string ver_kind,
      string ver_no)
    {
      this.send_date = send_date;
      this.create_date = create_date;
      this.ver_kind = ver_kind;
      this.ver_no = ver_no;
    }
  }
}
