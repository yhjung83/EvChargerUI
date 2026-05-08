using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EvChargerUI.Models;
using Newtonsoft.Json.Linq;

namespace EvChargerUI.Services
{
public interface IEvCommService
{
    /// <summary>
    /// 서버와의 통신 연결 상태
    /// </summary>
    bool IsServerConnected { get; }
        void Open();
        void Close();

        bool SendUser(ref ChargerChannel chargerChannel);
        bool SendChargerStatus(string stationId, string chargerId, int mode, int chargerState, int chargerDoor, int chargerPlug, uint integratedPower, string powerbox);

        bool SendChargingStart(string stationId, string chargerId, string createDate, string startDate, string cardNumber,
            string previousTrno, string previousDate, string payType, string chargerPayYn, int chargerType, uint integratedPower,
            int beforeCost, int currentV, int currentA, string estimatedChargeTime, string unitCost, string chargingRate, string orderNo);

        bool SendChargingInfo(string stationId, string chargerId, string createDate, string startDate, string cardNumber, 
            string previousTrno, string previousDate, string payType, string chargerPayYn, int chargerType, uint integratedPower,
            int beforeCost, int currentV, int currentA, string estimatedChargeTime, uint chargeW, int chargeCost, string unitCost, string chargingRate, string orderNo);

        bool SendChargingEnd(string stationId, string chargerId, string createDate, string startDate,string cardNumber,
            string previousTrno, string previousDate, string payType, string chargerPayYn, int chargerType, uint integratedPower,
            int beforeCost, int currentV, int currentA, int chargeTime, uint chargeW, int chargeEndType, string endDate,
            int afterCost, int cancelCost, string pointKind, string cancelDate, string cancelResult, string unitCost, string chargingRate, string orderNo);
        bool SendAlarmHistory(string stationId, string chargerId, string alarmType, string alarmDate, string alarmCode);

        JObject SendDumpChargerStatus(string stationId, string chargerId, int dumpType, string dumpStartTime, string dumpEndType);
        JObject SendDumpChargingStart(string stationId, string chargerId, int dumpType, string dumpStartTime, string dumpEndType);
        JObject SendDumpChargingInfo(string stationId, string chargerId, int dumpType, string dumpStartTime, string dumpEndType);
        JObject SendDumpChargingEnd(string stationId, string chargerId, int dumpType, string dumpStartTime, string dumpEndType);   
        JObject SendDumpAlarmHistory(string stationId, string chargerId, int dumpType, string dumpStartTime, string dumpEndType);

        bool SendInsertResv(string stationId, string chargerId, string cardNumber, out string reservationNo);

        bool SendSendSMS(string stationId, string chargerId, string cardNumber, string msg, string msgType,
            string data1, string data2, string data3, string data4, string data5);
        JObject SendResvCnt(string statinId);
        bool SendResvStation(string statinId, out string phoneNo, out string reservationNo);
        bool SendAuthResv(string stationId, string cardNumber);
        bool SendCancelResv(string stationId, string createDate, string cardNumber);
        JObject SendRTimeChargerStatus(string stationId,
            string chargerId,
            string responseDate,
            string uiVer,
            string chargerStatus,
            string rfStatus,
            string icStatus,
            string appStartDate,
            string stopButtonStatus,
            string chargingMode,
            string electricityMeterMode,
            string uiMode,
            string powerModule,
            string freeSpace,
            string avaMem,
            string timelimitYn,
            string timelimitValue,
            string testYn,
            string payYn,
            string volumeDay,
            string volumeNight,
            string volumemovieDay,
            string volumemovieNight,
            string chargerFirmware,
            string noticeCnt,
            string systemDate,
            string lcdIp,
            string currentUnitCost
            );

        JObject SendCheckCurrentUnitCost(string stationId, string chargerId);

        JObject SendCheckUpdate(string stationId, string chargerId, string versiongKind,
            string newVersion);
        bool SendPathUpdate(string patchId, string versionKind, string versionNo, string patchFile, string md5, string stationId, string chargerId);

        bool SendRemoteDone(string stationId, string chargerId, string tid, string cmd, string result, string resultMsg);

    }
}
