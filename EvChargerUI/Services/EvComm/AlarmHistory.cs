// Decompiled with JetBrains decompiler
// Type: EvComm.AlarmHistory
// Assembly: EvComm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: ACDC758A-085B-43B8-BB89-A752CE49F7D6
// Assembly location: C:\Users\SJYOON\Downloads\EVCharger_v0216\EvComm.dll


namespace EvChargerUI.Services.EvComm
{
  public class AlarmHistory
  {
    public string station_id = "";
    public string charger_id = "";
    public string create_date = "";
    public string alarm_type = "0";
    public string alarm_date = "";
    public string alarm_code = "0000";

    public void InitAlarmInfo(string stationId, string chargerId)
    {
      this.station_id = stationId;
      this.charger_id = chargerId;
      this.create_date = "";
      this.alarm_type = "0";
      this.alarm_date = "";
      this.alarm_code = "0000";
    }

    public void SetDataSendAlarmHistoryInfo(
      string create_date,
      string alarm_type,
      string alarm_date,
      string alarm_code)
    {
      this.create_date = create_date;
      this.alarm_type = alarm_type;
      this.alarm_date = alarm_date;
      this.alarm_code = alarm_code;
    }
  }
}
