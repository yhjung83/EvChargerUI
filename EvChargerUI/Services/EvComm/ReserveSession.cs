// Decompiled with JetBrains decompiler
// Type: EvComm.ReserveSession
// Assembly: EvComm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: ACDC758A-085B-43B8-BB89-A752CE49F7D6
// Assembly location: C:\Users\SJYOON\Downloads\EVCharger_v0216\EvComm.dll


namespace EvChargerUI.Services.EvComm
{
  public class ReserveSession
  {
    public string station_id = "";
    public string charger_id = "";
    public string create_date = "";
    public string card_num = "";
    public string resv_stat = "0";
    public string msg = "";
    public string msg_type = "sms";
    public string Data1 = (string) null;
    public string Data2 = (string) null;
    public string Data3 = (string) null;
    public string Data4 = (string) null;
    public string Data5 = (string) null;

    public void InitReserveSession(string stationId, string chargerId)
    {
      this.station_id = stationId;
      this.charger_id = chargerId;
      this.create_date = "";
      this.card_num = "";
      this.resv_stat = "0";
      this.msg = "";
      this.msg_type = "sms";
      this.Data1 = "";
      this.Data2 = "";
      this.Data3 = "";
      this.Data4 = "";
      this.Data5 = "";
    }

    public void SetDataSendReserveInfo(string create_date, string phNum)
    {
      this.create_date = create_date;
      this.card_num = phNum;
      this.resv_stat = "0";
    }

    public void SetDataSendSMS(
      string create_date,
      string phNum,
      string msg_type,
      string msg,
      string Data1,
      string Data2,
      string Data3,
      string Data4,
      string Data5)
    {
      this.create_date = create_date;
      this.card_num = phNum;
      this.msg_type = msg_type;
      this.msg = msg;
      this.Data1 = Data1;
      this.Data2 = Data2;
      this.Data3 = Data3;
      this.Data4 = Data4;
      this.Data5 = Data5;
    }

    public void SetDataSendCancelResv(string create_date, string phNum)
    {
      this.create_date = create_date;
      this.card_num = phNum;
    }

    public void SetDataSendAuthResv(string create_date, string phNum, string authNum)
    {
      this.create_date = create_date;
      this.card_num = phNum + authNum;
      this.resv_stat = "1";
    }
  }
}
