using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms.VisualStyles;
using Newtonsoft.Json.Linq;

namespace EvChargerUI.Services
{
    public interface IEvCommToChargerDelegate
    {
        bool ResetCharger(string stationId, string chargerId, out string errorCode);
        bool ChangeChargingUnitPrices(string stationId, string chargerId, double[] prices, string applyDate, string endDate, string createDate, out string errorCode);
        bool ChangeDisplayBrightness(string stationId, string chargerId, int dayLevel, int nightLevel, out string errorCode);
        bool ChangeSoundVolume(string stationId, string chargerId, int dayLevel, int nightLevel, out string errorCode);
        bool UpdateFirmware(string stationId, string chargerId, string versionNo, string filePath, out string errorCode);
        Task<bool> UpdateUIProgram(string stationId, string chargerId, string versionNo, string filePath, string md5Value);
        bool UpdateImageFile(string stationId, string chargerId, string versionNo, string filePath, out string errorCode);
        bool UpdateMovFile(string stationId, string chargerId, string versionNo, string filePath, out string errorCode);
        bool ChangeChargerStatus(string stationId, string chargerId, string status, out string errorCode);
        JObject GetChargerInfo(string stationId, string chargerId, out string errorCode);
        bool ChangeChargingTimeLimitInfo(string stationId, string chargerId, bool timeLimitOnFlag, int minute, out string errorCode);
        bool ChangeTestMode(string stationId, string chargerId, bool testModeOnFlag, out string errorCode);
        bool ChangePaymentRequiredFlag(string stationId, string chargerId, bool flag, out string errorCode);

        bool StartChargingAndRemoteDone(string stationId, string chargerId, string tid, string chargerType, out string errorCode);
        bool StopChargingAndRemoteDone(string stationId, string chargerId, string tid, out string errorCode);
        bool DumpReq(string stationId, string chargerId, string dumpType,  string dumpStartTime, string dumpEndTime, out string errorCode);
    }
}
