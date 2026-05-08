// Decompiled with JetBrains decompiler
// Type: EvComm.ChargingSession
// Assembly: EvComm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: ACDC758A-085B-43B8-BB89-A752CE49F7D6
// Assembly location: C:\Users\SJYOON\Downloads\EVCharger_v0216\EvComm.dll


namespace EvChargerUI.Services.EvComm
{
  public class ChargingSession
  {
    public string station_id = "";
    public string charger_id = "";
    public string create_date = "";
    public string start_date = "";
    public string card_num = "";
    public string previous_trno = "";
    public string previous_date = "";
    public string pay_type = "";
    public string charger_pay_yn = "";
    public int charger_type = 0;
    public uint integrated_power = 0;
    public int before_cost = 0;
    public int current_V = 0;
    public int current_A = 0;
    public int charge_time = 0;
    public uint charge_W = 0;
    public int charge_end_type = 0;
    public string end_date = "";
    public int after_cost = 0;
    public int cancel_cost = 0;
    public string point_kind = "";
    public string cancel_date = "";
    public string cancel_result = "";
    public int mode = 0;
    public int charger_state = 0;
    public int charger_door = 0;
    public int charger_plug = 0;
    public string powerbox = "0000000000011111";
    public string estimated_charge_time = "";
    public int charge_cost = 0;
    public int dump_type = 0;
    public string dump_start_time = "";
    public string dump_end_type = "";
    public string Data = "POINT";
    public string authtype = "1";
    public string unit_cost = "0";
    public string charging_rate = "0";
    public string order_no = "";

    public void InitChargingInfo(string stationId, string chargerId)
    {
      this.station_id = stationId;
      this.charger_id = chargerId;
      this.create_date = "";
      this.start_date = "";
      this.card_num = "";
      this.previous_trno = "";
      this.previous_date = "";
      this.pay_type = "";
      this.charger_pay_yn = "";
      this.charger_type = 0;
      this.integrated_power = 0U;
      this.before_cost = 0;
      this.current_V = 0;
      this.current_A = 0;
      this.charge_time = 0;
      this.charge_W = 0U;
      this.charge_end_type = 0;
      this.end_date = "";
      this.after_cost = 0;
      this.cancel_cost = 0;
      this.point_kind = "";
      this.mode = 0;
      this.charger_state = 0;
      this.charger_door = 0;
      this.charger_plug = 0;
      this.powerbox = "";
      this.estimated_charge_time = "";
      this.charge_cost = 0;
      this.dump_type = 0;
      this.dump_start_time = "";
      this.dump_end_type = "";
      this.unit_cost = "0";
      this.charging_rate = "0";
    }

    public void SetDataSendChargingStart(
      string create_date,
      string start_date,
      string card_num,
      string previous_trno,
      string previous_date,
      string pay_type,
      string charger_pay_yn,
      int charger_type,
      uint integrated_power,
      int before_cost,
      int current_V,
      int current_A,
      string estimated_charge_time,
      string unit_cost,
      string charging_rate)
    {
      this.create_date = create_date;
      this.start_date = start_date;
      this.card_num = card_num;
      this.previous_trno = previous_trno;
      this.previous_date = previous_date;
      this.pay_type = pay_type;
      this.charger_pay_yn = charger_pay_yn;
      this.charger_type = charger_type;
      this.integrated_power = integrated_power;
      this.before_cost = before_cost;
      this.current_V = current_V;
      this.current_A = current_A;
      this.estimated_charge_time = estimated_charge_time;
      this.unit_cost = unit_cost;
      this.charging_rate = charging_rate;
    }

    public void SetDataSendChargingInfo(
      string create_date,
      string start_date,
      string card_num,
      string previous_trno,
      string previous_date,
      string pay_type,
      string charger_pay_yn,
      int charger_type,
      uint integrated_power,
      int before_cost,
      int current_V,
      int current_A,
      string estimated_charge_time,
      uint charge_W,
      int charge_cost,
      string unit_cost,
      string charging_rate)
    {
      this.create_date = create_date;
      this.start_date = start_date;
      this.card_num = card_num;
      this.previous_trno = previous_trno;
      this.previous_date = previous_date;
      this.pay_type = pay_type;
      this.charger_pay_yn = charger_pay_yn;
      this.charger_type = charger_type;
      this.integrated_power = integrated_power;
      this.before_cost = before_cost;
      this.current_V = current_V;
      this.current_A = current_A;
      this.estimated_charge_time = estimated_charge_time;
      this.charge_W = charge_W;
      this.charge_cost = charge_cost;
      this.unit_cost = unit_cost;
      this.charging_rate = charging_rate;
    }

    public void SetDataSendChargingEnd(
      string create_date,
      string start_date,
      string card_num,
      string previous_trno,
      string previous_date,
      string pay_type,
      string charger_pay_yn,
      int charger_type,
      uint integrated_power,
      int before_cost,
      int current_V,
      int current_A,
      int charge_time,
      uint charge_W,
      int charge_end_type,
      string end_date,
      int after_cost,
      int cancel_cost,
      string point_kind,
      string cancel_date,
      string cancel_result,
      string unit_cost,
      string charging_rate)
    {
      this.create_date = create_date;
      this.start_date = start_date;
      this.card_num = card_num;
      this.previous_trno = previous_trno;
      this.previous_date = previous_date;
      this.pay_type = pay_type;
      this.charger_pay_yn = charger_pay_yn;
      this.charger_type = charger_type;
      this.integrated_power = integrated_power;
      this.before_cost = before_cost;
      this.current_V = current_V;
      this.current_A = current_A;
      this.charge_time = charge_time;
      this.charge_W = charge_W;
      this.charge_end_type = charge_end_type;
      this.end_date = end_date;
      this.after_cost = after_cost;
      this.cancel_cost = cancel_cost;
      this.point_kind = point_kind;
      this.cancel_date = cancel_date;
      this.cancel_result = cancel_result;
      this.unit_cost = unit_cost;
      this.charging_rate = charging_rate;
    }
  }
}
